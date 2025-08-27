using Microsoft.Extensions.Logging;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Infrastructure.Services
{
    public class ContextEngine : IContextEngine
    {
        private readonly IWeatherService _weatherService;
        private readonly ILogger<ContextEngine> _logger;

        public ContextEngine(IWeatherService weatherService, ILogger<ContextEngine> logger)
        {
            _weatherService = weatherService;
            _logger = logger;
        }

        public async Task<List<Place>> ApplyContextualFiltering(List<Place> places, float lat, float lng, CancellationToken cancellationToken = default)
        {
            try
            {
                var context = await GetContextualInsights(lat, lng, cancellationToken);
                var scoredPlaces = new List<(Place place, float score)>();

                foreach (var place in places)
                {
                    var score = CalculateContextualScore(place, context);
                    scoredPlaces.Add((place, score));
                }

                // Sort by contextual relevance and return top results
                var contextuallyFiltered = scoredPlaces
                    .OrderByDescending(x => x.score)
                    .Select(x => x.place)
                    .ToList();

                _logger.LogInformation("Applied contextual filtering to {count} places for {timeContext} at {temperature}°C", 
                    places.Count, context.TimeContext, context.Weather.Temperature);

                return contextuallyFiltered;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Contextual filtering failed, returning original places");
                return places;
            }
        }

        public async Task<ContextualInsight> GetContextualInsights(float lat, float lng, CancellationToken cancellationToken = default)
        {
            var context = new ContextualInsight
            {
                TimeContext = GetTimeOfDayContext(),
                Season = GetSeasonContext(),
                Location = GetLocationContext(lat, lng)
            };

            // Get weather data
            try
            {
                var weather = await _weatherService.GetCurrentWeatherAsync(lat, lng, cancellationToken);
                context.Weather = new WeatherContext
                {
                    Condition = weather.Condition,
                    Temperature = weather.Temperature,
                    IsGoodForOutdoor = weather.IsGoodForOutdoor,
                    Description = weather.Description
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch weather data, using defaults");
                context.Weather = new WeatherContext(); // Default values
            }

            // Generate contextual recommendations
            context.SuggestedCategories = GetSuggestedCategories(context);
            context.RecommendedActivities = GetRecommendedActivities(context);

            return context;
        }

        public async Task<List<string>> GetContextualReasons(Place place, ContextualInsight context)
        {
            var reasons = new List<string>();

            // Time-based reasons
            reasons.AddRange(GetTimeBasedReasons(place, context.TimeContext));

            // Weather-based reasons  
            reasons.AddRange(GetWeatherBasedReasons(place, context.Weather));

            // Season-based reasons
            reasons.AddRange(GetSeasonBasedReasons(place, context.Season));

            // Location-based reasons
            reasons.AddRange(GetLocationBasedReasons(place, context.Location));

            return reasons.Take(2).ToList(); // Limit to 2 most relevant reasons
        }

        private float CalculateContextualScore(Place place, ContextualInsight context)
        {
            float score = 0f;

            // Time of day scoring
            score += CalculateTimeScore(place, context.TimeContext);

            // Weather scoring
            score += CalculateWeatherScore(place, context.Weather);

            // Season scoring  
            score += CalculateSeasonScore(place, context.Season);

            // Location type scoring
            score += CalculateLocationScore(place, context.Location);

            return Math.Max(0f, Math.Min(100f, score)); // Clamp to 0-100
        }

        private TimeOfDayContext GetTimeOfDayContext()
        {
            var hour = DateTime.Now.Hour;
            
            return hour switch
            {
                >= 6 and < 9 => TimeOfDayContext.EarlyMorning,
                >= 9 and < 12 => TimeOfDayContext.Morning,
                >= 12 and < 14 => TimeOfDayContext.Lunch,
                >= 14 and < 17 => TimeOfDayContext.Afternoon,
                >= 17 and < 20 => TimeOfDayContext.Evening,
                >= 20 and < 23 => TimeOfDayContext.Night,
                _ => TimeOfDayContext.LateNight
            };
        }

        private SeasonContext GetSeasonContext()
        {
            var month = DateTime.Now.Month;
            
            return month switch
            {
                >= 3 and <= 5 => SeasonContext.Spring,
                >= 6 and <= 8 => SeasonContext.Summer,
                >= 9 and <= 11 => SeasonContext.Autumn,
                _ => SeasonContext.Winter
            };
        }

        private LocationContext GetLocationContext(float lat, float lng)
        {
            // Istanbul-specific location detection
            // Historical Peninsula
            if (lat >= 41.000 && lat <= 41.020 && lng >= 28.950 && lng <= 28.985)
                return LocationContext.HistoricArea;

            // Bosphorus waterfront
            if (lng >= 28.980 && lng <= 29.080)
                return LocationContext.WaterfrontArea;

            // Taksim/Beyoğlu tourist area
            if (lat >= 41.025 && lat <= 41.040 && lng >= 28.970 && lng <= 29.000)
                return LocationContext.TouristArea;

            // Şişli/Mecidiyeköy business district
            if (lat >= 41.040 && lat <= 41.070 && lng >= 28.980 && lng <= 29.010)
                return LocationContext.BusinessDistrict;

            return LocationContext.LocalNeighborhood;
        }

        private List<string> GetSuggestedCategories(ContextualInsight context)
        {
            var categories = new List<string>();

            // Time-based categories
            switch (context.TimeContext)
            {
                case TimeOfDayContext.EarlyMorning:
                case TimeOfDayContext.Morning:
                    categories.AddRange(new[] { "cafe", "bakery", "breakfast", "park" });
                    break;
                case TimeOfDayContext.Lunch:
                    categories.AddRange(new[] { "restaurant", "fast_food", "lunch" });
                    break;
                case TimeOfDayContext.Afternoon:
                    categories.AddRange(new[] { "museum", "shopping", "cafe", "tourist_attraction" });
                    break;
                case TimeOfDayContext.Evening:
                case TimeOfDayContext.Night:
                    categories.AddRange(new[] { "restaurant", "bar", "entertainment", "theater" });
                    break;
            }

            // Weather-based categories
            if (!context.Weather.IsGoodForOutdoor)
            {
                categories.AddRange(new[] { "museum", "shopping_mall", "cinema", "cafe", "restaurant" });
                categories.RemoveAll(c => c == "park" || c == "outdoor");
            }
            else
            {
                categories.AddRange(new[] { "park", "outdoor", "tourist_attraction", "waterfront" });
            }

            return categories.Distinct().ToList();
        }

        private List<string> GetRecommendedActivities(ContextualInsight context)
        {
            var activities = new List<string>();

            // Time-based activities
            switch (context.TimeContext)
            {
                case TimeOfDayContext.EarlyMorning:
                    activities.AddRange(new[] { "Kahvaltı yapmak", "Sabah yürüyüşü", "Fotoğraf çekmek" });
                    break;
                case TimeOfDayContext.Morning:
                    activities.AddRange(new[] { "Müze gezisi", "Alışveriş", "Kafe molası" });
                    break;
                case TimeOfDayContext.Lunch:
                    activities.AddRange(new[] { "Öğle yemeği", "İş görüşmesi", "Hızlı yemek" });
                    break;
                case TimeOfDayContext.Afternoon:
                    activities.AddRange(new[] { "Kültür turu", "Alışveriş", "Sosyal aktivite" });
                    break;
                case TimeOfDayContext.Evening:
                    activities.AddRange(new[] { "Akşam yemeği", "Sosyal buluşma", "Eğlence" });
                    break;
                case TimeOfDayContext.Night:
                    activities.AddRange(new[] { "Bar", "Gece hayatı", "Romantik yemek" });
                    break;
            }

            // Weather-based activities
            if (context.Weather.IsGoodForOutdoor && context.Weather.Temperature > 15)
            {
                activities.AddRange(new[] { "Açık hava aktivitesi", "Yürüyüş", "Piknik" });
            }
            else
            {
                activities.AddRange(new[] { "İç mekan aktivitesi", "Sıcak içecek", "Kapalı alışveriş" });
            }

            return activities.Take(5).ToList();
        }

        private List<string> GetTimeBasedReasons(Place place, TimeOfDayContext timeContext)
        {
            var reasons = new List<string>();
            var placeName = place.Name.ToLower();

            switch (timeContext)
            {
                case TimeOfDayContext.EarlyMorning:
                case TimeOfDayContext.Morning:
                    if (placeName.Contains("cafe") || placeName.Contains("kahve") || placeName.Contains("breakfast"))
                        reasons.Add("Sabah kahvaltısı için ideal");
                    break;
                case TimeOfDayContext.Lunch:
                    if (placeName.Contains("restaurant") || placeName.Contains("lokanta") || placeName.Contains("restoran"))
                        reasons.Add("Öğle yemeği saatine uygun");
                    break;
                case TimeOfDayContext.Evening:
                case TimeOfDayContext.Night:
                    if (placeName.Contains("bar") || placeName.Contains("restaurant") || placeName.Contains("pub"))
                        reasons.Add("Akşam sosyalleşmesi için uygun");
                    break;
            }

            return reasons;
        }

        private List<string> GetWeatherBasedReasons(Place place, WeatherContext weather)
        {
            var reasons = new List<string>();
            var placeName = place.Name.ToLower();

            if (!weather.IsGoodForOutdoor)
            {
                if (placeName.Contains("museum") || placeName.Contains("müze") || placeName.Contains("mall") || placeName.Contains("cafe"))
                    reasons.Add($"Hava koşulları için ideal kapalı mekan ({weather.Description})");
            }
            else if (weather.Temperature > 20)
            {
                if (placeName.Contains("park") || placeName.Contains("outdoor") || placeName.Contains("bahçe"))
                    reasons.Add($"Güzel hava için açık hava aktivitesi ({weather.Temperature:F0}°C)");
            }

            return reasons;
        }

        private List<string> GetSeasonBasedReasons(Place place, SeasonContext season)
        {
            var reasons = new List<string>();
            var placeName = place.Name.ToLower();

            switch (season)
            {
                case SeasonContext.Spring:
                    if (placeName.Contains("park") || placeName.Contains("garden") || placeName.Contains("bahçe"))
                        reasons.Add("Bahar mevsimi için park ve bahçe aktivitesi");
                    break;
                case SeasonContext.Summer:
                    if (placeName.Contains("beach") || placeName.Contains("waterfront") || placeName.Contains("deniz"))
                        reasons.Add("Yaz mevsimi için su kenarı aktivitesi");
                    break;
                case SeasonContext.Winter:
                    if (placeName.Contains("museum") || placeName.Contains("cafe") || placeName.Contains("restaurant"))
                        reasons.Add("Kış mevsimi için sıcak iç mekan");
                    break;
            }

            return reasons;
        }

        private List<string> GetLocationBasedReasons(Place place, LocationContext location)
        {
            var reasons = new List<string>();

            switch (location)
            {
                case LocationContext.TouristArea:
                    reasons.Add("Turist bölgesinde popüler mekan");
                    break;
                case LocationContext.HistoricArea:
                    reasons.Add("Tarihi bölgede kültürel deneyim");
                    break;
                case LocationContext.WaterfrontArea:
                    reasons.Add("Boğaz manzaralı özel konum");
                    break;
                case LocationContext.LocalNeighborhood:
                    reasons.Add("Yerel mahallede otantik deneyim");
                    break;
            }

            return reasons;
        }

        private float CalculateTimeScore(Place place, TimeOfDayContext timeContext)
        {
            var placeName = place.Name.ToLower();
            
            return timeContext switch
            {
                TimeOfDayContext.EarlyMorning => GetScore(placeName, new[] { "cafe", "kahve", "breakfast", "fırın" }, 25f),
                TimeOfDayContext.Morning => GetScore(placeName, new[] { "cafe", "museum", "müze", "park" }, 20f),
                TimeOfDayContext.Lunch => GetScore(placeName, new[] { "restaurant", "lokanta", "fast" }, 30f),
                TimeOfDayContext.Afternoon => GetScore(placeName, new[] { "cafe", "shopping", "museum", "park" }, 15f),
                TimeOfDayContext.Evening => GetScore(placeName, new[] { "restaurant", "restoran", "bar" }, 25f),
                TimeOfDayContext.Night => GetScore(placeName, new[] { "bar", "pub", "club", "restaurant" }, 20f),
                _ => 0f
            };
        }

        private float CalculateWeatherScore(Place place, WeatherContext weather)
        {
            var placeName = place.Name.ToLower();
            
            if (!weather.IsGoodForOutdoor)
            {
                return GetScore(placeName, new[] { "museum", "mall", "cafe", "restaurant", "cinema" }, 15f);
            }
            else if (weather.Temperature > 20)
            {
                return GetScore(placeName, new[] { "park", "outdoor", "bahçe", "terrace" }, 15f);
            }
            
            return 0f;
        }

        private float CalculateSeasonScore(Place place, SeasonContext season)
        {
            var placeName = place.Name.ToLower();
            
            return season switch
            {
                SeasonContext.Summer => GetScore(placeName, new[] { "beach", "outdoor", "terrace", "garden" }, 10f),
                SeasonContext.Winter => GetScore(placeName, new[] { "museum", "cafe", "restaurant", "indoor" }, 10f),
                SeasonContext.Spring => GetScore(placeName, new[] { "park", "garden", "outdoor", "cafe" }, 10f),
                _ => 0f
            };
        }

        private float CalculateLocationScore(Place place, LocationContext location)
        {
            return location switch
            {
                LocationContext.TouristArea => 10f,
                LocationContext.HistoricArea => 15f,
                LocationContext.WaterfrontArea => 20f,
                LocationContext.LocalNeighborhood => 5f,
                _ => 0f
            };
        }

        private float GetScore(string placeName, string[] keywords, float maxScore)
        {
            foreach (var keyword in keywords)
            {
                if (placeName.Contains(keyword))
                    return maxScore;
            }
            return 0f;
        }
    }
}