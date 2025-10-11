using WhatShouldIDo.Domain.Entities;
using WhatShouldIDo.Infrastructure.Options;
using WhatShouldIDo.Application.Common;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace WhatShouldIDo.Infrastructure.Services;

public class OpenTripMapProvider
{
    private readonly HttpClient _httpClient;
    private readonly OpenTripMapOptions _options;
    private readonly ILogger<OpenTripMapProvider> _logger;

    public OpenTripMapProvider(HttpClient httpClient, IOptions<OpenTripMapOptions> options, ILogger<OpenTripMapProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ProviderResult<List<Place>>> GetNearbyPlacesAsync(float lat, float lng, int radius, string keyword = null)
    {
        // Check if API key is configured
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || _options.ApiKey.StartsWith("${"))
        {
            _logger.LogWarning("[OTM] API key not configured (missing or placeholder). SkippedReason: NoApiKey");
            return ProviderResult<List<Place>>.ApiKeyInvalid("OpenTripMap");
        }

        try
        {
            var kinds = string.Join(",", _options.Kinds);
            var url = $"{_options.BaseUrl}/0.1/en/places/radius" +
                      $"?lat={lat}&lon={lng}&radius={radius}" +
                      $"&kinds={kinds}&limit=50&apikey={_options.ApiKey}";

            _logger.LogInformation("[OTM] Calling OpenTripMap API - lat:{lat}, lng:{lng}, radius:{radius}", lat, lng, radius);

            var response = await _httpClient.GetAsync(url);
            var httpStatus = (int)response.StatusCode;

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("[OTM] HTTP {httpStatus}: {errorContent} | SkippedReason: HttpError", httpStatus, errorContent);

                // Check if it's an API key issue
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
                    response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("[OTM] API key invalid or expired. SkippedReason: ApiKeyInvalid");
                    return ProviderResult<List<Place>>.ApiKeyInvalid("OpenTripMap", httpStatus);
                }

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("[OTM] Rate limit exceeded (429). SkippedReason: RateLimited");
                    return ProviderResult<List<Place>>.RateLimited("OpenTripMap", "HTTP 429 - Too Many Requests");
                }

                return ProviderResult<List<Place>>.Error("OpenTripMap", $"HTTP {httpStatus}: {errorContent}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<OtmResponse>(json);

            var places = data?.Features?.Select(MapToPlace).ToList() ?? new List<Place>();

            _logger.LogInformation(
                "[OTM] Provider call completed | Status: Success | Count: {count} | HTTP: {httpStatus} | Lat: {lat} | Lng: {lng} | Radius: {radius}",
                places.Count, httpStatus, lat, lng, radius);

            if (places.Count == 0)
            {
                return ProviderResult<List<Place>>.NoResults("OpenTripMap");
            }

            return ProviderResult<List<Place>>.Success(places, places.Count, "OpenTripMap");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "[OTM] Request timed out after {timeout}ms. SkippedReason: Timeout", _options.TimeoutMs);
            return ProviderResult<List<Place>>.Timeout("OpenTripMap", $"Timeout after {_options.TimeoutMs}ms");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[OTM] Network error. SkippedReason: NetworkError");
            return ProviderResult<List<Place>>.NetworkError("OpenTripMap", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OTM] Unexpected error. SkippedReason: Exception");
            return ProviderResult<List<Place>>.Error("OpenTripMap", ex.Message);
        }
    }

    public async Task<ProviderResult<List<Place>>> SearchByPromptAsync(string textQuery, float lat, float lng, string[] priceLevels = null)
    {
        return await GetNearbyPlacesAsync(lat, lng, 5000, textQuery);
    }

    private static Place MapToPlace(OtmFeature feature)
    {
        var props = feature.Properties;
        var coords = feature.Geometry?.Coordinates;
        
        return new Place
        {
            Id = Guid.NewGuid(),
            Name = props?.Name ?? "Unknown Place",
            Latitude = (float)(coords?[1] ?? 0),
            Longitude = (float)(coords?[0] ?? 0),
            Rating = props?.Rate.ToString() ?? "0",
            Category = props?.Kinds?.Split(',').FirstOrDefault() ?? "tourist_attraction",
            GooglePlaceId = feature.Properties?.Xid ?? "",
            Address = "",
            GoogleMapsUrl = "",
            CachedAt = DateTime.UtcNow,
            Source = "OpenTripMap"
        };
    }

    private class OtmResponse
    {
        public OtmFeature[]? Features { get; set; }
    }

    private class OtmFeature
    {
        public OtmProperties? Properties { get; set; }
        public OtmGeometry? Geometry { get; set; }
    }

    private class OtmProperties
    {
        public string? Xid { get; set; }
        public string? Name { get; set; }
        public string? Kinds { get; set; }
        public double Rate { get; set; }
    }

    private class OtmGeometry
    {
        public double[]? Coordinates { get; set; }
    }
}
