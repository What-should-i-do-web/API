# Program.cs Patch for Quota System

## Add After Line 21 (after using statements):
```csharp
using WhatShouldIDo.Application.Configuration;
using WhatShouldIDo.Infrastructure.Quota;
using WhatShouldIDo.API.Attributes;
```

## Add After Line 267 (after IPerformanceMonitoringService registration):
```csharp
// -------------------------------------
// Quota & Entitlement System
// -------------------------------------
// Configure quota options
builder.Services.Configure<QuotaOptions>(builder.Configuration.GetSection("Feature:Quota"));
builder.Services.AddOptions<QuotaOptions>()
    .Bind(builder.Configuration.GetSection("Feature:Quota"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Register quota store based on configuration
builder.Services.AddSingleton<IQuotaStore>(provider =>
{
    var options = provider.GetRequiredService<IOptions<QuotaOptions>>().Value;
    var logger = provider.GetRequiredService<ILoggerFactory>();

    return options.StorageBackend.ToLowerInvariant() switch
    {
        "redis" => new RedisQuotaStore(
            provider.GetRequiredService<IConnectionMultiplexer>(),
            logger.CreateLogger<RedisQuotaStore>()),
        "inmemory" => new InMemoryQuotaStore(logger.CreateLogger<InMemoryQuotaStore>()),
        _ => new InMemoryQuotaStore(logger.CreateLogger<InMemoryQuotaStore>())
    };
});

// Register quota services
builder.Services.AddScoped<IEntitlementService, EntitlementService>();
builder.Services.AddScoped<IQuotaService, QuotaService>();

// Log effective quota configuration
var quotaConfig = builder.Configuration.GetSection("Feature:Quota").Get<QuotaOptions>() ?? new QuotaOptions();
Log.Information("Quota System Initialized: DefaultFreeQuota={DefaultFreeQuota}, DailyReset={DailyReset}, Backend={Backend}",
    quotaConfig.DefaultFreeQuota, quotaConfig.DailyResetEnabled, quotaConfig.StorageBackend);
```

## Modify Line 312-313 (health endpoint):
```csharp
// BEFORE:
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// AFTER:
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .AllowAnonymous()
    .WithMetadata(new SkipQuotaAttribute());
```

## Add After Line 316 (after app.UseAuthorization()):
```csharp
app.UseMiddleware<EntitlementAndQuotaMiddleware>();
```

## Final middleware order should be:
```csharp
app.UseAuthentication();
app.UseApiRateLimit();
app.UseAuthorization();
app.UseMiddleware<EntitlementAndQuotaMiddleware>();  // ← NEW
app.MapControllers();
```
