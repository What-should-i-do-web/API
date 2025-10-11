using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Application.Common;
using WhatShouldIDo.Domain.Entities;
using WhatShouldIDo.Infrastructure.Options;
using Microsoft.Extensions.Options;
using WhatShouldIDo.Infrastructure.Caching;
using Microsoft.Extensions.Logging;

namespace WhatShouldIDo.Infrastructure.Services;

/// <summary>
/// Enhanced Hybrid Places Orchestrator with robust fallback, radius widening, and comprehensive telemetry
/// </summary>
public class HybridPlacesOrchestratorV2 : IPlacesProvider
{
    private readonly GooglePlacesProvider _googleProvider;
    private readonly OpenTripMapProvider _otmProvider;
    private readonly ICacheService _cache;
    private readonly PlacesMerger _merger;
    private readonly Ranker _ranker;
    private readonly CostGuard _costGuard;
    private readonly HybridOptions _options;
    private readonly ILogger<HybridPlacesOrchestratorV2> _logger;

    public HybridPlacesOrchestratorV2(
        GooglePlacesProvider googleProvider,
        OpenTripMapProvider otmProvider,
        ICacheService cache,
        PlacesMerger merger,
        Ranker ranker,
        CostGuard costGuard,
        IOptions<HybridOptions> options,
        ILogger<HybridPlacesOrchestratorV2> logger)
    {
        _googleProvider = googleProvider;
        _otmProvider = otmProvider;
        _cache = cache;
        _merger = merger;
        _ranker = ranker;
        _costGuard = costGuard;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<List<Place>> GetNearbyPlacesAsync(float lat, float lng, int radius, string keyword = null)
    {
        var cacheKey = $"hyb:nearby:{lat}:{lng}:{radius}:{keyword}";

        return await _cache.GetOrSetAsync(cacheKey,
            async () =>
            {
                _logger.LogInformation("[HYBRID] Cache miss - executing hybrid search for nearby places");
                var result = await ExecuteHybridSearchWithFallback(lat, lng, radius, keyword, isTourismIntent: false);
                return result.Places;
            },
            GetCacheTtl(radius, false));
    }

    public async Task<List<Place>> SearchByPromptAsync(string textQuery, float lat, float lng, string[] priceLevels = null)
    {
        var promptHash = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(textQuery)))[..8];
        var cacheKey = $"hyb:prompt:{promptHash}:{lat}:{lng}";

        var isTourismIntent = IsTourismRelated(textQuery);

