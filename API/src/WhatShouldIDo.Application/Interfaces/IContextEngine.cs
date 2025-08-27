using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Application.Interfaces
{
    public interface IContextEngine
    {
        Task<List<Place>> ApplyContextualFiltering(List<Place> places, float lat, float lng, CancellationToken cancellationToken = default);
        Task<ContextualInsight> GetContextualInsights(float lat, float lng, CancellationToken cancellationToken = default);
        Task<List<string>> GetContextualReasons(Place place, ContextualInsight context);
    }

    public class ContextualInsight
    {
        public TimeOfDayContext TimeContext { get; set; } = TimeOfDayContext.Any;
        public WeatherContext Weather { get; set; } = new();
        public SeasonContext Season { get; set; } = SeasonContext.Any;
        public LocationContext Location { get; set; } = LocationContext.Unknown;
        public List<string> SuggestedCategories { get; set; } = new();
        public List<string> RecommendedActivities { get; set; } = new();
    }

    public class WeatherContext
    {
        public string Condition { get; set; } = "Unknown"; // "Sunny", "Rainy", "Cloudy", etc.
        public float Temperature { get; set; } = 20; // Celsius
        public bool IsGoodForOutdoor { get; set; } = true;
        public string Description { get; set; } = string.Empty;
    }

    public enum TimeOfDayContext
    {
        Any,
        EarlyMorning,    // 6-9 AM
        Morning,         // 9-12 PM  
        Lunch,           // 12-2 PM
        Afternoon,       // 2-5 PM
        Evening,         // 5-8 PM
        Night,           // 8-11 PM
        LateNight        // 11 PM-6 AM
    }

    public enum SeasonContext
    {
        Any,
        Spring,
        Summer, 
        Autumn,
        Winter
    }

    public enum LocationContext
    {
        Unknown,
        TouristArea,
        LocalNeighborhood,
        BusinessDistrict,
        HistoricArea,
        WaterfrontArea,
        ShoppingDistrict
    }
}