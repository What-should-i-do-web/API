using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using WhatShouldIDo.API.Attributes;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.API.Middleware
{
    /// <summary>
    /// Middleware that enforces user entitlement and quota limits on API endpoints.
    /// Runs after authentication and authorization middleware.
    /// </summary>
    public class EntitlementAndQuotaMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<EntitlementAndQuotaMiddleware> _logger;

        public EntitlementAndQuotaMiddleware(RequestDelegate next, ILogger<EntitlementAndQuotaMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InvokeAsync(
            HttpContext context,
            IEntitlementService entitlementService,
            IQuotaService quotaService)
        {
            // Bypass everything in Development environment
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                _logger.LogDebug("Development mode - skipping Entitlement and Quota checks for {Path}", context.Request.Path);
                await _next(context);
                return;
            }
            // Skip if endpoint allows anonymous access
            var endpoint = context.GetEndpoint();
            if (endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null)
            {
                _logger.LogDebug("Endpoint {Path} allows anonymous, skipping quota check", context.Request.Path);
                await _next(context);
                return;
            }

            // Skip if endpoint is marked with [SkipQuota]
            if (endpoint?.Metadata?.GetMetadata<SkipQuotaAttribute>() != null)
            {
                _logger.LogDebug("Endpoint {Path} marked with [SkipQuota], bypassing quota check", context.Request.Path);
                await _next(context);
                return;
            }

            // Require authentication
            if (!context.User.Identity?.IsAuthenticated == true)
            {
                _logger.LogWarning("Unauthenticated access attempt to {Path}", context.Request.Path);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await WriteJsonResponse(context, new ProblemDetails
                {
                    Type = "https://errors.whatshouldido.app/unauthorized",
                    Title = "Unauthorized",
                    Status = StatusCodes.Status401Unauthorized,
                    Detail = "Authentication is required to access this resource."
                });
                return;
            }

            // Extract user ID from claims
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? context.User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                _logger.LogError("Unable to extract valid user ID from claims for path {Path}", context.Request.Path);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await WriteJsonResponse(context, new ProblemDetails
                {
                    Type = "https://errors.whatshouldido.app/invalid-token",
                    Title = "Invalid Token",
                    Status = StatusCodes.Status401Unauthorized,
                    Detail = "Unable to identify user from authentication token."
                });
                return;
            }

            // Check for [PremiumOnly] attribute
            var premiumOnly = endpoint?.Metadata?.GetMetadata<PremiumOnlyAttribute>() != null;

            // Check if user is premium
            var isPremium = await entitlementService.IsPremiumAsync(userId, context.RequestAborted);

            if (premiumOnly && !isPremium)
            {
                _logger.LogWarning("User {UserId} attempted to access premium-only endpoint {Path}",
                    userId, context.Request.Path);

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await WriteJsonResponse(context, new ProblemDetails
                {
                    Type = "https://errors.whatshouldido.app/premium-required",
                    Title = "Premium Subscription Required",
                    Status = StatusCodes.Status403Forbidden,
                    Detail = "This feature requires a premium subscription.",
                    Extensions =
                    {
                        ["premium"] = false,
                        ["userId"] = userId.ToString()
                    }
                });
                return;
            }

            // Premium users have unlimited access
            if (isPremium)
            {
                _logger.LogDebug("User {UserId} is premium, allowing request to {Path}",
                    userId, context.Request.Path);

                using var activity = Activity.Current;
                activity?.SetTag("quota.userId", userId);
                activity?.SetTag("quota.isPremium", true);
                activity?.SetTag("quota.bypass", true);

                await _next(context);
                return;
            }

            // For non-premium users, check and consume quota
            var remainingBefore = await quotaService.GetRemainingAsync(userId, context.RequestAborted);

            var consumed = await quotaService.TryConsumeAsync(userId, 1, context.RequestAborted);

            if (!consumed)
            {
                var remainingAfter = await quotaService.GetRemainingAsync(userId, context.RequestAborted);

                _logger.LogWarning(
                    "Quota exhausted for user {UserId}. Path: {Path}, RemainingBefore: {RemainingBefore}, RemainingAfter: {RemainingAfter}",
                    userId, context.Request.Path, remainingBefore, remainingAfter);

                using var activity = Activity.Current;
                activity?.SetTag("quota.userId", userId);
                activity?.SetTag("quota.isPremium", false);
                activity?.SetTag("quota.remainingBefore", remainingBefore);
                activity?.SetTag("quota.remainingAfter", remainingAfter);
                activity?.SetTag("quota.blocked", true);

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await WriteJsonResponse(context, new ProblemDetails
                {
                    Type = "https://errors.whatshouldido.app/quota-exhausted",
                    Title = "Quota Exhausted",
                    Status = StatusCodes.Status403Forbidden,
                    Detail = "You have used all 5 free requests. Upgrade to premium for unlimited access.",
                    Extensions =
                    {
                        ["remaining"] = remainingAfter,
                        ["premium"] = false,
                        ["userId"] = userId.ToString()
                    }
                });
                return;
            }

            var remainingAfterConsumption = await quotaService.GetRemainingAsync(userId, context.RequestAborted);

            _logger.LogInformation(
                "Quota consumed for user {UserId}. Path: {Path}, RemainingBefore: {RemainingBefore}, RemainingAfter: {RemainingAfter}",
                userId, context.Request.Path, remainingBefore, remainingAfterConsumption);

            using (var activity = Activity.Current)
            {
                activity?.SetTag("quota.userId", userId);
                activity?.SetTag("quota.isPremium", false);
                activity?.SetTag("quota.remainingBefore", remainingBefore);
                activity?.SetTag("quota.remainingAfter", remainingAfterConsumption);
                activity?.SetTag("quota.consumed", 1);
            }

            // Add remaining quota to response headers
            context.Response.Headers["X-Quota-Remaining"] = remainingAfterConsumption.ToString();
            context.Response.Headers["X-Quota-Limit"] = "5";

            await _next(context);
        }

        private static async Task WriteJsonResponse(HttpContext context, ProblemDetails problem)
        {
            context.Response.ContentType = "application/problem+json";

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(problem, options));
        }
    }
}
