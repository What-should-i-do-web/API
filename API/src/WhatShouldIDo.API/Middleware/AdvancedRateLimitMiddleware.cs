using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Net;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.API.Middleware
{
    public class AdvancedRateLimitMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AdvancedRateLimitMiddleware> _logger;
        private readonly RateLimitOptions _options;

        public AdvancedRateLimitMiddleware(
            RequestDelegate next,
            IMemoryCache cache,
            ILogger<AdvancedRateLimitMiddleware> logger,
            IOptions<RateLimitOptions> options)
        {
            _next = next;
            _cache = cache;
            _logger = logger;
            _options = options.Value;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var endpoint = context.GetEndpoint();
            if (endpoint == null)
            {
                await _next(context);
                return;
            }

            // Skip rate limiting for localhost in development
            var clientIP = GetClientIP(context);
            if (IsLocalhost(clientIP))
            {
                await _next(context);
                return;
            }

            // Check if rate limiting is disabled for this endpoint
            var rateLimitAttribute = endpoint.Metadata.GetMetadata<NoRateLimitAttribute>();
            if (rateLimitAttribute != null)
            {
                await _next(context);
                return;
            }

            // Get scoped cache service from HttpContext
            var cacheService = context.RequestServices.GetRequiredService<ICacheService>();

            // Get rate limit tier from API key or use default
            var rateLimitTier = await GetRateLimitTierAsync(context, cacheService);
            var clientId = GetClientIdentifier(context);

            // Check rate limits
            var rateLimitResult = await CheckRateLimitAsync(clientId, rateLimitTier, context.Request.Path);

            if (!rateLimitResult.IsAllowed)
            {
                await HandleRateLimitExceeded(context, rateLimitResult);
                return;
            }

            // Add rate limit headers
            AddRateLimitHeaders(context.Response, rateLimitResult);

            // Record request for analytics
            await RecordRequestAsync(clientId, rateLimitTier, context.Request.Path);

            await _next(context);
        }

        private async Task<RateLimitTier> GetRateLimitTierAsync(HttpContext context, ICacheService cacheService)
        {
            // Check for API key in header
            if (context.Request.Headers.TryGetValue("X-API-Key", out var apiKeyValues))
            {
                var apiKey = apiKeyValues.FirstOrDefault();
                if (!string.IsNullOrEmpty(apiKey))
                {
                    // Look up API key tier (this would be from database in real implementation)
                    var tier = await GetApiKeyTierAsync(apiKey, cacheService);
                    if (tier.HasValue)
                    {
                        return tier.Value;
                    }
                }
            }

            // Check for authentication and premium status
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var isPremium = context.User.Claims
                    .Any(c => c.Type == "tier" && c.Value == "premium");
                
                return isPremium ? RateLimitTier.Premium : RateLimitTier.Authenticated;
            }

            return RateLimitTier.Anonymous;
        }

        private async Task<RateLimitTier?> GetApiKeyTierAsync(string apiKey, ICacheService cacheService)
        {
            try
            {
                var cacheKey = $"api_key_tier:{apiKey}";
                var cachedTier = await cacheService.GetOrSetAsync(cacheKey, async () =>
                {
                    // In real implementation, this would query the database
                    // For demo, we'll use some sample API keys
                    return apiKey switch
                    {
                        "enterprise_key_123" => RateLimitTier.Enterprise,
                        "premium_key_456" => RateLimitTier.Premium,
                        "basic_key_789" => RateLimitTier.Basic,
                        _ => (RateLimitTier?)null
                    };
                }, TimeSpan.FromMinutes(15));

                return cachedTier;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error looking up API key tier for {ApiKey}", apiKey);
                return null;
            }
        }

        private string GetClientIP(HttpContext context)
        {
            // Check for forwarded IP first
            if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedValues))
            {
                var firstIP = forwardedValues.FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim();
                if (!string.IsNullOrEmpty(firstIP))
                    return firstIP;
            }

            // Check for real IP
            if (context.Request.Headers.TryGetValue("X-Real-IP", out var realIPValues))
            {
                var realIP = realIPValues.FirstOrDefault();
                if (!string.IsNullOrEmpty(realIP))
                    return realIP;
            }

            // Default to connection remote IP
            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        private bool IsLocalhost(string clientIP)
        {
            if (string.IsNullOrEmpty(clientIP)) return false;
            
            // Check for common localhost addresses
            return clientIP == "127.0.0.1" || 
                   clientIP == "::1" || 
                   clientIP == "localhost" ||
                   clientIP.StartsWith("192.168.") ||
                   clientIP.StartsWith("10.") ||
                   clientIP.StartsWith("172.16.");
        }

        private string GetClientIdentifier(HttpContext context)
        {
            // Try API key first
            if (context.Request.Headers.TryGetValue("X-API-Key", out var apiKeyValues))
            {
                var apiKey = apiKeyValues.FirstOrDefault();
                if (!string.IsNullOrEmpty(apiKey))
                {
                    return $"apikey:{apiKey}";
                }
            }

            // Try user ID if authenticated
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userId = context.User.FindFirst("sub")?.Value ?? context.User.FindFirst("id")?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    return $"user:{userId}";
                }
            }

            // Fall back to IP address
            var ipAddress = GetClientIpAddress(context);
            return $"ip:{ipAddress}";
        }

        private string GetClientIpAddress(HttpContext context)
        {
            var ipAddress = context.Connection.RemoteIpAddress;
            
            // Check for forwarded IP in headers
            if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
            {
                var firstIp = forwardedFor.FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim();
                if (!string.IsNullOrEmpty(firstIp) && IPAddress.TryParse(firstIp, out var parsedIp))
                {
                    return parsedIp.ToString();
                }
            }

            if (context.Request.Headers.TryGetValue("X-Real-IP", out var realIp))
            {
                var ip = realIp.FirstOrDefault();
                if (!string.IsNullOrEmpty(ip) && IPAddress.TryParse(ip, out var parsedIp))
                {
                    return parsedIp.ToString();
                }
            }

            return ipAddress?.ToString() ?? "unknown";
        }

        private async Task<RateLimitCheckResult> CheckRateLimitAsync(
            string clientId, 
            RateLimitTier tier, 
            string endpoint)
        {
            var limits = GetLimitsForTier(tier);
            var result = new RateLimitCheckResult
            {
                IsAllowed = true,
                Tier = tier,
                ClientId = clientId
            };

            // Check different time windows
            foreach (var limit in limits)
            {
                var windowKey = $"rate_limit:{clientId}:{limit.Window.TotalSeconds}s";
                var windowStart = DateTimeOffset.UtcNow.Subtract(limit.Window);
                
                var requestCount = await GetRequestCountInWindowAsync(windowKey, windowStart);
                
                result.RequestsInWindow = (int)requestCount;
                result.WindowSizeSeconds = (int)limit.Window.TotalSeconds;
                result.Limit = limit.MaxRequests;
                result.Remaining = Math.Max(0, limit.MaxRequests - (int)requestCount);
                result.ResetTime = windowStart.Add(limit.Window);

                if (requestCount >= limit.MaxRequests)
                {
                    result.IsAllowed = false;
                    result.RetryAfterSeconds = (int)(result.ResetTime - DateTimeOffset.UtcNow).TotalSeconds;
                    
                    _logger.LogWarning(
                        "Rate limit exceeded for {ClientId} (Tier: {Tier}). " +
                        "Requests: {RequestCount}/{Limit} in {Window}s",
                        clientId, tier, requestCount, limit.MaxRequests, limit.Window.TotalSeconds);
                    
                    break;
                }

                // Record the current request
                await RecordRequestInWindowAsync(windowKey, limit.Window);
            }

            return result;
        }

        private async Task<long> GetRequestCountInWindowAsync(string windowKey, DateTimeOffset windowStart)
        {
            try
            {
                // For simplicity, using memory cache. In production, use Redis with sliding window
                var cacheKey = $"{windowKey}:count";
                var count = _cache.Get<int?>(cacheKey) ?? 0;
                return count;
            }
            catch
            {
                return 0;
            }
        }

        private async Task RecordRequestInWindowAsync(string windowKey, TimeSpan window)
        {
            try
            {
                var cacheKey = $"{windowKey}:count";
                var count = _cache.Get<int?>(cacheKey) ?? 0;
                _cache.Set(cacheKey, count + 1, window);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording request in rate limit window");
            }
        }

        private List<RateLimit> GetLimitsForTier(RateLimitTier tier)
        {
            return tier switch
            {
                RateLimitTier.Enterprise => new List<RateLimit>
                {
                    new() { MaxRequests = 10000, Window = TimeSpan.FromHours(1) },
                    new() { MaxRequests = 1000, Window = TimeSpan.FromMinutes(1) },
                    new() { MaxRequests = 50, Window = TimeSpan.FromSeconds(1) }
                },
                RateLimitTier.Premium => new List<RateLimit>
                {
                    new() { MaxRequests = 2000, Window = TimeSpan.FromHours(1) },
                    new() { MaxRequests = 200, Window = TimeSpan.FromMinutes(1) },
                    new() { MaxRequests = 20, Window = TimeSpan.FromSeconds(1) }
                },
                RateLimitTier.Basic => new List<RateLimit>
                {
                    new() { MaxRequests = 500, Window = TimeSpan.FromHours(1) },
                    new() { MaxRequests = 50, Window = TimeSpan.FromMinutes(1) },
                    new() { MaxRequests = 5, Window = TimeSpan.FromSeconds(1) }
                },
                RateLimitTier.Authenticated => new List<RateLimit>
                {
                    new() { MaxRequests = 200, Window = TimeSpan.FromHours(1) },
                    new() { MaxRequests = 30, Window = TimeSpan.FromMinutes(1) },
                    new() { MaxRequests = 3, Window = TimeSpan.FromSeconds(1) }
                },
                RateLimitTier.Anonymous => new List<RateLimit>
                {
                    new() { MaxRequests = 1000, Window = TimeSpan.FromHours(1) },
                    new() { MaxRequests = 100, Window = TimeSpan.FromMinutes(1) },
                    new() { MaxRequests = 10, Window = TimeSpan.FromSeconds(1) }
                },
                _ => new List<RateLimit>
                {
                    new() { MaxRequests = 50, Window = TimeSpan.FromHours(1) },
                    new() { MaxRequests = 5, Window = TimeSpan.FromMinutes(1) }
                }
            };
        }

        private async Task HandleRateLimitExceeded(HttpContext context, RateLimitCheckResult result)
        {
            context.Response.StatusCode = 429; // Too Many Requests
            context.Response.Headers["Retry-After"] = result.RetryAfterSeconds.ToString();
            
            AddRateLimitHeaders(context.Response, result);

            var response = new
            {
                error = "Rate limit exceeded",
                tier = result.Tier.ToString(),
                limit = result.Limit,
                remaining = result.Remaining,
                resetTime = result.ResetTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                retryAfterSeconds = result.RetryAfterSeconds,
                upgrade = result.Tier == RateLimitTier.Anonymous 
                    ? "Consider upgrading to a premium plan for higher limits"
                    : null
            };

            await context.Response.WriteAsJsonAsync(response);
        }

        private void AddRateLimitHeaders(HttpResponse response, RateLimitCheckResult result)
        {
            response.Headers["X-RateLimit-Tier"] = result.Tier.ToString();
            response.Headers["X-RateLimit-Limit"] = result.Limit.ToString();
            response.Headers["X-RateLimit-Remaining"] = result.Remaining.ToString();
            response.Headers["X-RateLimit-Reset"] = result.ResetTime.ToUnixTimeSeconds().ToString();
            response.Headers["X-RateLimit-Window"] = result.WindowSizeSeconds.ToString();
        }

        private async Task RecordRequestAsync(string clientId, RateLimitTier tier, string endpoint)
        {
            try
            {
                // Record for analytics - this would be sent to analytics service
                _logger.LogInformation(
                    "API Request: Client={ClientId}, Tier={Tier}, Endpoint={Endpoint}",
                    clientId, tier, endpoint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording request analytics");
            }
        }
    }

    // Supporting classes
    public enum RateLimitTier
    {
        Anonymous,
        Authenticated,
        Basic,
        Premium,
        Enterprise
    }

    public class RateLimit
    {
        public int MaxRequests { get; set; }
        public TimeSpan Window { get; set; }
    }

    public class RateLimitCheckResult
    {
        public bool IsAllowed { get; set; }
        public RateLimitTier Tier { get; set; }
        public string ClientId { get; set; } = string.Empty;
        public int RequestsInWindow { get; set; }
        public int WindowSizeSeconds { get; set; }
        public int Limit { get; set; }
        public int Remaining { get; set; }
        public DateTimeOffset ResetTime { get; set; }
        public int RetryAfterSeconds { get; set; }
    }

    public class RateLimitOptions
    {
        public bool EnableRateLimiting { get; set; } = true;
        public bool EnableAnalytics { get; set; } = true;
        public Dictionary<string, int> CustomLimits { get; set; } = new();
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class NoRateLimitAttribute : Attribute
    {
    }
}

// Extension method for IOptions
namespace Microsoft.Extensions.DependencyInjection
{
    public static class RateLimitExtensions
    {
        public static IServiceCollection AddAdvancedRateLimit(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<WhatShouldIDo.API.Middleware.RateLimitOptions>(configuration.GetSection("RateLimiting"));
            return services;
        }
    }
}