        return await _cache.GetOrSetAsync(cacheKey,
            async () =>
            {
                _logger.LogInformation("[HYBRID] Cache miss - executing hybrid search for prompt: '{query}'", textQuery);
                var result = await ExecuteHybridSearchWithFallback(lat, lng, 5000, textQuery, isTourismIntent);
                return result.Places;
            },
            GetCacheTtl(5000, result => result.Count == 0));
    }

    /// <summary>
    /// Core hybrid search with mandatory fallback and radius widening
    /// </summary>
    private async Task<HybridSearchResult> ExecuteHybridSearchWithFallback(
        float lat, float lng, int radius, string keyword, bool isTourismIntent)
    {
        _logger.LogInformation(
            "[HYBRID] Starting search | Lat: {lat} | Lng: {lng} | Radius: {radius}m | Keyword: '{keyword}' | Tourism: {tourism}",
            lat, lng, radius, keyword ?? "null", isTourismIntent);

        var allPlaces = new List<Place>();
        var attempts = new List<SearchAttempt>();

        // === ATTEMPT 1: Google Places (Primary) ===
        var googleResult = await CallGoogleProvider(lat, lng, radius, keyword);
        attempts.Add(new SearchAttempt("Google", "Primary", googleResult.Status.ToString(), googleResult.Count, radius));

        LogProviderResult(googleResult, lat, lng, radius, keyword);

        if (googleResult.HasResults)
        {
            allPlaces.AddRange(googleResult.Data!.Take(_options.PrimaryTake));
            _logger.LogInformation("[HYBRID] ✓ Google returned sufficient results ({count}). Supplementing with OTM if needed.", googleResult.Count);
        }

        // === ATTEMPT 2: OpenTripMap Fallback (if Google failed or insufficient) ===
        var needsOtmFallback = !googleResult.HasResults || googleResult.Count < _options.MinPrimaryResults || isTourismIntent;

        if (needsOtmFallback)
        {
            _logger.LogInformation(
                "[HYBRID] Triggering OTM fallback | Reason: GoogleResults={googleCount}, MinRequired={minRequired}, Tourism={tourism}",
                googleResult.Count, _options.MinPrimaryResults, isTourismIntent);

            var otmResult = await CallOpenTripMapProvider(lat, lng, radius, keyword);
            attempts.Add(new SearchAttempt("OpenTripMap", "Fallback", otmResult.Status.ToString(), otmResult.Count, radius));

            LogProviderResult(otmResult, lat, lng, radius, keyword);

            if (otmResult.HasResults)
            {
                allPlaces.AddRange(otmResult.Data!);
                _logger.LogInformation("[HYBRID] ✓ OTM supplemented with {count} results", otmResult.Count);
            }
        }
        else
        {
            _logger.LogInformation("[HYBRID] OTM fallback not needed - Google results sufficient");
        }

        // === ATTEMPT 3: Radius Widening (if still no results) ===
        if (allPlaces.Count == 0 && radius < 12000)
        {
            var widenedRadius = Math.Min(radius * 2, 12000);
            _logger.LogWarning(
                "[HYBRID] Zero results from both providers. Widening radius: {original}m → {widened}m",
                radius, widenedRadius);

            var widenedKeyword = WidenKeywords(keyword);
            _logger.LogInformation("[HYBRID] Widened keywords: '{original}' → '{widened}'", keyword ?? "null", widenedKeyword);

            // Try Google with widened params
            var googleWidened = await CallGoogleProvider(lat, lng, widenedRadius, widenedKeyword);
            attempts.Add(new SearchAttempt("Google", "Widened", googleWidened.Status.ToString(), googleWidened.Count, widenedRadius));
            LogProviderResult(googleWidened, lat, lng, widenedRadius, widenedKeyword);

            if (googleWidened.HasResults)
            {
                allPlaces.AddRange(googleWidened.Data!);
                _logger.LogInformation("[HYBRID] ✓ Widened Google search returned {count} results", googleWidened.Count);
            }
            else
            {
                // Last resort: OTM with widened params
                var otmWidened = await CallOpenTripMapProvider(lat, lng, widenedRadius, widenedKeyword);
                attempts.Add(new SearchAttempt("OpenTripMap", "Widened", otmWidened.Status.ToString(), otmWidened.Count, widenedRadius));
                LogProviderResult(otmWidened, lat, lng, widenedRadius, widenedKeyword);

                if (otmWidened.HasResults)
                {
                    allPlaces.AddRange(otmWidened.Data!);
                    _logger.LogInformation("[HYBRID] ✓ Widened OTM search returned {count} results", otmWidened.Count);
                }
            }
        }

        // === FINAL PROCESSING ===
        if (allPlaces.Count == 0)
        {
            _logger.LogWarning(
                "[HYBRID] ⚠️ NO RESULTS after all attempts | Lat: {lat} | Lng: {lng} | Attempts: {attemptCount} | Check API keys and quotas",
                lat, lng, attempts.Count);

            LogSearchAttemptsSummary(attempts);
            return new HybridSearchResult { Places = new List<Place>(), Attempts = attempts };
        }

        // Merge, deduplicate, rank
        var deduplicated = _merger.Merge(allPlaces, new List<Place>(), _options.DedupMeters);
        var ranked = _ranker.Rank(deduplicated, lat, lng);
        var final = ranked.Take(50).ToList();

        _logger.LogInformation(
            "[HYBRID] ✅ Search completed | TotalPlaces: {totalPlaces} | AfterDedup: {dedupCount} | Final: {finalCount} | Attempts: {attemptCount}",
            allPlaces.Count, deduplicated.Count, final.Count, attempts.Count);

        LogSearchAttemptsSummary(attempts);

        return new HybridSearchResult { Places = final, Attempts = attempts };
    }

    /// <summary>
    /// Call Google Provider with proper rate limit and error handling
    /// </summary>
    private async Task<ProviderResult<List<Place>>> CallGoogleProvider(float lat, float lng, int radius, string? keyword)
    {
        // Check rate limits
        if (!_costGuard.CanCall("Google"))
        {
            var reason = _costGuard.ShouldDegrade("Google") ? "Approaching quota limit" : "Rate limit exceeded";
            _logger.LogWarning("[GOOGLE] ⚠️ Rate limited | SkippedReason: {reason}", reason);
            return ProviderResult<List<Place>>.RateLimited("Google", reason);
        }

        try
        {
            List<Place> places;

            if (!string.IsNullOrEmpty(keyword))
            {
                places = await _googleProvider.SearchByPromptAsync(keyword, lat, lng, null);
            }
            else
            {
                places = await _googleProvider.GetNearbyPlacesAsync(lat, lng, radius, keyword);
            }

            _costGuard.NotifyCall("Google");

            if (places.Count == 0)
            {
                return ProviderResult<List<Place>>.NoResults("Google");
            }

            return ProviderResult<List<Place>>.Success(places, places.Count, "Google");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GOOGLE] Unexpected error calling Google Places API");
            return ProviderResult<List<Place>>.Error("Google", ex.Message);
        }
    }

    /// <summary>
    /// Call OpenTripMap Provider with proper error handling
    /// </summary>
    private async Task<ProviderResult<List<Place>>> CallOpenTripMapProvider(float lat, float lng, int radius, string? keyword)
    {
        // Check rate limits
        if (!_costGuard.CanCall("OpenTripMap"))
        {
            var reason = _costGuard.ShouldDegrade("OpenTripMap") ? "Approaching quota limit" : "Rate limit exceeded";
            _logger.LogWarning("[OTM] ⚠️ Rate limited | SkippedReason: {reason}", reason);
            return ProviderResult<List<Place>>.RateLimited("OpenTripMap", reason);
        }

        var result = await _otmProvider.GetNearbyPlacesAsync(lat, lng, radius, keyword);

        if (result.IsSuccess)
        {
            _costGuard.NotifyCall("OpenTripMap");
        }

        return result;
    }

    /// <summary>
    /// Widen keywords to broader categories when specific search fails
    /// </summary>
    private static string WidenKeywords(string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return "restaurant cafe tourist_attraction";

        // If already contains restaurant/cafe, just add tourist attractions
        if (keyword.Contains("restaurant") || keyword.Contains("cafe"))
            return $"{keyword} tourist_attraction point_of_interest";

        // Otherwise, broaden to general categories
        return $"{keyword} restaurant cafe";
    }

    /// <summary>
    /// Check if query is tourism-related
    /// </summary>
    private static bool IsTourismRelated(string prompt)
    {
        var lowered = prompt.ToLowerInvariant();
        return lowered.Contains("tourist") ||
               lowered.Contains("sightseeing") ||
               lowered.Contains("attraction") ||
               lowered.Contains("museum") ||
               lowered.Contains("historic") ||
               lowered.Contains("gezilecek") ||
               lowered.Contains("görülecek");
    }

    /// <summary>
    /// Determine cache TTL based on result quality
    /// </summary>
    private TimeSpan GetCacheTtl(int radius, bool isEmptyResult)
    {
        if (isEmptyResult)
        {
            // Short TTL for empty results to allow quick recovery
            return TimeSpan.FromSeconds(45);
        }

        // Normal TTL for successful results
        return TimeSpan.FromMinutes(_options.NearbyTtlMinutes);
    }

    private TimeSpan GetCacheTtl(int radius, Func<List<Place>, bool> resultEvaluator)
    {
        // This overload is for deferred evaluation
        return TimeSpan.FromMinutes(_options.PromptTtlMinutes);
    }

    /// <summary>
    /// Log structured provider result
    /// </summary>
    private void LogProviderResult(ProviderResult<List<Place>> result, float lat, float lng, int radius, string? keyword)
    {
        _logger.LogInformation(
            "[{provider}] Status: {status} | Count: {count} | HTTP: {httpStatus} | SkippedReason: {reason} | Lat: {lat} | Lng: {lng} | Radius: {radius}m | Keyword: '{keyword}'",
            result.ProviderName,
            result.Status,
            result.Count,
            result.HttpStatusCode?.ToString() ?? "N/A",
            result.SkippedReason ?? "None",
            lat,
            lng,
            radius,
            keyword ?? "null");
    }

    /// <summary>
    /// Log summary of all search attempts
    /// </summary>
    private void LogSearchAttemptsSummary(List<SearchAttempt> attempts)
    {
        _logger.LogInformation("[HYBRID] Search attempts summary:");
        foreach (var attempt in attempts)
        {
            _logger.LogInformation(
                "  → {provider} ({type}): {status} | {count} results | Radius: {radius}m",
                attempt.Provider, attempt.AttemptType, attempt.Status, attempt.ResultCount, attempt.Radius);
        }
    }

    private record HybridSearchResult
    {
        public List<Place> Places { get; init; } = new();
        public List<SearchAttempt> Attempts { get; init; } = new();
    }

    private record SearchAttempt(
        string Provider,
        string AttemptType,
        string Status,
        int ResultCount,
        int Radius);
}
