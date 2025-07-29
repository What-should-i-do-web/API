using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace WhatShouldIDo.Infrastructure.Services
{
    public class GeoapifyPlacesProvider : IPlacesProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public GeoapifyPlacesProvider(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["Geoapify:ApiKey"];
        }

        public async Task<List<Place>> GetNearbyPlacesAsync(float lat, float lng, int radius, string keyword = null)
        {
            string categories = string.IsNullOrWhiteSpace(keyword) ? "catering.restaurant" : MapKeywordToCategory(keyword);
            string url = $"https://api.geoapify.com/v2/places?categories={categories}&filter=circle:{lng},{lat},{radius}&limit=10&apiKey={_apiKey}";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var results = new List<Place>();
            foreach (var feature in root.GetProperty("features").EnumerateArray())
            {
                var props = feature.GetProperty("properties");

                results.Add(new Place
                {
                    Id = Guid.NewGuid(),
                    Name = props.GetProperty("name").GetString(),
                    Latitude = props.GetProperty("lat").GetSingle(),
                    Longitude = props.GetProperty("lon").GetSingle(),
                    Address = props.TryGetProperty("address_line1", out var addr) ? addr.GetString() : "",
                    Rating = props.TryGetProperty("rating", out var rating) ? rating.ToString() : null,
                    Category = categories,
                    Source = "Geoapify",
                    CachedAt = DateTime.UtcNow
                });
            }

            return results;
        }

        public Task<List<Place>> SearchByPromptAsync(string textQuery, float lat, float lng, string[] priceLevels = null)
        {
            throw new NotImplementedException();
        }

        private string MapKeywordToCategory(string keyword)
        {
            keyword = keyword.ToLower();
            return keyword switch
            {
                "cafe" => "catering.cafe",
                "coffee" => "catering.cafe",
                "restaurant" => "catering.restaurant",
                "park" => "leisure.park",
                "museum" => "entertainment.museum",
                "shopping" => "commercial.shopping_mall",
                _ => "catering.restaurant"
            };
        }
    }
}
