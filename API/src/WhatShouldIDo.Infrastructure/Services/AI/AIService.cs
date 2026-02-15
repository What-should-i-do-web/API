using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using WhatShouldIDo.Application.DTOs.AI;
using WhatShouldIDo.Application.DTOs.Prompt;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Infrastructure.Options;

namespace WhatShouldIDo.Infrastructure.Services.AI
{
    /// <summary>
    /// Main AI service implementation with provider-agnostic architecture
    /// </summary>
    public class AIService : IAIService
    {
        private readonly IAIProvider _primaryProvider;
        private readonly IAIProvider? _fallbackProvider;
        private readonly AIOptions _options;
        private readonly ILogger<AIService> _logger;
        private readonly ICacheService? _cacheService;

        public string ProviderName => _primaryProvider.Name;

        public AIService(
            IAIProvider primaryProvider,
            IOptions<AIOptions> options,
            ILogger<AIService> logger,
            ICacheService? cacheService = null,
            IAIProvider? fallbackProvider = null)
        {
            _primaryProvider = primaryProvider ?? throw new ArgumentNullException(nameof(primaryProvider));
            _fallbackProvider = fallbackProvider;
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cacheService = cacheService;
        }

        public async Task<InterpretedPrompt> InterpretPromptAsync(string promptText, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(promptText))
            {
                throw new ArgumentException("Prompt text cannot be empty", nameof(promptText));
            }

            var cacheKey = $"ai:interpret:{promptText.GetHashCode()}";

            // Try cache first
            if (_options.EnableCaching && _cacheService != null)
            {
                var cached = await _cacheService.GetAsync<InterpretedPrompt>(cacheKey);
                if (cached != null)
                {
                    _logger.LogDebug("Cache hit for prompt interpretation: {Prompt}", promptText);
                    return cached;
                }
            }

            try
            {
                var systemMessage = @"You are an expert at interpreting natural language search queries for places and activities.
Extract structured information from the user's prompt. Return a JSON object with the following structure:
{
  ""textQuery"": ""extracted search keywords"",
  ""locationText"": ""location name if mentioned, else null"",
  ""pricePreferences"": [""PRICE_LEVEL_INEXPENSIVE"", ""PRICE_LEVEL_MODERATE"", ""PRICE_LEVEL_EXPENSIVE"", ""PRICE_LEVEL_VERY_EXPENSIVE""],
  ""categories"": [""restaurant"", ""cafe"", ""bar"", ""museum"", etc],
  ""dietaryRestrictions"": [""vegan"", ""vegetarian"", ""gluten-free"", ""halal"", etc],
  ""timeContext"": ""breakfast"" | ""lunch"" | ""dinner"" | ""evening"" | ""morning"" | null,
  ""atmosphere"": [""romantic"", ""casual"", ""quiet"", ""lively"", etc],
  ""activityTypes"": [""dining"", ""sightseeing"", ""shopping"", ""entertainment"", etc],
  ""tags"": [""additional keywords""],
  ""suggestedRadius"": 5000,
  ""confidence"": 0.9,
  ""aiReasoning"": ""brief explanation""
}

Be comprehensive but accurate. If something is not mentioned, use null or empty arrays.";

                var prompt = $"Interpret this search query: \"{promptText}\"";

                var jsonResponse = await ExecuteWithFallbackAsync(
                    p => p.CompleteJsonAsync(prompt, systemMessage, 0.3, cancellationToken),
                    "InterpretPrompt");

                var interpreted = JsonSerializer.Deserialize<InterpretedPrompt>(jsonResponse, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new InterpretedPrompt();

                interpreted.OriginalPrompt = promptText;
                interpreted.TextQuery = string.IsNullOrEmpty(interpreted.TextQuery) ? promptText : interpreted.TextQuery;

                // Cache the result
                if (_options.EnableCaching && _cacheService != null)
                {
                    await _cacheService.SetAsync(cacheKey, interpreted, TimeSpan.FromMinutes(_options.CacheTTLMinutes));
                }

                _logger.LogInformation("Prompt interpreted successfully: {Prompt} -> {Categories}",
                    promptText, string.Join(", ", interpreted.Categories));

                return interpreted;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to interpret prompt: {Prompt}", promptText);

                // Return basic interpretation as fallback
                return new InterpretedPrompt
                {
                    OriginalPrompt = promptText,
                    TextQuery = promptText,
                    Confidence = 0.5,
                    AIReasoning = "AI interpretation failed, using direct query"
                };
            }
        }

