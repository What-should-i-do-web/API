using Microsoft.EntityFrameworkCore;
using WhatShouldIDo.API.Middleware;
using WhatShouldIDo.API.Validators;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Infrastructure.Data;
using WhatShouldIDo.Infrastructure.Repositories;
using WhatShouldIDo.Infrastructure.Services;
using Microsoft.OpenApi.Models;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using WhatShouldIDo.Infrastructure.Caching;
using WhatShouldIDo.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Localization;
using System.Globalization;
using WhatShouldIDo.Application.Services;
using WhatShouldIDo.Infrastructure.Interceptors;
using StackExchange.Redis;
using Serilog;
using Npgsql.EntityFrameworkCore.PostgreSQL; // ✅ PostgreSQL provider
using WhatShouldIDo.Application.Configuration;
using WhatShouldIDo.Infrastructure.Quota;
using WhatShouldIDo.API.Attributes;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using WhatShouldIDo.Infrastructure.Observability;
using WhatShouldIDo.Infrastructure.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// -------------------------------------
// Serilog configuration
// -------------------------------------
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ApplicationName", "WhatShouldIDo.API")
        .Enrich.WithProperty("Version", "2.0.0")
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
        .WriteTo.Seq("http://localhost:5341")
        .WriteTo.File("logs/api-.txt", rollingInterval: RollingInterval.Day));

// -------------------------------------
// CORS Configuration
// -------------------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://localhost:3000", "http://localhost:3001", "https://localhost:3001")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
    options.AddPolicy("Development", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

// -------------------------------------
// JWT Authentication
// -------------------------------------
var jwtSection = builder.Configuration.GetSection("JwtSettings");
var jwtKey = jwtSection["Key"]!;
var jwtIssuer = jwtSection["Issuer"]!;
var jwtAudience = jwtSection["Audience"]!;
var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

builder.Services
  .AddAuthentication(options =>
  {
      options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
      options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
  })
  .AddJwtBearer(options =>
  {
      options.RequireHttpsMetadata = false;
      options.SaveToken = true;
      options.TokenValidationParameters = new TokenValidationParameters
      {
          ValidateIssuer = true,
          ValidIssuer = jwtIssuer,
          ValidateAudience = true,
          ValidAudience = jwtAudience,
          ValidateIssuerSigningKey = true,
          IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
          ValidateLifetime = true,
          ClockSkew = TimeSpan.Zero
      };
  });

// -------------------------------------
// Redis Configuration
// -------------------------------------
var redisConnectionString = builder.Configuration["Redis:ConnectionString"] ?? "localhost:7001,localhost:7002,localhost:7003";
builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
{
    var configuration = ConfigurationOptions.Parse(redisConnectionString);
    configuration.AbortOnConnectFail = false;
    configuration.ConnectRetry = 3;
    configuration.ConnectTimeout = 5000;
    configuration.SyncTimeout = 5000;
    configuration.AsyncTimeout = 5000;
    return ConnectionMultiplexer.Connect(configuration);
});

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
});
builder.Services.AddMemoryCache();

// -------------------------------------
// Cache Configuration
// -------------------------------------
builder.Services.Configure<CacheWarmingOptions>(builder.Configuration.GetSection("CacheWarming"));
builder.Services.Configure<CacheOptions>(builder.Configuration.GetSection("CacheOptions"));
builder.Services.AddScoped<RedisHealthChecker>();
builder.Services.AddScoped<RedisClusterCacheService>();
builder.Services.AddScoped<FallbackCacheService>();
builder.Services.AddScoped<ICacheInvalidationService>(p => p.GetService<RedisClusterCacheService>()!);

builder.Services.AddScoped<ICacheService>(provider =>
{
    try
    {
        var redis = provider.GetService<IConnectionMultiplexer>();
        if (redis?.IsConnected == true)
            return provider.GetService<RedisClusterCacheService>()!;
    }
    catch { }
    return provider.GetService<FallbackCacheService>()!;
});

builder.Services.AddScoped<ICacheWarmingService, CacheWarmingService>();

// -------------------------------------
// Localization
// -------------------------------------
builder.Services.AddAdvancedRateLimit(builder.Configuration);

