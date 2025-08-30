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
var builder = WebApplication.CreateBuilder(args);

// Configure Serilog for structured logging
builder.Host.UseSerilog((context, configuration) => 
    configuration.ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ApplicationName", "WhatShouldIDo.API")
        .Enrich.WithProperty("Version", "2.0.0")
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
        .WriteTo.Seq("http://localhost:5341")
        .WriteTo.File("logs/api-.txt", rollingInterval: RollingInterval.Day));

// CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins("http://localhost:3000", "https://localhost:3000", "http://localhost:3001", "https://localhost:3001") // Frontend dev servers
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    
    options.AddPolicy("Development",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

//Read JWT settings from configuration
var jwtSection = builder.Configuration.GetSection("JwtSettings");
var jwtKey = jwtSection["Key"]!;
var jwtIssuer = jwtSection["Issuer"]!;
var jwtAudience = jwtSection["Audience"]!;
var keyBytes = Encoding.UTF8.GetBytes(jwtKey);
//Auth
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

// Redis Cluster Configuration
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

// Redis services with fallback
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
});
builder.Services.AddMemoryCache();

// Configure cache services
builder.Services.Configure<CacheWarmingOptions>(builder.Configuration.GetSection("CacheWarming"));
builder.Services.Configure<CacheOptions>(builder.Configuration.GetSection("CacheOptions"));

// Register cache services
builder.Services.AddScoped<RedisHealthChecker>();
builder.Services.AddScoped<RedisClusterCacheService>();
builder.Services.AddScoped<FallbackCacheService>();
builder.Services.AddScoped<ICacheInvalidationService>(provider => provider.GetService<RedisClusterCacheService>()!);

// Use Redis cluster if available, fallback to FallbackCacheService
builder.Services.AddScoped<ICacheService>(provider =>
{
    try
    {
        var redis = provider.GetService<IConnectionMultiplexer>();
        if (redis?.IsConnected == true)
        {
            return provider.GetService<RedisClusterCacheService>()!;
        }
    }
    catch
    {
        // Fall back to memory cache if Redis is not available
    }
    
    return provider.GetService<FallbackCacheService>()!;
});

// Cache warming service
builder.Services.AddScoped<ICacheWarmingService, CacheWarmingService>();

// Advanced Rate Limiting
builder.Services.AddAdvancedRateLimit(builder.Configuration);

// Prometheus Metrics
builder.Services.AddSingleton<IMetricsService, PrometheusMetricsService>();

// Localization
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

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ILocalizationService, LocalizationService>();

// Database Options Configuration
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection("DatabaseOptions"));

// Database with performance monitoring and optimization
builder.Services.AddScoped<QueryPerformanceInterceptor>();
builder.Services.AddDbContext<WhatShouldIDoDbContext>((provider, options) =>
{
    var dbOptions = builder.Configuration.GetSection("DatabaseOptions").Get<DatabaseOptions>() ?? new DatabaseOptions();
    
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"), sqlOptions =>
    {
        // Enable retries for transient failures
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: dbOptions.MaxRetryCount,
            maxRetryDelay: TimeSpan.FromSeconds(dbOptions.MaxRetryDelay),
            errorNumbersToAdd: null);
            
        // Set command timeout
        sqlOptions.CommandTimeout(dbOptions.CommandTimeout);
    });
    
    // Add performance monitoring interceptor
    options.AddInterceptors(provider.GetService<QueryPerformanceInterceptor>()!);
    
    // Performance optimizations
    options.EnableServiceProviderCaching();
    options.EnableSensitiveDataLogging(dbOptions.EnableSensitiveDataLogging || builder.Environment.IsDevelopment());
    options.EnableDetailedErrors(dbOptions.EnableDetailedErrors || builder.Environment.IsDevelopment());
    
    // Set default query tracking behavior for performance
    if (dbOptions.QueryTrackingBehavior == "NoTracking")
    {
        options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    }
});

// Repositories
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IRouteRepository, RouteRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