        public async Task<List<PlaceDto>> RankPlacesByRelevanceAsync(
            string originalQuery,
            List<PlaceDto> places,
            CancellationToken cancellationToken = default)
        {
            if (places == null || places.Count == 0)
            {
                return places ?? new List<PlaceDto>();
            }

            try
            {
                // Generate embeddings for the query
                var queryEmbedding = await ExecuteWithFallbackAsync(
                    p => p.GenerateEmbeddingAsync(originalQuery, cancellationToken),
                    "GenerateQueryEmbedding");

                // Generate embeddings for each place (using name + description)
                var placeEmbeddings = new List<(PlaceDto place, float[] embedding, double similarity)>();

                foreach (var place in places)
                {
                    var placeText = $"{place.Name} {place.Description ?? ""} {string.Join(" ", place.Types ?? new List<string>())}";
                    var embedding = await ExecuteWithFallbackAsync(
                        p => p.GenerateEmbeddingAsync(placeText, cancellationToken),
                        "GeneratePlaceEmbedding");

                    var similarity = CosineSimilarity(queryEmbedding, embedding);
                    placeEmbeddings.Add((place, embedding, similarity));
                }

                // Sort by similarity descending
                var ranked = placeEmbeddings
                    .OrderByDescending(x => x.similarity)
                    .Select(x => x.place)
                    .ToList();

                _logger.LogInformation("Ranked {Count} places by relevance for query: {Query}", places.Count, originalQuery);

                return ranked;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rank places by relevance, returning original order");
                return places;
            }
        }