var supportedCultures = new[] { "en-US", "tr-TR", "es-ES", "fr-FR", "de-DE", "it-IT", "pt-PT", "ru-RU", "ja-JP", "ko-KR" };
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.SetDefaultCulture("en-US")
           .AddSupportedCultures(supportedCultures)
           .AddSupportedUICultures(supportedCultures);
    options.RequestCultureProviders.Clear();
    options.RequestCultureProviders.Add(new QueryStringRequestCultureProvider());
    options.RequestCultureProviders.Add(new AcceptLanguageHeaderRequestCultureProvider());
});

builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ILocalizationService, LocalizationService>();

// -------------------------------------
// OpenTelemetry & Observability
// -------------------------------------
// Configure observability options
builder.Services.Configure<ObservabilityOptions>(builder.Configuration.GetSection("Observability"));
builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection("Security"));

var observabilityOptions = builder.Configuration.GetSection("Observability").Get<ObservabilityOptions>()
    ?? new ObservabilityOptions();

// Register observability services
builder.Services.AddScoped<IObservabilityContext, ObservabilityContext>();
builder.Services.AddSingleton<IMetricsService, MetricsService>();

// Configure OpenTelemetry
if (observabilityOptions.Enabled)
{
    var resourceBuilder = ResourceBuilder.CreateDefault()
        .AddService(
            serviceName: observabilityOptions.ServiceName,
            serviceVersion: observabilityOptions.ServiceVersion)
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName,
            ["host.name"] = Environment.MachineName
        });

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(
            serviceName: observabilityOptions.ServiceName,
            serviceVersion: observabilityOptions.ServiceVersion))
        .WithTracing(tracing =>
        {
            tracing
                .SetResourceBuilder(resourceBuilder)
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.EnrichWithHttpRequest = (activity, httpRequest) =>
                    {
                        activity.SetTag("http.request.method", httpRequest.Method);
                        activity.SetTag("http.request.path", httpRequest.Path);
                    };
                    options.EnrichWithHttpResponse = (activity, httpResponse) =>
                    {
                        activity.SetTag("http.response.status_code", httpResponse.StatusCode);
                    };
                })
                .AddHttpClientInstrumentation()
                .AddSource("WhatShouldIDo.API")
                .AddSource("WhatShouldIDo.Redis")
                .SetSampler(new TraceIdRatioBasedSampler(observabilityOptions.TraceSamplingRatio));

            // Add OTLP exporter for traces (Tempo/Jaeger)
            if (observabilityOptions.OtlpTracesEnabled && !string.IsNullOrEmpty(observabilityOptions.OtlpTracesEndpoint))
            {
                tracing.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(observabilityOptions.OtlpTracesEndpoint);
                    options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                });
            }
        })
        .WithMetrics(metrics =>
        {
            metrics
                .SetResourceBuilder(resourceBuilder)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter("WhatShouldIDo.API");

            // Add Prometheus exporter
            if (observabilityOptions.PrometheusEnabled)
            {
                metrics.AddPrometheusExporter();
            }
        });

    Log.Information("OpenTelemetry initialized: ServiceName={ServiceName}, TraceSampling={Sampling}%, Prometheus={Prometheus}, OTLP={OTLP}",
        observabilityOptions.ServiceName,
        observabilityOptions.TraceSamplingRatio * 100,
        observabilityOptions.PrometheusEnabled,
        observabilityOptions.OtlpTracesEnabled);
}
else
{
    // Fallback to basic metrics service without OpenTelemetry
    Log.Warning("OpenTelemetry is disabled - using basic metrics only");
}

