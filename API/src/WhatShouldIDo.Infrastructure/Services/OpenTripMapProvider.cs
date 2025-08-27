using WhatShouldIDo.Domain.Entities;
using WhatShouldIDo.Infrastructure.Options;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace WhatShouldIDo.Infrastructure.Services;

public class OpenTripMapProvider
{
    private readonly HttpClient _httpClient;
    private readonly OpenTripMapOptions _options;

    public OpenTripMapProvider(HttpClient httpClient, IOptions<OpenTripMapOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<List<Place>> GetNearbyPlacesAsync(float lat, float lng, int radius, string keyword = null)
    {
        var kinds = string.Join(",", _options.Kinds);
        var url = $"{_options.BaseUrl}/0.1/en/places/radius" +
                  $"?lat={lat}&lon={lng}&radius={radius}" +
                  $"&kinds={kinds}&limit=50&apikey={_options.ApiKey}";

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<OtmResponse>(json);

        return data?.Features?.Select(MapToPlace).ToList() ?? new List<Place>();
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
