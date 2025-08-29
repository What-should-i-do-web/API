using WhatShouldIDo.Domain.Entities;
using WhatShouldIDo.Infrastructure.Options;
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

    public async Task<List<Place>> GetNearbyPlacesAsync(float lat, float lng, int radius, string keyword = null)
    {
        try
        {
            var kinds = string.Join(",", _options.Kinds);
            var url = $"{_options.BaseUrl}/0.1/en/places/radius" +
                      $"?lat={lat}&lon={lng}&radius={radius}" +
                      $"&kinds={kinds}&limit=50&apikey={_options.ApiKey}";

            _logger.LogInformation("[OTM] Calling OpenTripMap API: {url}", url);

            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("[OTM] OpenTripMap API failed with status {statusCode}: {errorContent}", 
                    response.StatusCode, errorContent);
                
                // Check if it's an API key issue
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden || 
                    response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("[OTM] OpenTripMap API key appears to be invalid or expired. Returning empty results.");
                }
                
                return new List<Place>();
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<OtmResponse>(json);

            var places = data?.Features?.Select(MapToPlace).ToList() ?? new List<Place>();
            _logger.LogInformation("[OTM] OpenTripMap returned {count} places", places.Count);
            
            return places;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[OTM] Network error calling OpenTripMap API. Returning empty results.");
            return new List<Place>();
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "[OTM] OpenTripMap API call timed out. Returning empty results.");
            return new List<Place>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OTM] Unexpected error calling OpenTripMap API. Returning empty results.");
            return new List<Place>();
        }
    }

    public async Task<List<Place>> SearchByPromptAsync(string textQuery, float lat, float lng, string[] priceLevels = null)
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