// -------------------------------------
// Database (PostgreSQL only)
// -------------------------------------
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection("DatabaseOptions"));
builder.Services.AddScoped<QueryPerformanceInterceptor>();
builder.Services.AddDbContext<WhatShouldIDoDbContext>((provider, options) =>
{
    var dbOptions = builder.Configuration
        .GetSection("DatabaseOptions")
        .Get<DatabaseOptions>() ?? new DatabaseOptions();

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: dbOptions.MaxRetryCount,
            maxRetryDelay: TimeSpan.FromSeconds(dbOptions.MaxRetryDelay),
            errorCodesToAdd: null);
        npgsqlOptions.CommandTimeout(dbOptions.CommandTimeout);
    });

    options.AddInterceptors(provider.GetService<QueryPerformanceInterceptor>()!);
    options.EnableServiceProviderCaching();
    options.EnableSensitiveDataLogging(dbOptions.EnableSensitiveDataLogging || builder.Environment.IsDevelopment());
    options.EnableDetailedErrors(dbOptions.EnableDetailedErrors || builder.Environment.IsDevelopment());

    if (dbOptions.QueryTrackingBehavior == "NoTracking")
        options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
});

// -------------------------------------
// Repositories & Services
// -------------------------------------
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IRouteRepository, RouteRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserHistoryRepository, UserHistoryRepository>(); // Surprise Me feature
builder.Services.AddScoped<IRouteService, RouteService>();
builder.Services.AddScoped<IPoiRepository, PoiRepository>();
builder.Services.AddScoped<IRoutePointRepository, RoutePointRepository>();
builder.Services.AddScoped<IPoiService, PoiService>();
builder.Services.AddScoped<IRoutePointService, RoutePointService>();
builder.Services.AddScoped<ISuggestionService, SuggestionService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IVisitTrackingService, VisitTrackingService>();
builder.Services.AddScoped<IPreferenceLearningService, PreferenceLearningService>();
builder.Services.AddScoped<IVariabilityEngine, VariabilityEngine>();
builder.Services.AddScoped<ISmartSuggestionService, SmartSuggestionService>();
builder.Services.AddHttpClient<OpenWeatherService>();
builder.Services.AddScoped<IWeatherService, OpenWeatherService>();
builder.Services.AddScoped<IContextEngine, ContextEngine>();

// -------------------------------------
// Subscription System (Mobile IAP Ready)
// -------------------------------------
// Configure subscription options
builder.Services.Configure<SubscriptionOptions>(builder.Configuration.GetSection("Feature:Subscription"));
builder.Services.AddOptions<SubscriptionOptions>()
    .Bind(builder.Configuration.GetSection("Feature:Subscription"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Register IClock for testable time handling
builder.Services.AddSingleton<IClock>(WhatShouldIDo.Infrastructure.Services.SystemClock.Instance);

// Register subscription repository
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();

// Register receipt verifier based on configuration
builder.Services.AddScoped<IReceiptVerifier>(provider =>
{
    var options = provider.GetRequiredService<IOptions<SubscriptionOptions>>().Value;
    var logger = provider.GetRequiredService<ILoggerFactory>();
    var env = provider.GetRequiredService<IHostEnvironment>();
    var clock = provider.GetRequiredService<IClock>();

    // If verification is disabled, return disabled verifier
    if (!options.VerificationEnabled)
    {
        return new WhatShouldIDo.Infrastructure.Services.Subscription.DisabledReceiptVerifier();
    }

    // In development with AllowDevTestReceipts, use dev test verifier
    if (env.IsDevelopment() && options.AllowDevTestReceipts)
    {
        return new WhatShouldIDo.Infrastructure.Services.Subscription.DevTestReceiptVerifier(
            provider.GetRequiredService<IOptions<SubscriptionOptions>>(),
            logger.CreateLogger<WhatShouldIDo.Infrastructure.Services.Subscription.DevTestReceiptVerifier>(),
            clock);
    }

    // Production: Return disabled verifier until real Apple/Google SDKs are integrated
    return new WhatShouldIDo.Infrastructure.Services.Subscription.DisabledReceiptVerifier();
});

// Register subscription service
builder.Services.AddScoped<ISubscriptionService, WhatShouldIDo.Infrastructure.Services.Subscription.SubscriptionService>();

// Log subscription configuration
var subscriptionConfig = builder.Configuration.GetSection("Feature:Subscription").Get<SubscriptionOptions>() ?? new SubscriptionOptions();
Log.Information("Subscription System Initialized: VerificationEnabled={VerificationEnabled}, AllowDevTestReceipts={AllowDevTestReceipts}",
    subscriptionConfig.VerificationEnabled, subscriptionConfig.AllowDevTestReceipts);

// -------------------------------------
// Taste Profile System
// -------------------------------------
// Configure taste quiz options
builder.Services.Configure<TasteQuizOptions>(builder.Configuration.GetSection("Feature:TasteQuiz"));
builder.Services.Configure<RecommendationScoringOptions>(builder.Configuration.GetSection("Feature:RecommendationScoring"));

// Register repositories
builder.Services.AddScoped<ITasteProfileRepository, WhatShouldIDo.Infrastructure.Repositories.TasteProfileRepository>();
builder.Services.AddScoped<ITasteEventRepository, WhatShouldIDo.Infrastructure.Repositories.TasteEventRepository>();
builder.Services.AddScoped<ITasteDraftStore, WhatShouldIDo.Infrastructure.Repositories.TasteDraftStore>();

// Register scoring services
builder.Services.AddScoped<IPlaceCategoryMapper, PlaceCategoryMapper>();
builder.Services.AddScoped<IImplicitScorer, ImplicitScorer>();
builder.Services.AddScoped<IExplicitScorer, ExplicitScorer>();
builder.Services.AddScoped<IExplainabilityService, ExplainabilityService>();
builder.Services.AddScoped<IHybridScorer, HybridScorer>();

// Register business services
builder.Services.AddScoped<ITasteQuizService, TasteQuizService>();
builder.Services.AddScoped<ITasteProfileService, TasteProfileService>();

// Log taste profile configuration
var tasteQuizConfig = builder.Configuration.GetSection("Feature:TasteQuiz").Get<TasteQuizOptions>() ?? new TasteQuizOptions();
var scoringConfig = builder.Configuration.GetSection("Feature:RecommendationScoring").Get<RecommendationScoringOptions>() ?? new RecommendationScoringOptions();
Log.Information("Taste Profile System Initialized: QuizVersion={Version}, DraftTtl={DraftTtl}h, ScoringWeights=(I:{Implicit}, E:{Explicit}, N:{Novelty}, C:{Context}, Q:{Quality})",
    tasteQuizConfig.Version,
    tasteQuizConfig.DraftTtlHours,
    scoringConfig.ImplicitWeight,
    scoringConfig.ExplicitWeight,
    scoringConfig.NoveltyWeight,
    scoringConfig.ContextWeight,
    scoringConfig.QualityWeight);

// -------------------------------------
// Hybrid Services
// -------------------------------------
builder.Services.Configure<HybridOptions>(builder.Configuration.GetSection("HybridPlaces"));
builder.Services.Configure<OpenTripMapOptions>(builder.Configuration.GetSection("OpenTripMap"));
builder.Services.Configure<CostGuardOptions>(builder.Configuration.GetSection("CostGuard"));
builder.Services.AddHttpClient<GooglePlacesProvider>();
builder.Services.AddHttpClient<OpenTripMapProvider>(client =>
{
    var otmOptions = builder.Configuration.GetSection("OpenTripMap").Get<OpenTripMapOptions>();
    client.Timeout = TimeSpan.FromMilliseconds(otmOptions?.TimeoutMs ?? 5000);
});
builder.Services.AddScoped<GooglePlacesProvider>();
builder.Services.AddSingleton<PlacesMerger>();
builder.Services.AddSingleton<CostGuard>();
builder.Services.AddSingleton<Ranker>();
//Old orchestrator for rollback capability
builder.Services.AddScoped<HybridPlacesOrchestrator>(provider =>
    new HybridPlacesOrchestrator(
        provider.GetService<GooglePlacesProvider>()!,
        provider.GetService<OpenTripMapProvider>()!,
        provider.GetService<ICacheService>()!,
        provider.GetService<PlacesMerger>()!,
        provider.GetService<Ranker>()!,
        provider.GetService<CostGuard>()!,
        provider.GetService<IOptions<HybridOptions>>()!,
        provider.GetService<ILogger<HybridPlacesOrchestrator>>()!
    ));

//Register V2 Orchestrator
builder.Services.AddScoped<HybridPlacesOrchestratorV2>(provider =>
    new HybridPlacesOrchestratorV2(
        provider.GetService<GooglePlacesProvider>()!,
        provider.GetService<OpenTripMapProvider>()!,
        provider.GetService<ICacheService>()!,
        provider.GetService<PlacesMerger>()!,
        provider.GetService<Ranker>()!,
        provider.GetService<CostGuard>()!,
        provider.GetService<IOptions<HybridOptions>>()!,
        provider.GetService<ILogger<HybridPlacesOrchestratorV2>>()!
    ));

//Register startup validation
builder.Services.AddSingleton<StartupValidationService>();

//Use V2 by default
builder.Services.AddScoped<IPlacesProvider>(provider =>
{
    var hybridOptions = provider.GetService<IOptions<HybridOptions>>()?.Value;
    return (hybridOptions?.Enabled == true)
        ? provider.GetService<HybridPlacesOrchestratorV2>()!  // ← Changed to V2
        : provider.GetService<GooglePlacesProvider>()!;
});

builder.Services.AddScoped<IPromptInterpreter, BasicPromptInterpreter>();
builder.Services.AddScoped<IGeocodingService, GoogleGeocodingService>();
builder.Services.AddScoped<IPlaceService, PlaceService>();
builder.Services.AddScoped<IAdvancedFilterService, AdvancedFilterService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IPerformanceMonitoringService, PerformanceMonitoringService>();
builder.Services.AddScoped<IDayPlanningService, DayPlanningService>();

// Intent-First Suggestion Policy
builder.Services.AddScoped<ISuggestionPolicy, SuggestionPolicyService>();

// Route Optimization & Directions Services
builder.Services.Configure<GoogleMapsOptions>(builder.Configuration.GetSection("GoogleMaps"));
builder.Services.AddHttpClient<IDirectionsService, GoogleDirectionsService>();
builder.Services.AddScoped<IRouteOptimizationService, RouteOptimizationService>();

Log.Information("Route optimization services registered: Google Directions, TSP Solver");

// -------------------------------------
// Quota & Entitlement System
// -------------------------------------
// Configure quota options
builder.Services.Configure<QuotaOptions>(builder.Configuration.GetSection("Feature:Quota"));
builder.Services.AddOptions<QuotaOptions>()
    .Bind(builder.Configuration.GetSection("Feature:Quota"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Register quota store based on configuration (with instrumentation)
builder.Services.AddSingleton<IQuotaStore>(provider =>
{
    var options = provider.GetRequiredService<IOptions<QuotaOptions>>().Value;
    var logger = provider.GetRequiredService<ILoggerFactory>();
    var metricsService = provider.GetRequiredService<IMetricsService>();

    // Create inner store based on backend configuration
    IQuotaStore innerStore = options.StorageBackend.ToLowerInvariant() switch
    {
        "redis" => new RedisQuotaStore(
            provider.GetRequiredService<IConnectionMultiplexer>(),
            logger.CreateLogger<RedisQuotaStore>()),
        "inmemory" => new InMemoryQuotaStore(logger.CreateLogger<InMemoryQuotaStore>()),
        _ => new InMemoryQuotaStore(logger.CreateLogger<InMemoryQuotaStore>())
    };

    // Wrap with instrumentation for OpenTelemetry traces and metrics
    return new InstrumentedRedisQuotaStore(
        innerStore,
        metricsService,
        logger.CreateLogger<InstrumentedRedisQuotaStore>());
});

// Register quota services
builder.Services.AddScoped<IEntitlementService, EntitlementService>();
builder.Services.AddScoped<IQuotaService, QuotaService>();

// Log effective quota configuration
var quotaConfig = builder.Configuration.GetSection("Feature:Quota").Get<QuotaOptions>() ?? new QuotaOptions();
Log.Information("Quota System Initialized: DefaultFreeQuota={DefaultFreeQuota}, DailyReset={DailyReset}, Backend={Backend}",
    quotaConfig.DefaultFreeQuota, quotaConfig.DailyResetEnabled, quotaConfig.StorageBackend);

// -------------------------------------
// MediatR Configuration (CQRS Pattern)
// -------------------------------------
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(WhatShouldIDo.Application.UseCases.Queries.SearchPlacesQuery).Assembly);
});

Log.Information("MediatR registered with application handlers");

// -------------------------------------
// AI Configuration & Services
// -------------------------------------
builder.Services.Configure<AIOptions>(builder.Configuration.GetSection("AI"));

// Register AI providers
builder.Services.AddHttpClient<WhatShouldIDo.Infrastructure.Services.AI.OpenAIProvider>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<WhatShouldIDo.Infrastructure.Services.AI.HuggingFaceProvider>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60); // HF models can be slower
});

builder.Services.AddHttpClient<WhatShouldIDo.Infrastructure.Services.AI.OllamaProvider>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(120); // Local models can be slow on first run
});

builder.Services.AddSingleton<WhatShouldIDo.Infrastructure.Services.AI.NoOpAIProvider>();
builder.Services.AddSingleton<WhatShouldIDo.Infrastructure.Services.AI.AIProviderFactory>();

// Register AI service with provider factory
builder.Services.AddScoped<IAIService>(provider =>
{
    var factory = provider.GetRequiredService<WhatShouldIDo.Infrastructure.Services.AI.AIProviderFactory>();
    var cacheService = provider.GetService<ICacheService>();
    var logger = provider.GetRequiredService<ILogger<WhatShouldIDo.Infrastructure.Services.AI.AIService>>();
    var options = provider.GetRequiredService<IOptions<AIOptions>>();

    var primaryProvider = factory.CreatePrimaryProvider();
    var fallbackProvider = factory.CreateFallbackProvider();

    return new WhatShouldIDo.Infrastructure.Services.AI.AIService(primaryProvider, options, logger, cacheService, fallbackProvider);
});

var aiProviderName = builder.Configuration["AI:Provider"] ?? "OpenAI";
var aiEnabled = builder.Configuration.GetValue<bool>("AI:Enabled", true);