// Services
builder.Services.AddScoped<IRouteService, RouteService>();
builder.Services.AddScoped<IPoiRepository, PoiRepository>();
builder.Services.AddScoped<IRoutePointRepository, RoutePointRepository>();
builder.Services.AddScoped<IPoiService, PoiService>();
builder.Services.AddScoped<IRoutePointService, RoutePointService>();
builder.Services.AddScoped<ISuggestionService, SuggestionService>();
builder.Services.AddScoped<IUserService, UserService>();

// Phase 2: User Intelligence Services
builder.Services.AddScoped<IVisitTrackingService, VisitTrackingService>();
builder.Services.AddScoped<IPreferenceLearningService, PreferenceLearningService>();
builder.Services.AddScoped<IVariabilityEngine, VariabilityEngine>();
builder.Services.AddScoped<ISmartSuggestionService, SmartSuggestionService>();

// Phase 3: Advanced Context Services
builder.Services.AddHttpClient<OpenWeatherService>();
builder.Services.AddScoped<IWeatherService, OpenWeatherService>();
builder.Services.AddScoped<IContextEngine, ContextEngine>();

// Hybrid Places Configuration
builder.Services.Configure<HybridOptions>(builder.Configuration.GetSection("HybridPlaces"));
builder.Services.Configure<OpenTripMapOptions>(builder.Configuration.GetSection("OpenTripMap"));
builder.Services.Configure<CostGuardOptions>(builder.Configuration.GetSection("CostGuard"));

// HttpClients
builder.Services.AddHttpClient<GooglePlacesProvider>();
builder.Services.AddHttpClient<OpenTripMapProvider>(client => {
    var otmOptions = builder.Configuration.GetSection("OpenTripMap").Get<OpenTripMapOptions>();
    client.Timeout = TimeSpan.FromMilliseconds(otmOptions?.TimeoutMs ?? 5000);
});

// Register Google provider directly (not as IPlacesProvider)
builder.Services.AddScoped<GooglePlacesProvider>();

// Register hybrid services
builder.Services.AddSingleton<PlacesMerger>();
builder.Services.AddSingleton<CostGuard>();
builder.Services.AddSingleton<Ranker>();

// Register HybridPlacesOrchestrator with explicit Google dependency
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

// Conditional IPlacesProvider registration
builder.Services.AddScoped<IPlacesProvider>(provider => {
    var hybridOptions = provider.GetService<IOptions<HybridOptions>>()?.Value;
    if (hybridOptions?.Enabled == true)
    {
        return provider.GetService<HybridPlacesOrchestrator>()!;
    }
    return provider.GetService<GooglePlacesProvider>()!;
});

builder.Services.AddScoped<IPromptInterpreter, BasicPromptInterpreter>();
builder.Services.AddScoped<IGeocodingService, GoogleGeocodingService>();
builder.Services.AddScoped<IPlaceService, PlaceService>();
builder.Services.AddScoped<IAdvancedFilterService, AdvancedFilterService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IPerformanceMonitoringService, PerformanceMonitoringService>();
builder.Services.AddScoped<IDayPlanningService, DayPlanningService>();

// Controllers
builder.Services.AddControllers();

// Auto‐ and client‐side validation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<CreateRouteRequestValidator>();



// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "WhatShouldIDo API", Version = "v1" });
});



var googleApiKey = builder.Configuration["GooglePlaces:ApiKey"];


var app = builder.Build();





// Middleware pipeline
app.UseMiddleware<GlobalExceptionMiddleware>();

// Metrics collection (very early in pipeline)
app.UseMiddleware<MetricsMiddleware>();

// Advanced Rate Limiting (early in pipeline)
app.UseMiddleware<AdvancedRateLimitMiddleware>();

// CORS must be before Authentication and Authorization
if (app.Environment.IsDevelopment())
{
    app.UseCors("Development");
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "WhatShouldIDo API v1");
    });
}
else
{
    app.UseCors("AllowFrontend");
}

// Localization middleware
app.UseRequestLocalization();

app.UseAuthentication();
app.UseApiRateLimit(); // Add rate limiting after authentication
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }