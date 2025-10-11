using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Application.Common;
using WhatShouldIDo.Domain.Entities;
using WhatShouldIDo.Infrastructure.Options;
using Microsoft.Extensions.Options;
using WhatShouldIDo.Infrastructure.Caching;
using Microsoft.Extensions.Logging;

namespace WhatShouldIDo.Infrastructure.Services;

public class HybridPlacesOrchestrator : IPlacesProvider
{
    private readonly IPlacesProvider _googleProvider;
    private readonly OpenTripMapProvider _otmProvider;
    private readonly ICacheService _cache;
    private readonly PlacesMerger _merger;
    private readonly Ranker _ranker;
    private readonly CostGuard _costGuard;
    private readonly HybridOptions _options;
    private readonly ILogger<HybridPlacesOrchestrator> _logger;

    public HybridPlacesOrchestrator(
        IPlacesProvider googleProvider,
        OpenTripMapProvider otmProvider,
        ICacheService cache,
        PlacesMerger merger,
        Ranker ranker,
        CostGuard costGuard,
        IOptions<HybridOptions> options,
        ILogger<HybridPlacesOrchestrator> logger)
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
        Console.WriteLine($"[HYBRID] GetNearbyPlacesAsync called: {lat},{lng} radius:{radius}");
        var cacheKey = $"hyb:nearby:{lat}:{lng}:{radius}:{keyword}";
        
        return await _cache.GetOrSetAsync(cacheKey, 
            async () => {
                Console.WriteLine($"[HYBRID] Cache miss, executing hybrid search");
                return await ExecuteHybridSearch(lat, lng, radius, keyword, isTourismIntent: false);
            },
            TimeSpan.FromMinutes(_options.NearbyTtlMinutes));
    }

    public async Task<List<Place>> SearchByPromptAsync(string textQuery, float lat, float lng, string[] priceLevels = null)
    {
        var promptHash = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(textQuery)))[..8];
        var cacheKey = $"hyb:prompt:{promptHash}:{lat}:{lng}";
        
        var isTourismIntent = IsTourismRelated(textQuery);
        return await _cache.GetOrSetAsync(cacheKey,
            async () => await ExecuteHybridSearch(lat, lng, 5000, textQuery, isTourismIntent),
            TimeSpan.FromMinutes(_options.PromptTtlMinutes));
    }

    private async Task<List<Place>> ExecuteHybridSearch(float lat, float lng, int radius, string keyword, bool isTourismIntent)
    {
        _logger.LogInformation("[HYBRID] ExecuteHybridSearch started for lat:{lat}, lng:{lng}, radius:{radius}, keyword:{keyword}", lat, lng, radius, keyword);
        var googleResults = new List<Place>();
        var otmResults = new List<Place>();

        // Try Google Places API first
        _logger.LogInformation("[HYBRID] Checking Google API availability...");
        if (_costGuard.CanCall("Google"))
        {
            try
            {
                _logger.LogInformation("[HYBRID] Calling Google Places API...");
                // Use SearchByPromptAsync for text queries, GetNearbyPlacesAsync for location-only
                if (!string.IsNullOrEmpty(keyword))
                {
                    _logger.LogInformation("[HYBRID] Using text search for keyword: {keyword}", keyword);
                    googleResults = await _googleProvider.SearchByPromptAsync(keyword, lat, lng, null);
                }
                else
                {
                    _logger.LogInformation("[HYBRID] Using nearby search (no keyword)");
                    googleResults = await _googleProvider.GetNearbyPlacesAsync(lat, lng, radius, keyword);
                }
                _logger.LogInformation("[HYBRID] Google returned {count} results", googleResults.Count);
                _costGuard.NotifyCall("Google");
                
                if (googleResults.Count >= _options.PrimaryTake)
                    googleResults = googleResults.Take(_options.PrimaryTake).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[HYBRID] Google Places API call failed, continuing with empty Google results");
                googleResults = new List<Place>();
            }
        }
        else
        {
            _logger.LogWarning("[HYBRID] Google API rate limit reached, skipping Google results");
        }

        var needsOtmSupplement = googleResults.Count < _options.MinPrimaryResults || 
                                 _options.ForceTourismKinds || 
                                 isTourismIntent;

        _logger.LogInformation("[HYBRID] Needs OTM supplement: {needsOtmSupplement} (Google count: {count})", needsOtmSupplement, googleResults.Count);

        // Try OpenTripMap API if needed (with fallback handling)
        if (needsOtmSupplement && _costGuard.CanCall("OpenTripMap"))
        {
            try
            {
                _logger.LogInformation("[HYBRID] Calling OpenTripMap API...");
                var otmResult = await _otmProvider.GetNearbyPlacesAsync(lat, lng, radius, keyword);

                if (otmResult.IsSuccess && otmResult.Data != null)
                {
                    otmResults = otmResult.Data;
                    _logger.LogInformation("[HYBRID] OpenTripMap returned {count} results", otmResults.Count);
                    _costGuard.NotifyCall("OpenTripMap");
                }
                else
                {
                    _logger.LogWarning("[HYBRID] OpenTripMap call unsuccessful: {status} - {reason}",
                        otmResult.Status, otmResult.SkippedReason ?? "Unknown");
                    otmResults = new List<Place>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[HYBRID] OpenTripMap API call failed (likely expired API key), continuing with Google-only results");
                otmResults = new List<Place>();
            }
        }
        else
        {
            _logger.LogInformation("[HYBRID] OpenTripMap skipped - either not needed or rate limit reached");
        }

        // If we have no results from either provider, this is a problem
        if (googleResults.Count == 0 && otmResults.Count == 0)
        {
            _logger.LogWarning("[HYBRID] No results from any provider - check API configurations");
            return new List<Place>();
        }

        _logger.LogInformation("[HYBRID] Merging and ranking results...");
        var merged = _merger.Merge(googleResults, otmResults, _options.DedupMeters);
        var ranked = _ranker.Rank(merged, lat, lng);
        _logger.LogInformation("[HYBRID] Final result: {count} places", ranked.Count);
        return ranked.Take(50).ToList();
    }

    private static bool IsTourismRelated(string prompt) =>
        prompt.ToLowerInvariant().Contains("tourist") ||
        prompt.ToLowerInvariant().Contains("sightseeing") ||
        prompt.ToLowerInvariant().Contains("attraction") ||
        prompt.ToLowerInvariant().Contains("museum") ||
        prompt.ToLowerInvariant().Contains("historic");
}