Log.Information("AI service configured: Enabled={Enabled}, Provider={Provider}", aiEnabled, aiProviderName);

// -------------------------------------
// Health Checks
// -------------------------------------
builder.Services.AddHealthChecks()
    .AddCheck<RedisHealthCheck>(
        name: "redis",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "ready", "redis" })
    .AddCheck<PostgresHealthCheck>(
        name: "postgres",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "ready", "database" })
    .AddCheck(
        name: "self",
        check: () => HealthCheckResult.Healthy("API is running"),
        tags: new[] { "live" });

Log.Information("Health checks registered: Redis, PostgreSQL, Self");

// -------------------------------------
// Controllers & Validation
// -------------------------------------
builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<CreateRouteRequestValidator>();

// -------------------------------------
// Swagger
// -------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "WhatShouldIDo API",
        Version = "v1"
    });

    // 🔑 DTO name collision fix (API vs Application)
    c.CustomSchemaIds(type => type.FullName);
});


// -------------------------------------
// Background Jobs
// -------------------------------------
// Register AI-related services
builder.Services.AddScoped<WhatShouldIDo.Infrastructure.Services.AI.DiversityHelper>();

// Configure background job options
builder.Services.Configure<WhatShouldIDo.Infrastructure.BackgroundJobs.PreferenceUpdateJobOptions>(
    builder.Configuration.GetSection("BackgroundJobs:PreferenceUpdate"));

builder.Services.Configure<WhatShouldIDo.Infrastructure.BackgroundJobs.UserActionCleanupJobOptions>(
    builder.Configuration.GetSection("BackgroundJobs:UserActionCleanup"));

// Register background jobs as hosted services (only if enabled)
var preferenceUpdateOptions = builder.Configuration
    .GetSection("BackgroundJobs:PreferenceUpdate")
    .Get<WhatShouldIDo.Infrastructure.BackgroundJobs.PreferenceUpdateJobOptions>()
    ?? new WhatShouldIDo.Infrastructure.BackgroundJobs.PreferenceUpdateJobOptions();

var userActionCleanupOptions = builder.Configuration
    .GetSection("BackgroundJobs:UserActionCleanup")
    .Get<WhatShouldIDo.Infrastructure.BackgroundJobs.UserActionCleanupJobOptions>()
    ?? new WhatShouldIDo.Infrastructure.BackgroundJobs.UserActionCleanupJobOptions();

if (preferenceUpdateOptions.Enabled)
{
    builder.Services.AddHostedService<WhatShouldIDo.Infrastructure.BackgroundJobs.PreferenceUpdateJob>();
    Log.Information("PreferenceUpdateJob enabled: Interval={Interval}min, BatchSize={BatchSize}",
        preferenceUpdateOptions.IntervalMinutes, preferenceUpdateOptions.BatchSize);
}
else
{
    Log.Information("PreferenceUpdateJob is disabled");
}

if (userActionCleanupOptions.Enabled)
{
    builder.Services.AddHostedService<WhatShouldIDo.Infrastructure.BackgroundJobs.UserActionCleanupJob>();
    Log.Information("UserActionCleanupJob enabled: Interval={Interval}h, RetentionDays={Days}",
        userActionCleanupOptions.IntervalHours, userActionCleanupOptions.RetentionDays);
}
else
{
    Log.Information("UserActionCleanupJob is disabled");
}

// -------------------------------------
// Build & Pipeline
// -------------------------------------
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var validator = scope.ServiceProvider.GetRequiredService<StartupValidationService>();
    validator.ValidateConfiguration();
    validator.LogConfigurationSummary();
}

// Middleware pipeline - ORDER MATTERS!
app.UseMiddleware<GlobalExceptionMiddleware>();           // 1. Exception handling (outermost)
app.UseMiddleware<CorrelationIdMiddleware>();             // 2. Correlation ID + W3C trace context
app.UseMiddleware<MetricsMiddleware>();                   // 3. Metrics collection
app.UseMiddleware<AdvancedRateLimitMiddleware>();         // 4. Rate limiting

if (app.Environment.IsDevelopment())
{
    app.UseCors("Development");
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "WhatShouldIDo API v1"));
}
else
{
    app.UseCors("AllowFrontend");
}


// -------------------------------------
// Health & Metrics Endpoints
// -------------------------------------
// Legacy simple health endpoint (backward compatibility)
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .AllowAnonymous()
    .WithMetadata(new SkipQuotaAttribute())
    .ExcludeFromDescription();

// Readiness probe - checks all dependencies (Redis, Postgres)
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
    AllowCachingResponses = false
})
.AllowAnonymous()
.WithMetadata(new SkipQuotaAttribute())
.WithName("Readiness")
.WithTags("health");

// Liveness probe - checks if the app is running (no dependency checks)
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
    AllowCachingResponses = false
})
.AllowAnonymous()
.WithMetadata(new SkipQuotaAttribute())
.WithName("Liveness")
.WithTags("health");

// Startup probe - same as readiness (for Kubernetes startup probe)
app.MapHealthChecks("/health/startup", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
    AllowCachingResponses = false
})
.AllowAnonymous()
.WithMetadata(new SkipQuotaAttribute())
.WithName("Startup")
.WithTags("health");

// Prometheus metrics endpoint
if (observabilityOptions.Enabled && observabilityOptions.PrometheusEnabled)
{
    app.MapPrometheusScrapingEndpoint(observabilityOptions.PrometheusEndpoint)
        .AllowAnonymous()
        .WithMetadata(new SkipQuotaAttribute())
        .WithName("Metrics")
        .ExcludeFromDescription();

    Log.Information("Prometheus metrics endpoint available at {Endpoint}", observabilityOptions.PrometheusEndpoint);
}

// -------------------------------------
// Request Pipeline
// -------------------------------------
app.UseRequestLocalization();
app.UseAuthentication();
app.UseApiRateLimit();
app.UseAuthorization();
app.UseMiddleware<EntitlementAndQuotaMiddleware>();  // After auth/authz
app.MapControllers();

Log.Information("WhatShouldIDo API started successfully");
Log.Information("Health endpoints: /health/ready, /health/live, /health/startup");
if (observabilityOptions.Enabled && observabilityOptions.PrometheusEnabled)
{
    Log.Information("Metrics endpoint: {Endpoint}", observabilityOptions.PrometheusEndpoint);
}

app.Run();

public partial class Program { }
