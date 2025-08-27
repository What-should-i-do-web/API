using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Domain.Entities;
using WhatShouldIDo.Infrastructure.Options;
using Microsoft.Extensions.Options;
using WhatShouldIDo.Infrastructure.Caching;

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

    public HybridPlacesOrchestrator(
        IPlacesProvider googleProvider,
        OpenTripMapProvider otmProvider,
        ICacheService cache,
        PlacesMerger merger,
        Ranker ranker,
        CostGuard costGuard,
        IOptions<HybridOptions> options)
    {
        _googleProvider = googleProvider;
        _otmProvider = otmProvider;
        _cache = cache;
        _merger = merger;
        _ranker = ranker;
        _costGuard = costGuard;
        _options = options.Value;
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
        Console.WriteLine($"[HYBRID] ExecuteHybridSearch started");
        var googleResults = new List<Place>();
        var otmResults = new List<Place>();

        Console.WriteLine($"[HYBRID] Checking Google API...");
        if (_costGuard.CanCall("Google"))
        {
            Console.WriteLine($"[HYBRID] Calling Google Places API...");
            // Use SearchByPromptAsync for text queries, GetNearbyPlacesAsync for location-only
            if (!string.IsNullOrEmpty(keyword))
            {
                Console.WriteLine($"[HYBRID] Using text search for keyword: {keyword}");
                googleResults = await _googleProvider.SearchByPromptAsync(keyword, lat, lng, null);
            }
            else
            {
                Console.WriteLine($"[HYBRID] Using nearby search (no keyword)");
                googleResults = await _googleProvider.GetNearbyPlacesAsync(lat, lng, radius, keyword);
            }
            Console.WriteLine($"[HYBRID] Google returned {googleResults.Count} results");
            _costGuard.NotifyCall("Google");
            
            if (googleResults.Count >= _options.PrimaryTake)
                googleResults = googleResults.Take(_options.PrimaryTake).ToList();
        }

        var needsOtmSupplement = googleResults.Count < _options.MinPrimaryResults || 
                                 _options.ForceTourismKinds || 
                                 isTourismIntent;

        Console.WriteLine($"[HYBRID] Needs OTM supplement: {needsOtmSupplement} (Google count: {googleResults.Count})");

        if (needsOtmSupplement && _costGuard.CanCall("OpenTripMap"))
        {
            Console.WriteLine($"[HYBRID] Calling OpenTripMap API...");
            otmResults = await _otmProvider.GetNearbyPlacesAsync(lat, lng, radius, keyword);
            Console.WriteLine($"[HYBRID] OpenTripMap returned {otmResults.Count} results");
            _costGuard.NotifyCall("OpenTripMap");
        }

        Console.WriteLine($"[HYBRID] Merging and ranking results...");
        var merged = _merger.Merge(googleResults, otmResults, _options.DedupMeters);
        var ranked = _ranker.Rank(merged, lat, lng);
        Console.WriteLine($"[HYBRID] Final result: {ranked.Count} places");
        return ranked.Take(50).ToList();
    }

    private static bool IsTourismRelated(string prompt) =>
        prompt.ToLowerInvariant().Contains("tourist") ||
        prompt.ToLowerInvariant().Contains("sightseeing") ||
        prompt.ToLowerInvariant().Contains("attraction") ||
        prompt.ToLowerInvariant().Contains("museum") ||
        prompt.ToLowerInvariant().Contains("historic");
}
