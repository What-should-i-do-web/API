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
// Localization & Metrics
// -------------------------------------
builder.Services.AddAdvancedRateLimit(builder.Configuration);
builder.Services.AddSingleton<IMetricsService, PrometheusMetricsService>();

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
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "WhatShouldIDo API", Version = "v1" });
});

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
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<MetricsMiddleware>();
app.UseMiddleware<AdvancedRateLimitMiddleware>();

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


app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.UseRequestLocalization();
app.UseAuthentication();
app.UseApiRateLimit();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }
