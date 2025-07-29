using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.Infrastructure.Services
{
    public class GoogleGeocodingService : IGeocodingService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly ILogger<GoogleGeocodingService> _logger;
        private readonly string _base_Geo_URL;


        public GoogleGeocodingService(HttpClient httpClient, IConfiguration configuration, ILogger<GoogleGeocodingService> logger)
        {
            _httpClient = httpClient;
            _apiKey = configuration["GooglePlaces:ApiKey"];
            _base_Geo_URL = configuration["GooglePlaces:GeocodingUrl"];
            _logger = logger;
        }

        public async Task<(float Latitude, float Longitude)> GetCoordinatesAsync(string locationText)
        {
            var query = HttpUtility.UrlEncode(locationText);
            var url = $"{_base_Geo_URL}={query}&key={_apiKey}";

            _logger.LogInformation("Geocoding API call: {url}", url);

            var response = await _httpClient.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Geocoding API : {status}, {json}", response.StatusCode, json);
                return (0, 0);
            }

            var root = JsonDocument.Parse(json).RootElement;
            var location = root
                .GetProperty("results")[0]
                .GetProperty("geometry")
                .GetProperty("location");

            return (
                location.GetProperty("lat").GetSingle(),
                location.GetProperty("lng").GetSingle()
            );
        }
    }
}
