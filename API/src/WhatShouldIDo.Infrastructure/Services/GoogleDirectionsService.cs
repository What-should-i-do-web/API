using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Infrastructure.Options;

namespace WhatShouldIDo.Infrastructure.Services
{
    /// <summary>
    /// Google Directions API implementation
    /// </summary>
    public class GoogleDirectionsService : IDirectionsService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly ILogger<GoogleDirectionsService> _logger;
        private readonly ICacheService? _cacheService;
        private const string BaseUrl = "https://maps.googleapis.com/maps/api/";

        public GoogleDirectionsService(
            HttpClient httpClient,
            IOptions<GoogleMapsOptions> options,
            ILogger<GoogleDirectionsService> logger,
            ICacheService? cacheService = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiKey = options?.Value?.ApiKey ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cacheService = cacheService;

            _httpClient.BaseAddress = new Uri(BaseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
        }

        public async Task<DirectionsResult> GetDirectionsAsync(
            double originLat,
            double originLng,
            double destLat,
            double destLng,
            string mode = "driving",
            CancellationToken cancellationToken = default)
        {
            try
            {
                var cacheKey = $"directions:{originLat:F4},{originLng:F4}:{destLat:F4},{destLng:F4}:{mode}";

                // Try cache first
                if (_cacheService != null)
                {
                    var cached = await _cacheService.GetAsync<DirectionsResult>(cacheKey);
                    if (cached != null)
                    {
                        _logger.LogDebug("Cache hit for directions");
                        return cached;
                    }
                }

                var url = $"directions/json?origin={originLat},{originLng}&destination={destLat},{destLng}&mode={mode}&key={_apiKey}";
                var response = await _httpClient.GetAsync(url, cancellationToken);

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var apiResponse = JsonSerializer.Deserialize<GoogleDirectionsResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse == null || apiResponse.Status != "OK" || apiResponse.Routes == null || !apiResponse.Routes.Any())
                {
                    _logger.LogWarning("Google Directions API returned no routes. Status: {Status}", apiResponse?.Status);
                    throw new InvalidOperationException($"No route found. Status: {apiResponse?.Status}");
                }

                var route = apiResponse.Routes.First();
                var leg = route.Legs?.FirstOrDefault();

                if (leg == null)
                {
                    throw new InvalidOperationException("Route has no legs");
                }

                var result = new DirectionsResult
                {
                    DistanceMeters = leg.Distance?.Value ?? 0,
                    DurationSeconds = leg.Duration?.Value ?? 0,
                    DistanceText = leg.Distance?.Text ?? "",
                    DurationText = leg.Duration?.Text ?? "",
                    PolylineEncoded = route.OverviewPolyline?.Points,
                    Steps = leg.Steps?.Select(s => new DirectionsStep
                    {
                        DistanceMeters = s.Distance?.Value ?? 0,
                        DurationSeconds = s.Duration?.Value ?? 0,
                        Instructions = s.HtmlInstructions ?? "",
                        StartLat = s.StartLocation?.Lat ?? 0,
                        StartLng = s.StartLocation?.Lng ?? 0,
                        EndLat = s.EndLocation?.Lat ?? 0,
                        EndLng = s.EndLocation?.Lng ?? 0
                    }).ToList() ?? new List<DirectionsStep>()
                };

                // Cache for 30 minutes
                if (_cacheService != null)
                {
                    await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(30));
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get directions from Google");
                throw;
            }
        }

