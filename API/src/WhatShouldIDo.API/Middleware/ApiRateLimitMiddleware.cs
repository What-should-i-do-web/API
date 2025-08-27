using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using WhatShouldIDo.Application.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace WhatShouldIDo.API.Middleware
{
    public class ApiRateLimitMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiRateLimitMiddleware> _logger;

        public ApiRateLimitMiddleware(RequestDelegate next, ILogger<ApiRateLimitMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Only check rate limits for API endpoints that consume quota
            if (!ShouldCheckRateLimit(context.Request.Path))
            {
                await _next(context);
                return;
            }

            // Get user ID from JWT token
            var userId = GetUserIdFromToken(context);
            if (userId == null)
            {
                // If no valid user token, allow the request to proceed
                // The auth middleware will handle authentication
                await _next(context);
                return;
            }

            // Check if user can make API call
            var userService = context.RequestServices.GetRequiredService<IUserService>();
            var canMakeCall = await userService.CanUserMakeApiCallAsync(userId.Value);

            if (!canMakeCall)
            {
                _logger.LogWarning("API rate limit exceeded for user: {UserId}", userId);
                await WriteRateLimitExceededResponse(context);
                return;
            }

            // Increment usage counter
            await userService.IncrementApiUsageAsync(userId.Value);

            await _next(context);
        }

        private static bool ShouldCheckRateLimit(string path)
        {
            // Only check rate limits for these API endpoints
            var rateLimitedPaths = new[]
            {
                "/api/discover",
                "/api/discover/prompt", 
                "/api/discover/random",
                "/api/plan" // Future day planning endpoint
            };

            return rateLimitedPaths.Any(limitedPath => 
                path.StartsWith(limitedPath, StringComparison.OrdinalIgnoreCase));
        }

        private static Guid? GetUserIdFromToken(HttpContext context)
        {
            try
            {
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
                    return null;

                var token = authHeader.Substring("Bearer ".Length).Trim();
                var handler = new JwtSecurityTokenHandler();

                if (!handler.CanReadToken(token))
                    return null;

                var jsonToken = handler.ReadJwtToken(token);
                var userIdClaim = jsonToken.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Sub);

                if (userIdClaim?.Value != null && Guid.TryParse(userIdClaim.Value, out var userId))
                    return userId;

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static async Task WriteRateLimitExceededResponse(HttpContext context)
        {
            context.Response.StatusCode = 429; // Too Many Requests
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = "Rate limit exceeded",
                message = "You have exceeded your daily API quota. Please upgrade your subscription for higher limits.",
                retryAfter = GetSecondsUntilMidnightUTC(),
                upgradeUrl = "/api/subscription/upgrade" // Future subscription endpoint
            };

            var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(jsonResponse);
        }

        private static int GetSecondsUntilMidnightUTC()
        {
            var now = DateTime.UtcNow;
            var midnight = now.Date.AddDays(1);
            return (int)(midnight - now).TotalSeconds;
        }
    }

    // Extension method for easy middleware registration
    public static class ApiRateLimitMiddlewareExtensions
    {
        public static IApplicationBuilder UseApiRateLimit(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ApiRateLimitMiddleware>();
        }
    }
}