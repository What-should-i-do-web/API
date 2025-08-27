namespace WhatShouldIDo.Application.Interfaces
{
    public interface IWeatherService
    {
        Task<WeatherData> GetCurrentWeatherAsync(float lat, float lng, CancellationToken cancellationToken = default);
        Task<List<WeatherForecast>> GetForecastAsync(float lat, float lng, int days = 5, CancellationToken cancellationToken = default);
    }

    public class WeatherData
    {
        public float Temperature { get; set; }
        public float FeelsLike { get; set; }
        public string Condition { get; set; } = string.Empty; // "Clear", "Rain", "Clouds", "Snow"
        public string Description { get; set; } = string.Empty; // "Light rain", "Partly cloudy"
        public int Humidity { get; set; }
        public float WindSpeed { get; set; }
        public int Visibility { get; set; } // in km
        public bool IsGoodForOutdoor { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Istanbul specific
        public int AirQualityIndex { get; set; }
        public bool IsGoodForWalking { get; set; }
    }

    public class WeatherForecast
    {
        public DateTime Date { get; set; }
        public float MaxTemp { get; set; }
        public float MinTemp { get; set; }
        public string Condition { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int ChanceOfRain { get; set; }
    }
}