        public async Task<DistanceMatrix> GetDistanceMatrixAsync(
            List<(double lat, double lng)> origins,
            List<(double lat, double lng)> destinations,
            string mode = "driving",
            CancellationToken cancellationToken = default)
        {
            try
            {
                var originsStr = string.Join("|", origins.Select(o => $"{o.lat},{o.lng}"));
                var destinationsStr = string.Join("|", destinations.Select(d => $"{d.lat},{d.lng}"));

                var url = $"distancematrix/json?origins={originsStr}&destinations={destinationsStr}&mode={mode}&key={_apiKey}";

                var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var apiResponse = JsonSerializer.Deserialize<GoogleDistanceMatrixResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse == null || apiResponse.Status != "OK")
                {
                    _logger.LogWarning("Google Distance Matrix API error. Status: {Status}", apiResponse?.Status);
                    throw new InvalidOperationException($"Distance matrix request failed. Status: {apiResponse?.Status}");
                }

                var result = new DistanceMatrix
                {
                    OriginCount = origins.Count,
                    DestinationCount = destinations.Count,
                    Rows = apiResponse.Rows?.Select(r => new DistanceMatrixRow
                    {
                        Elements = r.Elements?.Select(e => new DistanceMatrixElement
                        {
                            DistanceMeters = e.Distance?.Value ?? 0,
                            DurationSeconds = e.Duration?.Value ?? 0,
                            Status = e.Status ?? "UNKNOWN"
                        }).ToList() ?? new List<DistanceMatrixElement>()
                    }).ToList() ?? new List<DistanceMatrixRow>()
                };

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get distance matrix from Google");
                throw;
            }
        }

        public async Task<TravelEstimate> EstimateTravelAsync(
            double originLat,
            double originLng,
            double destLat,
            double destLng,
            string mode = "driving",
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Use distance matrix for quick estimate
                var matrix = await GetDistanceMatrixAsync(
                    new List<(double, double)> { (originLat, originLng) },
                    new List<(double, double)> { (destLat, destLng) },
                    mode,
                    cancellationToken);

                if (matrix.Rows.Any() && matrix.Rows[0].Elements.Any())
                {
                    var element = matrix.Rows[0].Elements[0];
                    return new TravelEstimate
                    {
                        DistanceMeters = element.DistanceMeters,
                        DurationSeconds = element.DurationSeconds,
                        Mode = mode
                    };
                }

                throw new InvalidOperationException("Failed to estimate travel");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to estimate travel");
                throw;
            }
        }

        // Google API response models
        private class GoogleDirectionsResponse
        {
            public string? Status { get; set; }
            public List<GoogleRoute>? Routes { get; set; }
        }

        private class GoogleRoute
        {
            public List<GoogleLeg>? Legs { get; set; }
            public GooglePolyline? OverviewPolyline { get; set; }
        }

        private class GoogleLeg
        {
            public GoogleDistance? Distance { get; set; }
            public GoogleDuration? Duration { get; set; }
            public List<GoogleStep>? Steps { get; set; }
        }

        private class GoogleStep
        {
            public GoogleDistance? Distance { get; set; }
            public GoogleDuration? Duration { get; set; }
            public string? HtmlInstructions { get; set; }
            public GoogleLatLng? StartLocation { get; set; }
            public GoogleLatLng? EndLocation { get; set; }
        }

        private class GoogleDistance
        {
            public int Value { get; set; }
            public string? Text { get; set; }
        }

        private class GoogleDuration
        {
            public int Value { get; set; }
            public string? Text { get; set; }
        }

        private class GoogleLatLng
        {
            public double Lat { get; set; }
            public double Lng { get; set; }
        }

        private class GooglePolyline
        {
            public string? Points { get; set; }
        }

        private class GoogleDistanceMatrixResponse
        {
            public string? Status { get; set; }
            public List<GoogleMatrixRow>? Rows { get; set; }
        }

        private class GoogleMatrixRow
        {
            public List<GoogleMatrixElement>? Elements { get; set; }
        }

        private class GoogleMatrixElement
        {
            public string? Status { get; set; }
            public GoogleDistance? Distance { get; set; }
            public GoogleDuration? Duration { get; set; }
        }
    }

    /// <summary>
    /// Google Maps API options
    /// </summary>
    public class GoogleMapsOptions
    {
        public string ApiKey { get; set; } = string.Empty;
    }
}