        public async Task<PlaceSummary> SummarizePlaceAsync(PlaceDto place, CancellationToken cancellationToken = default)
        {
            if (place == null)
            {
                throw new ArgumentNullException(nameof(place));
            }

            var cacheKey = $"ai:summary:{place.PlaceId}";

            // Try cache first
            if (_options.EnableCaching && _cacheService != null)
            {
                var cached = await _cacheService.GetAsync<PlaceSummary>(cacheKey);
                if (cached != null)
                {
                    return cached;
                }
            }

            try
            {
                var systemMessage = @"You are an expert at summarizing places and attractions.
Create a concise, informative summary that highlights key features and helps travelers decide if this place is right for them.
Return JSON with this structure:
{
  ""summary"": ""1-3 sentence overview"",
  ""highlights"": [""key feature 1"", ""key feature 2"", etc],
  ""sentimentScore"": 0.0 to 1.0,
  ""bestFor"": [""families"", ""couples"", ""solo travelers"", etc],
  ""recommendedTime"": ""best time to visit""
}";

                var prompt = $@"Summarize this place:
Name: {place.Name}
Types: {string.Join(", ", place.Types ?? new List<string>())}
Rating: {place.Rating ?? 0}
Description: {place.Description ?? "No description available"}
Address: {place.Address ?? ""}";

                var jsonResponse = await ExecuteWithFallbackAsync(
                    p => p.CompleteJsonAsync(prompt, systemMessage, 0.5, cancellationToken),
                    "SummarizePlace");

                var summary = JsonSerializer.Deserialize<PlaceSummary>(jsonResponse, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new PlaceSummary();

                summary.PlaceId = place.PlaceId;

                // Cache the result
                if (_options.EnableCaching && _cacheService != null)
                {
                    await _cacheService.SetAsync(cacheKey, summary, TimeSpan.FromHours(24));
                }

                _logger.LogInformation("Summarized place: {PlaceName}", place.Name);

                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to summarize place: {PlaceName}", place.Name);

                return new PlaceSummary
                {
                    PlaceId = place.PlaceId,
                    Summary = place.Description ?? $"A {string.Join(", ", place.Types ?? new List<string>())} in the area.",
                    SentimentScore = (place.Rating ?? 3.0) / 5.0,
                    Highlights = new List<string>()
                };
            }
        }

        public async Task<AIItinerary> GenerateDailyItineraryAsync(
            AIItineraryRequest request,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Generating AI-driven daily itinerary for location: {Location}", request.Location);

            try
            {
                // Build the context for the AI prompt
                var totalHours = (request.EndTime - request.StartTime).TotalHours;
                var activitiesText = request.PreferredActivities.Any()
                    ? string.Join(", ", request.PreferredActivities)
                    : "various activities";

                var dietaryText = request.DietaryPreferences.Any()
                    ? $" with {string.Join(", ", request.DietaryPreferences)} dietary preferences"
                    : "";

                var systemMessage = $@"You are a travel planning assistant. Generate a realistic daily itinerary in JSON format.

Requirements:
- Location: {request.Location} ({request.Latitude}, {request.Longitude})
- Duration: {request.StartTime:hh\:mm} to {request.EndTime:hh\:mm} ({totalHours} hours)
- Budget: {request.BudgetLevel}
- Preferred activities: {activitiesText}
- Transportation: {request.TransportationMode}
- Maximum stops: {request.MaxStops}
{(string.IsNullOrEmpty(dietaryText) ? "" : "- Dietary: " + dietaryText)}
{(string.IsNullOrEmpty(request.AdditionalPreferences) ? "" : "- Additional: " + request.AdditionalPreferences)}

Generate a JSON object with this exact structure:
{{
  ""title"": ""A catchy title for the day"",
  ""description"": ""Brief overview of the day's plan"",
  ""stops"": [
    {{
      ""order"": 1,
      ""placeName"": ""Name of the place"",
      ""latitude"": 41.0082,
      ""longitude"": 28.9784,
      ""arrivalTime"": ""09:00"",
      ""durationMinutes"": 90,
      ""activityType"": ""breakfast"",
      ""reason"": ""Why this place fits the itinerary"",
      ""categories"": [""cafe"", ""restaurant""]
    }}
  ],
  ""reasoning"": ""Overall rationale for this itinerary"",
  ""totalDurationMinutes"": 600,
  ""estimatedCost"": ""$50-$100""
}}

Important:
- Include breakfast, lunch, and/or dinner based on time range
- Balance activity types (don't have 6 museums in a row)
- Keep stops within {request.RadiusMeters}m of the center point
- Account for travel time between stops (walking: 5 min/km, driving: 2 min/km)
- Each stop should be 30-120 minutes
- Activity types: breakfast, lunch, dinner, coffee_break, sightseeing, shopping, entertainment, relaxation";

                var userMessage = $@"Create a {totalHours}-hour itinerary for {request.Location} starting at {request.StartTime:hh\:mm}.
Focus on {activitiesText}{dietaryText}.
Budget level: {request.BudgetLevel}.
{(string.IsNullOrEmpty(request.AdditionalPreferences) ? "" : $"Special request: {request.AdditionalPreferences}")}";

                // Call AI provider with JSON mode
                var jsonResponse = await ExecuteWithFallbackAsync(
                    p => p.CompleteJsonAsync(userMessage, systemMessage, 0.7, cancellationToken),
                    "GenerateDailyItinerary");

                // Parse the JSON response
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var aiResponse = JsonSerializer.Deserialize<AIItineraryResponse>(jsonResponse, options);

                if (aiResponse == null || aiResponse.Stops == null || !aiResponse.Stops.Any())
                {
                    _logger.LogWarning("AI returned empty or invalid itinerary");
                    throw new InvalidOperationException("Failed to generate itinerary - AI returned invalid response");
                }

                // Convert to domain model
                var itinerary = new AIItinerary
                {
                    Id = Guid.NewGuid(),
                    Title = aiResponse.Title ?? "Your Day Plan",
                    Description = aiResponse.Description ?? "",
                    Date = request.TargetDate ?? DateTime.Today,
                    TransportationMode = request.TransportationMode,
                    Reasoning = aiResponse.Reasoning ?? "",
                    TotalDurationMinutes = aiResponse.TotalDurationMinutes,
                    EstimatedCost = aiResponse.EstimatedCost,
                    GeneratedAt = DateTime.UtcNow,
                    Stops = new List<ItineraryStop>()
                };

                // Convert stops
                int previousDistance = 0;
                TimeSpan previousArrival = request.StartTime;

                foreach (var stop in aiResponse.Stops.OrderBy(s => s.Order))
                {
                    var arrivalTime = TimeSpan.TryParse(stop.ArrivalTime, out var parsed)
                        ? parsed
                        : previousArrival.Add(TimeSpan.FromMinutes(stop.DurationMinutes + 15));

                    // Calculate travel time from previous stop (rough estimate)
                    int? travelTime = null;
                    if (itinerary.Stops.Any())
                    {
                        var minutes = (arrivalTime - previousArrival).TotalMinutes - itinerary.Stops.Last().DurationMinutes;
                        travelTime = (int)Math.Max(0, minutes);
                    }

                    itinerary.Stops.Add(new ItineraryStop
                    {
                        Order = stop.Order,
                        Place = new PlaceDto
                        {
                            PlaceId = $"ai-generated-{Guid.NewGuid()}",
                            Name = stop.PlaceName,
                            Latitude = stop.Latitude,
                            Longitude = stop.Longitude,
                            Types = stop.Categories,
                            Source = "AI-Generated"
                        },
                        ArrivalTime = arrivalTime,
                        DurationMinutes = stop.DurationMinutes,
                        ActivityType = stop.ActivityType,
                        Reason = stop.Reason,
                        TravelTimeFromPrevious = travelTime,
                        DistanceFromPrevious = previousDistance
                    });

                    previousArrival = arrivalTime;
                }

                // Calculate total distance (rough estimate based on radius)
                itinerary.TotalDistanceMeters = itinerary.Stops.Count * (request.RadiusMeters / 2);

                _logger.LogInformation("Successfully generated itinerary with {StopCount} stops for {Location}",
                    itinerary.Stops.Count, request.Location);

                return itinerary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate daily itinerary for {Location}", request.Location);
                throw;
            }
        }

        // Helper class for JSON deserialization
        private class AIItineraryResponse
        {
            public string? Title { get; set; }
            public string? Description { get; set; }
            public List<AIStopResponse> Stops { get; set; } = new();
            public string? Reasoning { get; set; }
            public int TotalDurationMinutes { get; set; }
            public string? EstimatedCost { get; set; }
        }

        private class AIStopResponse
        {
            public int Order { get; set; }
            public string PlaceName { get; set; } = string.Empty;
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public string ArrivalTime { get; set; } = string.Empty;
            public int DurationMinutes { get; set; }
            public string ActivityType { get; set; } = string.Empty;
            public string Reason { get; set; } = string.Empty;
            public List<string> Categories { get; set; } = new();
        }

        public async Task<Dictionary<string, string>> ExtractStructuredDataAsync(
            string text,
            string[] fields,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text) || fields == null || fields.Length == 0)
            {
                return new Dictionary<string, string>();
            }

            try
            {
                var systemMessage = $@"Extract the following fields from the text: {string.Join(", ", fields)}.
Return a JSON object with these exact field names as keys and extracted values.
If a field cannot be extracted, use an empty string.";

                var jsonResponse = await ExecuteWithFallbackAsync(
                    p => p.CompleteJsonAsync(text, systemMessage, 0.3, cancellationToken),
                    "ExtractStructuredData");

                var result = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonResponse, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new Dictionary<string, string>();

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract structured data from text");
                return new Dictionary<string, string>();
            }
        }

