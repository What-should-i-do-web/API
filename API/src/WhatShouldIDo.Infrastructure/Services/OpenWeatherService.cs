using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.Infrastructure.Services
{
    public class OpenWeatherService : IWeatherService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly ILogger<OpenWeatherService> _logger;
        private const string BaseUrl = "https://api.openweathermap.org/data/2.5";

        public OpenWeatherService(HttpClient httpClient, IConfiguration configuration, ILogger<OpenWeatherService> logger)
        {
            _httpClient = httpClient;
            _apiKey = configuration["OpenWeather:ApiKey"] ?? throw new ArgumentNullException("OpenWeather:ApiKey");
            _logger = logger;
        }

        public async Task<WeatherData> GetCurrentWeatherAsync(float lat, float lng, CancellationToken cancellationToken = default)
        {
            try
            {
                var url = $"{BaseUrl}/weather?lat={lat}&lon={lng}&appid={_apiKey}&units=metric&lang=tr";
                var response = await _httpClient.GetAsync(url, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Weather API returned {StatusCode}", response.StatusCode);
                    return GetDefaultWeather();
                }

                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var weatherResponse = JsonSerializer.Deserialize<OpenWeatherResponse>(jsonContent, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                if (weatherResponse == null)
                    return GetDefaultWeather();

                var weatherData = new WeatherData
                {
                    Temperature = weatherResponse.Main?.Temp ?? 20f,
                    FeelsLike = weatherResponse.Main?.FeelsLike ?? 20f,
                    Condition = weatherResponse.Weather?.FirstOrDefault()?.Main ?? "Clear",
                    Description = weatherResponse.Weather?.FirstOrDefault()?.Description ?? "Açık hava",
                    Humidity = weatherResponse.Main?.Humidity ?? 50,
                    WindSpeed = weatherResponse.Wind?.Speed ?? 0f,
                    Visibility = (weatherResponse.Visibility ?? 10000) / 1000, // Convert to km
                    UpdatedAt = DateTime.UtcNow
                };

                // Calculate contextual properties
                weatherData.IsGoodForOutdoor = CalculateOutdoorSuitability(weatherData);
                weatherData.IsGoodForWalking = CalculateWalkingSuitability(weatherData);
                weatherData.AirQualityIndex = await GetAirQualityAsync(lat, lng, cancellationToken);

                _logger.LogInformation("Weather data retrieved: {temp}°C, {condition}, Outdoor: {outdoor}", 
                    weatherData.Temperature, weatherData.Condition, weatherData.IsGoodForOutdoor);

                return weatherData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve weather data for {lat},{lng}", lat, lng);
                return GetDefaultWeather();
            }
        }

        public async Task<List<WeatherForecast>> GetForecastAsync(float lat, float lng, int days = 5, CancellationToken cancellationToken = default)
        {
            try
            {
                var url = $"{BaseUrl}/forecast?lat={lat}&lon={lng}&appid={_apiKey}&units=metric&lang=tr";
                var response = await _httpClient.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Weather forecast API returned {StatusCode}", response.StatusCode);
                    return new List<WeatherForecast>();
                }

                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var forecastResponse = JsonSerializer.Deserialize<OpenWeatherForecastResponse>(jsonContent, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                if (forecastResponse?.List == null)
                    return new List<WeatherForecast>();

                // Group by date and take daily forecasts
                var dailyForecasts = forecastResponse.List
                    .GroupBy(f => DateTimeExtensions.UnixTimeStamp((long)f.Dt).Date)
                    .Take(days)
                    .Select(g =>
                    {
                        var dayForecasts = g.ToList();
                        var midDayForecast = dayForecasts.OrderBy(f => Math.Abs(12 - DateTimeExtensions.UnixTimeStamp((long)f.Dt).Hour)).First();
                        
                        return new WeatherForecast
                        {
                            Date = g.Key,
                            MaxTemp = dayForecasts.Max(f => f.Main?.TempMax ?? 20f),
                            MinTemp = dayForecasts.Min(f => f.Main?.TempMin ?? 15f),
                            Condition = midDayForecast.Weather?.FirstOrDefault()?.Main ?? "Clear",
                            Description = midDayForecast.Weather?.FirstOrDefault()?.Description ?? "Açık",
                            ChanceOfRain = (int)((midDayForecast.Pop ?? 0f) * 100)
                        };
                    })
                    .ToList();

                _logger.LogInformation("Weather forecast retrieved for {daysCount} days", dailyForecasts.Count);
                return dailyForecasts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve weather forecast for {lat},{lng}", lat, lng);
                return new List<WeatherForecast>();
            }
        }

        private async Task<int> GetAirQualityAsync(float lat, float lng, CancellationToken cancellationToken)
        {
            try
            {
                var url = $"{BaseUrl}/air_pollution?lat={lat}&lon={lng}&appid={_apiKey}";
                var response = await _httpClient.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                    return 50; // Default moderate air quality

                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var airQualityResponse = JsonSerializer.Deserialize<AirQualityResponse>(jsonContent, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                return airQualityResponse?.List?.FirstOrDefault()?.Main?.Aqi ?? 50;
            }
            catch
            {
                return 50; // Default on error
            }
        }

        private WeatherData GetDefaultWeather()
        {
            // Return reasonable defaults for Istanbul
            return new WeatherData
            {
                Temperature = 18f,
                FeelsLike = 18f,
                Condition = "Clear",
                Description = "Açık hava",
                Humidity = 60,
                WindSpeed = 10f,
                Visibility = 10,
                IsGoodForOutdoor = true,
                IsGoodForWalking = true,
                AirQualityIndex = 50,
                UpdatedAt = DateTime.UtcNow
            };
        }

        private bool CalculateOutdoorSuitability(WeatherData weather)
        {
            // Not suitable for outdoor if:
            // - Heavy rain or snow
            // - Too cold (< 5°C) or too hot (> 35°C) 
            // - Very high wind speed (> 25 km/h)
            // - Very low visibility (< 1 km)

            if (weather.Condition.Contains("Rain") || weather.Condition.Contains("Snow") || weather.Condition.Contains("Storm"))
                return false;

            if (weather.Temperature < 5f || weather.Temperature > 35f)
                return false;

            if (weather.WindSpeed > 7f) // m/s to roughly km/h
                return false;

            if (weather.Visibility < 1)
                return false;

            return true;
        }

        private bool CalculateWalkingSuitability(WeatherData weather)
        {
            // More lenient than outdoor suitability
            if (weather.Condition.Contains("Heavy") || weather.Condition.Contains("Storm"))
                return false;

            if (weather.Temperature < 0f || weather.Temperature > 40f)
                return false;

            return true;
        }

        // OpenWeather API response models
        private class OpenWeatherResponse
        {
            public WeatherInfo[]? Weather { get; set; }
            public MainInfo? Main { get; set; }
            public WindInfo? Wind { get; set; }
            public int? Visibility { get; set; }
        }

        private class OpenWeatherForecastResponse
        {
            public ForecastItem[]? List { get; set; }
        }

        private class ForecastItem
        {
            public long Dt { get; set; }
            public MainInfo? Main { get; set; }
            public WeatherInfo[]? Weather { get; set; }
            public float? Pop { get; set; } // Probability of precipitation
        }

        private class WeatherInfo
        {
            public string Main { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
        }

        private class MainInfo
        {
            public float Temp { get; set; }
            public float FeelsLike { get; set; }
            public float? TempMin { get; set; }
            public float? TempMax { get; set; }
            public int Humidity { get; set; }
        }

        private class WindInfo
        {
            public float Speed { get; set; }
        }

        private class AirQualityResponse
        {
            public AirQualityItem[]? List { get; set; }
        }

        private class AirQualityItem
        {
            public AirQualityMain? Main { get; set; }
        }

        private class AirQualityMain
        {
            public int Aqi { get; set; } // Air Quality Index: 1-5 (Good to Very Poor)
        }
    }

    // Extension method for Unix timestamp conversion
    public static class DateTimeExtensions
    {
        public static DateTime UnixTimeStamp(long unixTimeStamp)
        {
            return DateTime.UnixEpoch.AddSeconds(unixTimeStamp);
        }
    }
}