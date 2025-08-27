using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Text;
using System.Text.Json;
using WhatShouldIDo.Application.Common;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Domain.Entities;
using WhatShouldIDo.Infrastructure.Caching;

public class GooglePlacesProvider : IPlacesProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<GooglePlacesProvider> _logger;
    private readonly ICacheService _cache;
    private readonly IConfiguration _configuration;
    private readonly string Base_URL_Places;
    private readonly string Base_URL_Text;

    public GooglePlacesProvider(HttpClient httpClient, IConfiguration configuration, ILogger<GooglePlacesProvider> logger, ICacheService cache)
    {
        _httpClient = httpClient;
        _apiKey = configuration["GooglePlaces:ApiKey"];
        _logger = logger;
        _configuration = configuration;
        _cache = cache;
        Base_URL_Places = configuration["GooglePlaces:NearbyPlacesUrl"];
        Base_URL_Text = configuration["GooglePlaces:PlacesTextUrl"];
    }

    public async Task<List<Place>> GetNearbyPlacesAsync(float lat, float lng, int radius, string keyword = null)
    {
        var cacheKey = CacheKeyBuilder.Nearby(lat, lng, radius, keyword);
        var ttl = TimeSpan.FromMinutes(int.Parse(_configuration["CacheOptions:NearbyTtlMinutes"] ?? "30"));

        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            _logger.LogInformation("Google Places API çağrısı yapılıyor (nearby).");

            var requestBody = new
            {
                includedTypes = string.IsNullOrWhiteSpace(keyword) 
                    ? new[] { "restaurant" }
                    : keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => t.ToLower()).ToArray(),
                maxResultCount = 10,
                locationRestriction = new
                {
                    circle = new
                    {
                        center = new { latitude = lat, longitude = lng },
                        radius = radius
                    }
                }
            };

            var requestJson = JsonSerializer.Serialize(requestBody);
            _logger.LogInformation("🔍 Request to Google Places API: {requestBody}", requestJson);
            
            var request = new HttpRequestMessage(HttpMethod.Post, Base_URL_Places);
            request.Headers.Add("X-Goog-Api-Key", _apiKey);
            request.Headers.Add("X-Goog-FieldMask", "places.displayName,places.location,places.rating,places.types,places.photos");
            request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("❌ Google Places API error: {statusCode} - {body}", response.StatusCode, json);
            }

            response.EnsureSuccessStatusCode();

            var root = JsonDocument.Parse(json).RootElement;
            if (!root.TryGetProperty("places", out var results))
            {
                _logger.LogWarning("❌ Google yanıtı boş.");
                return new List<Place>();
            }

            var places = new List<Place>();
            foreach (var item in results.EnumerateArray())
            {
                var name = item.GetProperty("displayName").GetProperty("text").GetString();
                var location = item.GetProperty("location");

                var place = new Place
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Latitude = location.GetProperty("latitude").GetSingle(),
                    Longitude = location.GetProperty("longitude").GetSingle(),
                    Category = item.TryGetProperty("types", out var types) ? string.Join(",", types.EnumerateArray().Select(t => t.GetString())) : null,
                    Source = "Google",
                    CachedAt = DateTime.UtcNow
                };

                // Debug: Log the photos structure
                if (item.TryGetProperty("photos", out var debugPhotos))
                {
                    _logger.LogInformation($"🔍 Photos structure for {place.Name}: {debugPhotos.GetRawText()}");
                }
                
                // Enrich with photo
                place = EnrichWithPhoto(place, item);
                places.Add(place);
            }

            return places;
        }, ttl);
    }

    public async Task<List<Place>> SearchByPromptAsync(string textQuery, float lat, float lng, string[] priceLevels = null)
    {
        var cacheKey = CacheKeyBuilder.Prompt(textQuery, lat, lng);
        var ttl = TimeSpan.FromMinutes(int.Parse(_configuration["CacheOptions:PromptTtlMinutes"] ?? "15"));

        return await _cache.GetOrSetAsync(cacheKey, async () =>
        {
            _logger.LogInformation("Google Text Search API çağrısı başlatılıyor → Query: {query}, Lat: {lat}, Lng: {lng}", textQuery, lat, lng);

            string url = Base_URL_Text;

            var requestBody = new Dictionary<string, object>
            {
                ["textQuery"] = textQuery,
                ["maxResultCount"] = 10,
                ["locationBias"] = new
                {
                    circle = new
                    {
                        center = new { latitude = lat, longitude = lng },
                        radius = 3000
                    }
                }
            };

            if (priceLevels is { Length: > 0 })
            {
                requestBody["priceLevels"] = priceLevels;
            }

            var json = JsonSerializer.Serialize(requestBody);
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("X-Goog-Api-Key", _apiKey);
            request.Headers.Add("X-Goog-FieldMask", "places.displayName,places.formattedAddress,places.priceLevel,places.rating,places.types,places.location,places.photos");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("❌ Google Text Search failed: {status} - {body}", response.StatusCode, responseJson);
                return new List<Place>();
            }

            var result = JsonDocument.Parse(responseJson).RootElement;
            if (!result.TryGetProperty("places", out var placesJson))
            {
                _logger.LogWarning("⚠️ Google yanıtında 'places' yok.");
                return new List<Place>();
            }

            var places = new List<Place>();
            foreach (var item in placesJson.EnumerateArray())
            {
                var location = item.GetProperty("location");
                var place = new Place
                {
                    Id = Guid.NewGuid(),
                    Name = item.GetProperty("displayName").GetProperty("text").GetString(),
                    Address = item.TryGetProperty("formattedAddress", out var addr) ? addr.GetString() : null,
                    Latitude = location.GetProperty("latitude").GetSingle(),
                    Longitude = location.GetProperty("longitude").GetSingle(),
                    Rating = item.TryGetProperty("rating", out var r) ? r.ToString() : null,
                    PriceLevel = item.TryGetProperty("priceLevel", out var p) ? p.ToString() : null,
                    Category = item.TryGetProperty("types", out var types) ? string.Join(",", types.EnumerateArray().Select(t => t.GetString())) : null,
                    Source = "Google",
                    CachedAt = DateTime.UtcNow
                };

                // Debug: Log the photos structure
                if (item.TryGetProperty("photos", out var debugPhotos))
                {
                    _logger.LogInformation($"🔍 Photos structure for {place.Name}: {debugPhotos.GetRawText()}");
                }
                
                // Enrich with photo
                place = EnrichWithPhoto(place, item);
                places.Add(place);
            }

            return places;

        }, ttl);
    }

    public string? GetPlacePhotoUrl(string photoName, int maxWidth = 400)
    {
        if (string.IsNullOrEmpty(photoName))
            return null;

        // New Google Places API format: use the photo name directly with maxHeightPx parameter
        return $"https://places.googleapis.com/v1/{photoName}/media" +
               $"?key={_apiKey}" +
               $"&maxHeightPx={maxWidth}" +
               $"&maxWidthPx={maxWidth}";
    }

    private Place EnrichWithPhoto(Place place, JsonElement item)
    {
        if (item.TryGetProperty("photos", out var photos) && photos.GetArrayLength() > 0)
        {
            var firstPhoto = photos[0];
            
            // Try new Google Places API format first (name field)
            if (firstPhoto.TryGetProperty("name", out var photoNameElement))
            {
                var photoName = photoNameElement.GetString();
                if (!string.IsNullOrEmpty(photoName))
                {
                    // Store the photo name as reference for potential caching
                    place.PhotoReference = photoName.Split('/').LastOrDefault(); // Extract just the photo ID for reference
                    place.PhotoUrl = GetPlacePhotoUrl(photoName, 400);
                    _logger.LogInformation($"📸 Photo found (new format) for {place.Name}: {photoName}");
                    return place;
                }
            }
            
            // Fallback to legacy format (photoReference field) for backward compatibility
            if (firstPhoto.TryGetProperty("photoReference", out var photoRefElement))
            {
                var photoReference = photoRefElement.GetString();
                if (!string.IsNullOrEmpty(photoReference))
                {
                    place.PhotoReference = photoReference;
                    // Use legacy photo URL format as fallback
                    place.PhotoUrl = $"https://maps.googleapis.com/maps/api/place/photo?maxwidth=400&photoreference={photoReference}&key={_apiKey}";
                    _logger.LogInformation($"📸 Photo found (legacy format) for {place.Name}: {photoReference[..Math.Min(8, photoReference.Length)]}...");
                    return place;
                }
            }
            
            _logger.LogWarning($"📸 Photo structure not recognized for {place.Name}: {firstPhoto.GetRawText()}");
        }
        else
        {
            _logger.LogInformation($"📸 No photos available for {place.Name}");
        }
        return place;
    }

}