        public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Text cannot be empty", nameof(text));
            }

            var cacheKey = $"ai:embedding:{text.GetHashCode()}";

            // Try cache first
            if (_options.EnableCaching && _cacheService != null)
            {
                try
                {
                    var cached = await _cacheService.GetAsync<float[]>(cacheKey);
                    if (cached != null && cached.Length > 0)
                    {
                        _logger.LogDebug("Cache hit for embedding: {TextLength} chars", text.Length);
                        return cached;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get embedding from cache");
                }
            }

            try
            {
                var embedding = await ExecuteWithFallbackAsync(
                    p => p.GenerateEmbeddingAsync(text, cancellationToken),
                    "GenerateEmbedding");

                // Cache the result
                if (_options.EnableCaching && _cacheService != null && embedding != null && embedding.Length > 0)
                {
                    try
                    {
                        // Embeddings are relatively stable, cache for longer
                        await _cacheService.SetAsync(cacheKey, embedding, TimeSpan.FromHours(24));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cache embedding");
                    }
                }

                _logger.LogDebug("Generated embedding: {TextLength} chars -> {Dimension} dimensions",
                    text.Length, embedding?.Length ?? 0);

                return embedding ?? Array.Empty<float>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate embedding for text: {TextLength} chars", text.Length);
                throw;
            }
        }

        public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _primaryProvider.IsHealthyAsync(cancellationToken);
            }
            catch
            {
                if (_fallbackProvider != null)
                {
                    try
                    {
                        return await _fallbackProvider.IsHealthyAsync(cancellationToken);
                    }
                    catch
                    {
                        return false;
                    }
                }
                return false;
            }
        }

        // Helper method to execute with fallback
        private async Task<T> ExecuteWithFallbackAsync<T>(
            Func<IAIProvider, Task<T>> operation,
            string operationName)
        {
            try
            {
                return await operation(_primaryProvider);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Primary provider failed for {Operation}, trying fallback", operationName);

                if (_fallbackProvider != null)
                {
                    try
                    {
                        return await operation(_fallbackProvider);
                    }
                    catch (Exception fallbackEx)
                    {
                        _logger.LogError(fallbackEx, "Fallback provider also failed for {Operation}", operationName);
                        throw;
                    }
                }

                throw;
            }
        }

        // Cosine similarity calculation
        private static double CosineSimilarity(float[] vector1, float[] vector2)
        {
            if (vector1.Length != vector2.Length)
            {
                throw new ArgumentException("Vectors must have the same length");
            }

            double dotProduct = 0;
            double magnitude1 = 0;
            double magnitude2 = 0;

            for (int i = 0; i < vector1.Length; i++)
            {
                dotProduct += vector1[i] * vector2[i];
                magnitude1 += vector1[i] * vector1[i];
                magnitude2 += vector2[i] * vector2[i];
            }

            magnitude1 = Math.Sqrt(magnitude1);
            magnitude2 = Math.Sqrt(magnitude2);

            if (magnitude1 == 0 || magnitude2 == 0)
            {
                return 0;
            }

            return dotProduct / (magnitude1 * magnitude2);
        }
    }
}
