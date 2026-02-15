using MediatR;
using Microsoft.Extensions.Logging;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Application.UseCases.Queries;

namespace WhatShouldIDo.Application.UseCases.Handlers
{
    /// <summary>
    /// Handler for searching places with AI-powered interpretation and ranking
    /// </summary>
    public class SearchPlacesQueryHandler : IRequestHandler<SearchPlacesQuery, SearchPlacesResult>
    {
        private readonly IAIService _aiService;
        private readonly IPlacesProvider _placesProvider;
        private readonly ICacheService _cacheService;
        private readonly ILogger<SearchPlacesQueryHandler> _logger;

        public SearchPlacesQueryHandler(
            IAIService aiService,
            IPlacesProvider placesProvider,
            ICacheService cacheService,
            ILogger<SearchPlacesQueryHandler> logger)
        {
            _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
            _placesProvider = placesProvider ?? throw new ArgumentNullException(nameof(placesProvider));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<SearchPlacesResult> Handle(SearchPlacesQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Searching places with query: {Query}", request.Query);

            var result = new SearchPlacesResult
            {
                UsedAI = false
            };

            try
            {
                // Step 1: Interpret the prompt using AI
                var interpreted = await _aiService.InterpretPromptAsync(request.Query, cancellationToken);
                result.InterpretedQuery = interpreted.TextQuery;
                result.ExtractedCategories = interpreted.Categories;
                result.AIConfidence = interpreted.Confidence;
                result.UsedAI = true;

                _logger.LogInformation("AI interpretation complete. Categories: {Categories}, Confidence: {Confidence}",
                    string.Join(", ", interpreted.Categories), interpreted.Confidence);

                // Step 2: Build place types filter
                var placeTypes = request.PlaceTypes ?? new List<string>();
                if (interpreted.Categories.Any())
                {
                    placeTypes = interpreted.Categories
                        .Where(c => !string.IsNullOrWhiteSpace(c))
                        .ToList();
                }

                // Step 3: Fetch places from provider
                var places = await _placesProvider.SearchNearbyAsync(
                    request.Latitude,
                    request.Longitude,
                    request.Radius,
                    placeTypes.Any() ? string.Join(",", placeTypes) : null,
                    request.MaxResults);

                if (places == null || !places.Any())
                {
                    _logger.LogWarning("No places found for query: {Query}", request.Query);
                    return result;
                }

                _logger.LogInformation("Found {Count} places from provider", places.Count);

                // Step 4: Apply AI ranking if enabled
                if (request.UseAIRanking && places.Count > 1)
                {
                    try
                    {
                        places = await _aiService.RankPlacesByRelevanceAsync(
                            request.Query,
                            places,
                            cancellationToken);

                        _logger.LogInformation("Places ranked by AI relevance");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to rank places by AI, using provider order");
                    }
                }

                // Step 5: Filter by price level if specified
                if (!string.IsNullOrEmpty(request.PriceLevel) || interpreted.PricePreferences.Any())
                {
                    var allowedPriceLevels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (!string.IsNullOrEmpty(request.PriceLevel))
                    {
                        allowedPriceLevels.Add(request.PriceLevel);
                    }
                    foreach (var pref in interpreted.PricePreferences)
                    {
                        allowedPriceLevels.Add(pref);
                    }

                    places = places
                        .Where(p => string.IsNullOrEmpty(p.PriceLevel) || allowedPriceLevels.Contains(p.PriceLevel))
                        .ToList();
                }

                // Step 6: Apply dietary restrictions filtering if available
                if (interpreted.DietaryRestrictions.Any())
                {
                    // This would require more sophisticated filtering based on place descriptions or tags
                    // For now, we'll log it but not filter
                    _logger.LogInformation("Dietary restrictions noted: {Restrictions}",
                        string.Join(", ", interpreted.DietaryRestrictions));
                }

                result.Places = places.Take(request.MaxResults).ToList();
                result.TotalCount = result.Places.Count;

                _logger.LogInformation("Search complete. Returning {Count} places", result.TotalCount);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching places with query: {Query}", request.Query);

                // Fallback: try basic search without AI
                try
                {
                    var places = await _placesProvider.SearchNearbyAsync(
                        request.Latitude,
                        request.Longitude,
                        request.Radius,
                        request.PlaceTypes != null ? string.Join(",", request.PlaceTypes) : null,
                        request.MaxResults);

                    result.Places = places ?? new List<PlaceDto>();
                    result.TotalCount = result.Places.Count;
                    result.UsedAI = false;

                    return result;
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Fallback search also failed");
                    throw;
                }
            }
        }
    }
}
