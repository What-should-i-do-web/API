using MediatR;
using Microsoft.Extensions.Logging;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Application.UseCases.Queries;

namespace WhatShouldIDo.Application.UseCases.Handlers
{
    /// <summary>
    /// Handler for generating AI-powered place summaries
    /// </summary>
    public class GetPlaceSummaryQueryHandler : IRequestHandler<GetPlaceSummaryQuery, PlaceSummaryResult>
    {
        private readonly IPlacesProvider _placesProvider;
        private readonly IAIService _aiService;
        private readonly ICacheService _cacheService;
        private readonly ILogger<GetPlaceSummaryQueryHandler> _logger;

        public GetPlaceSummaryQueryHandler(
            IPlacesProvider placesProvider,
            IAIService aiService,
            ICacheService cacheService,
            ILogger<GetPlaceSummaryQueryHandler> logger)
        {
            _placesProvider = placesProvider ?? throw new ArgumentNullException(nameof(placesProvider));
            _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PlaceSummaryResult> Handle(GetPlaceSummaryQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Generating place summary for: {PlaceId} with style: {Style}",
                request.PlaceId, request.Style);

            try
            {
                // Check cache first (24-hour cache for summaries)
                var cacheKey = $"place_summary:{request.PlaceId}:{request.Style}";
                var cachedResult = await _cacheService.GetAsync<PlaceSummaryResult>(cacheKey);

                if (cachedResult != null)
                {
                    _logger.LogDebug("Returning cached summary for place: {PlaceId}", request.PlaceId);
                    return cachedResult;
                }

                // Step 1: Fetch place details
                var place = await _placesProvider.GetPlaceDetailsAsync(request.PlaceId);

                if (place == null)
                {
                    throw new InvalidOperationException($"Place not found: {request.PlaceId}");
                }

                _logger.LogInformation("Fetched place details for: {PlaceName}", place.Name);

                // Step 2: Generate AI summary using the existing service method
                var aiSummary = await _aiService.SummarizePlaceAsync(place, cancellationToken);

                if (aiSummary == null || string.IsNullOrWhiteSpace(aiSummary.Summary))
                {
                    throw new InvalidOperationException("Failed to generate AI summary");
                }

                // Step 3: Map AI summary to result
                var result = new PlaceSummaryResult
                {
                    PlaceId = place.PlaceId,
                    PlaceName = place.Name,
                    Summary = aiSummary.Summary,
                    Highlights = aiSummary.Highlights,
                    BestTimeToVisit = aiSummary.RecommendedTime,
                    BestFor = aiSummary.BestFor,
                    UsedAI = true,
                    AIProvider = _aiService.ProviderName,
                    PlaceData = new Dictionary<string, object>
                    {
                        ["rating"] = place.Rating ?? 0.0,
                        ["address"] = place.Address ?? "",
                        ["types"] = place.Types ?? new List<string>(),
                        ["sentimentScore"] = aiSummary.SentimentScore
                    }
                };

                // Adjust based on requested style
                if (request.Style.ToLower() == "highlights")
                {
                    // For highlights style, emphasize the highlights list
                    if (!result.Highlights.Any())
                    {
                        // If no highlights, extract from summary
                        result.Highlights.Add(result.Summary);
                    }
                }
                else if (request.Style.ToLower() == "detailed")
                {
                    // For detailed style, add estimated duration based on place type
                    result.RecommendedDuration = EstimateVisitDuration(place);
                }

                // Step 4: Cache the result (24 hours)
                await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromHours(24));

                _logger.LogInformation("Successfully generated AI summary for place: {PlaceName}", place.Name);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating place summary for: {PlaceId}", request.PlaceId);
                throw;
            }
        }

        /// <summary>
        /// Estimates visit duration based on place type
        /// </summary>
        private string EstimateVisitDuration(DTOs.Response.PlaceDto place)
        {
            if (place.Types == null || !place.Types.Any())
            {
                return "1-2 hours";
            }

            var types = place.Types.Select(t => t.ToLower()).ToList();

            if (types.Any(t => t.Contains("museum") || t.Contains("gallery") || t.Contains("zoo") || t.Contains("aquarium")))
            {
                return "2-3 hours";
            }

            if (types.Any(t => t.Contains("park") || t.Contains("garden")))
            {
                return "1-2 hours";
            }

            if (types.Any(t => t.Contains("restaurant") || t.Contains("cafe") || t.Contains("bar")))
            {
                return "1-1.5 hours";
            }

            if (types.Any(t => t.Contains("shopping") || t.Contains("mall")))
            {
                return "1-3 hours";
            }

            if (types.Any(t => t.Contains("church") || t.Contains("temple") || t.Contains("mosque") || t.Contains("synagogue")))
            {
                return "30 minutes - 1 hour";
            }

            return "1-2 hours";
        }
    }
}
