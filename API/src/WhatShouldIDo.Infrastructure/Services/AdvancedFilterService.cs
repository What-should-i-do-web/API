using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using WhatShouldIDo.Application.DTOs.Request;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Services;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Infrastructure.Caching;
using System.Linq.Expressions;

namespace WhatShouldIDo.Infrastructure.Services;

public class AdvancedFilterService : IAdvancedFilterService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<AdvancedFilterService> _logger;
    private readonly IWeatherService _weatherService;
    private readonly ICacheService _cacheService;

    private const int CacheExpirationMinutes = 30;

    public AdvancedFilterService(
        IMemoryCache cache,
        ILogger<AdvancedFilterService> logger,
        IWeatherService weatherService,
        ICacheService cacheService)
    {
        _cache = cache;
        _logger = logger;
        _weatherService = weatherService;
        _cacheService = cacheService;
    }

    public async Task<IEnumerable<SuggestionDto>> ApplyFiltersAsync(IEnumerable<SuggestionDto> suggestions, FilterCriteria criteria)
    {
        try
        {
            if (!suggestions.Any()) return suggestions;

            var filteredSuggestions = suggestions.AsEnumerable();

            // Apply location filters
            if (criteria.Latitude.HasValue && criteria.Longitude.HasValue && criteria.Radius.HasValue)
            {
                filteredSuggestions = filteredSuggestions.Where(s => 
                    CalculateDistance(criteria.Latitude.Value, criteria.Longitude.Value, s.Latitude, s.Longitude) 
                    <= criteria.Radius.Value);
            }

            // Apply category filters
            if (criteria.Categories?.Any() == true)
            {
                filteredSuggestions = filteredSuggestions.Where(s => 
                    criteria.Categories.Contains(s.Category, StringComparer.OrdinalIgnoreCase));
            }

            if (criteria.ExcludeCategories?.Any() == true)
            {
                filteredSuggestions = filteredSuggestions.Where(s => 
                    !criteria.ExcludeCategories.Contains(s.Category, StringComparer.OrdinalIgnoreCase));
            }

            // Apply score filters
            if (criteria.MinScore.HasValue)
            {
                filteredSuggestions = filteredSuggestions.Where(s => s.Score >= criteria.MinScore.Value);
            }

            // Apply time-based filters
            filteredSuggestions = await ApplyTimeFiltersAsync(filteredSuggestions, criteria);

            // Apply weather-based filters
            filteredSuggestions = await ApplyWeatherFiltersAsync(filteredSuggestions, criteria);

            // Apply social filters
            filteredSuggestions = ApplySocialFilters(filteredSuggestions, criteria);

            // Apply keyword filters
            if (criteria.Keywords?.Any() == true)
            {
                filteredSuggestions = filteredSuggestions.Where(s =>
                    criteria.Keywords.Any(keyword =>
                        s.PlaceName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                        s.Category.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                        s.Reason.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
            }

            // Apply source filters
            if (criteria.Sources?.Any() == true)
            {
                filteredSuggestions = filteredSuggestions.Where(s =>
                    criteria.Sources.Contains(s.Source, StringComparer.OrdinalIgnoreCase));
            }

            // Apply photo filter
            if (criteria.HasPhotos == true)
            {
                filteredSuggestions = filteredSuggestions.Where(s => !string.IsNullOrEmpty(s.PhotoUrl));
            }

            // Apply created date filter
            if (criteria.CreatedAfter.HasValue)
            {
                filteredSuggestions = filteredSuggestions.Where(s => s.CreatedAt >= criteria.CreatedAfter.Value);
            }

            // Apply user hash filter for personalization
            if (!string.IsNullOrEmpty(criteria.UserHash) && criteria.MatchPreferences == true)
            {
                filteredSuggestions = await ApplyPersonalizationFiltersAsync(filteredSuggestions, criteria.UserHash);
            }

            // Apply sorting
            filteredSuggestions = ApplySorting(filteredSuggestions, criteria.SortBy ?? SortBy.Relevance, criteria.Latitude, criteria.Longitude);

            // Apply sponsored content filter
            if (criteria.IncludeSponsored == false)
            {
                filteredSuggestions = filteredSuggestions.Where(s => !s.IsSponsored);
            }

            // Apply limit
            if (criteria.Limit.HasValue && criteria.Limit.Value > 0)
            {
                filteredSuggestions = filteredSuggestions.Take(criteria.Limit.Value);
            }

            return filteredSuggestions.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying filters to suggestions");
            return suggestions; // Return unfiltered results on error
        }
    }

    public async Task<FilterCriteria> GetSmartFiltersAsync(double latitude, double longitude, string? userHash = null)
    {
        try
        {
            var cacheKey = $"smart_filters_{latitude}_{longitude}_{userHash}";
            if (_cache.TryGetValue(cacheKey, out FilterCriteria? cachedFilters) && cachedFilters != null)
            {
                return cachedFilters;
            }

            var smartFilters = new FilterCriteria
            {
                Latitude = latitude,
                Longitude = longitude,
                Radius = 3000 // Default 3km radius
            };

            // Get current time-based smart filters
            var currentHour = DateTime.Now.Hour;
            smartFilters.TimeOfDay = currentHour switch
            {
                >= 6 and < 9 => TimeOfDay.EarlyMorning,
                >= 9 and < 12 => TimeOfDay.Morning,
                >= 12 and < 17 => TimeOfDay.Afternoon,
                >= 17 and < 20 => TimeOfDay.Evening,
                >= 20 and < 24 => TimeOfDay.Night,
                _ => TimeOfDay.LateNight
            };

            // Get weather-based smart filters
            try
            {
                var weather = await _weatherService.GetCurrentWeatherAsync((float)latitude, (float)longitude);
                if (weather != null)
                {
                    smartFilters.WeatherCondition = DetermineWeatherCondition(weather);
                    smartFilters.IndoorOnly = IsIndoorRecommended(weather);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get weather data for smart filters");
            }

            // Apply day-based filters
            var today = DateTime.Now.DayOfWeek;
            smartFilters.PreferredDays = new List<DayOfWeek> { today };

            if (today == DayOfWeek.Saturday || today == DayOfWeek.Sunday)
            {
                smartFilters.FamilyFriendly = true;
                smartFilters.PopularWithLocals = true;
            }

            _cache.Set(cacheKey, smartFilters, TimeSpan.FromMinutes(CacheExpirationMinutes));
            return smartFilters;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating smart filters");
            return new FilterCriteria { Latitude = latitude, Longitude = longitude };
        }
    }

    public async Task<Dictionary<string, object>> GetFilterStatisticsAsync(IEnumerable<SuggestionDto> suggestions)
    {
        try
        {
            var stats = new Dictionary<string, object>();
            var suggestionsList = suggestions.ToList();

            if (!suggestionsList.Any())
            {
                return stats;
            }

            // Category distribution
            var categoryStats = suggestionsList
                .GroupBy(s => s.Category)
                .ToDictionary(g => g.Key, g => g.Count());
            stats["categories"] = categoryStats;

            // Source distribution
            var sourceStats = suggestionsList
                .GroupBy(s => s.Source)
                .ToDictionary(g => g.Key, g => g.Count());
            stats["sources"] = sourceStats;

            // Score statistics
            stats["score_stats"] = new
            {
                min = suggestionsList.Min(s => s.Score),
                max = suggestionsList.Max(s => s.Score),
                avg = suggestionsList.Average(s => s.Score),
                median = suggestionsList.OrderBy(s => s.Score).ElementAt(suggestionsList.Count / 2).Score
            };

            // Photo availability
            stats["with_photos"] = suggestionsList.Count(s => !string.IsNullOrEmpty(s.PhotoUrl));
            stats["without_photos"] = suggestionsList.Count(s => string.IsNullOrEmpty(s.PhotoUrl));

            // Sponsored content
            stats["sponsored"] = suggestionsList.Count(s => s.IsSponsored);
            stats["organic"] = suggestionsList.Count(s => !s.IsSponsored);

            // Time-based stats
            var now = DateTime.Now;
            stats["created_today"] = suggestionsList.Count(s => s.CreatedAt.Date == now.Date);
            stats["created_this_week"] = suggestionsList.Count(s => s.CreatedAt >= now.AddDays(-7));

            stats["total_results"] = suggestionsList.Count;

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating filter statistics");
            return new Dictionary<string, object>();
        }
    }

    public async Task<FilterCriteria> OptimizeFiltersAsync(FilterCriteria criteria, int targetResultCount = 20)
    {
        // This would implement smart filter optimization to reach target result count
        // For now, returning the original criteria
        return criteria;
    }

    public bool ValidateFilters(FilterCriteria criteria, out List<string> errors)
    {
        errors = new List<string>();

        if (criteria.Radius.HasValue && criteria.Radius.Value <= 0)
        {
            errors.Add("Radius must be greater than 0");
        }

        if (criteria.Radius.HasValue && criteria.Radius.Value > 50000)
        {
            errors.Add("Radius cannot exceed 50km");
        }

        if (criteria.MinRating.HasValue && (criteria.MinRating.Value < 0 || criteria.MinRating.Value > 5))
        {
            errors.Add("MinRating must be between 0 and 5");
        }

        if (criteria.MaxRating.HasValue && (criteria.MaxRating.Value < 0 || criteria.MaxRating.Value > 5))
        {
            errors.Add("MaxRating must be between 0 and 5");
        }

        if (criteria.MinRating.HasValue && criteria.MaxRating.HasValue && criteria.MinRating.Value > criteria.MaxRating.Value)
        {
            errors.Add("MinRating cannot be greater than MaxRating");
        }

        if (criteria.Limit.HasValue && criteria.Limit.Value <= 0)
        {
            errors.Add("Limit must be greater than 0");
        }

        if (criteria.Limit.HasValue && criteria.Limit.Value > 100)
        {
            errors.Add("Limit cannot exceed 100");
        }

        return !errors.Any();
    }

    public async Task<IEnumerable<string>> GetPopularFiltersAsync(string? userHash = null)
    {
        // This would return popular filter combinations based on usage analytics
        return new List<string>
        {
            "restaurants_nearby",
            "outdoor_activities",
            "cultural_attractions",
            "family_friendly",
            "budget_friendly"
        };
    }

    public async Task<FilterCriteria> GetRecommendedFiltersAsync(string? userHash, double? latitude = null, double? longitude = null)
    {
        var recommendations = new FilterCriteria();

        if (latitude.HasValue && longitude.HasValue)
        {
            recommendations = await GetSmartFiltersAsync(latitude.Value, longitude.Value, userHash);
        }

        // Add user preference-based recommendations here
        return recommendations;
    }

    private async Task<IEnumerable<SuggestionDto>> ApplyTimeFiltersAsync(IEnumerable<SuggestionDto> suggestions, FilterCriteria criteria)
    {
        // Time-based filtering logic would go here
        return suggestions;
    }

    private async Task<IEnumerable<SuggestionDto>> ApplyWeatherFiltersAsync(IEnumerable<SuggestionDto> suggestions, FilterCriteria criteria)
    {
        if (criteria.IndoorOnly == true)
        {
            return suggestions.Where(s => IsIndoorActivity(s.Category));
        }

        if (criteria.OutdoorOnly == true)
        {
            return suggestions.Where(s => IsOutdoorActivity(s.Category));
        }

        return suggestions;
    }

    private IEnumerable<SuggestionDto> ApplySocialFilters(IEnumerable<SuggestionDto> suggestions, FilterCriteria criteria)
    {
        if (criteria.TrendingNow == true)
        {
            // Logic to identify trending places
            suggestions = suggestions.Where(s => s.CreatedAt >= DateTime.Now.AddHours(-24));
        }

        return suggestions;
    }

    private async Task<IEnumerable<SuggestionDto>> ApplyPersonalizationFiltersAsync(IEnumerable<SuggestionDto> suggestions, string userHash)
    {
        // Apply user preference-based filtering
        return suggestions;
    }

    private IEnumerable<SuggestionDto> ApplySorting(IEnumerable<SuggestionDto> suggestions, SortBy sortBy, double? userLat, double? userLng)
    {
        return sortBy switch
        {
            SortBy.Distance when userLat.HasValue && userLng.HasValue => 
                suggestions.OrderBy(s => CalculateDistance(userLat.Value, userLng.Value, s.Latitude, s.Longitude)),
            SortBy.Score => suggestions.OrderByDescending(s => s.Score),
            SortBy.Recent => suggestions.OrderByDescending(s => s.CreatedAt),
            SortBy.Name => suggestions.OrderBy(s => s.PlaceName),
            _ => suggestions.OrderByDescending(s => s.Score) // Default relevance
        };
    }

    private static double CalculateDistance(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371000; // Earth's radius in meters
        var dLat = ToRadians(lat2 - lat1);
        var dLng = ToRadians(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static double ToRadians(double degrees) => degrees * (Math.PI / 180);

    private WeatherCondition DetermineWeatherCondition(WeatherData weather)
    {
        return weather.Condition.ToLowerInvariant() switch
        {
            "clear" => WeatherCondition.Sunny,
            "clouds" => WeatherCondition.Cloudy,
            "rain" => WeatherCondition.Rainy,
            "snow" => WeatherCondition.Snowy,
            "drizzle" => WeatherCondition.Rainy,
            _ when weather.WindSpeed > 20 => WeatherCondition.Windy,
            _ when weather.Temperature > 30 => WeatherCondition.Hot,
            _ when weather.Temperature < 5 => WeatherCondition.Cold,
            _ => WeatherCondition.Sunny
        };
    }

    private bool IsIndoorRecommended(WeatherData weather)
    {
        return weather.Condition.ToLowerInvariant() switch
        {
            "rain" or "snow" or "thunderstorm" => true,
            _ when weather.Temperature < 0 || weather.Temperature > 35 => true,
            _ when weather.WindSpeed > 25 => true,
            _ => false
        };
    }

    private bool IsIndoorActivity(string category)
    {
        var indoorCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "museum", "shopping", "cafe", "restaurant", "mall", "cinema", "theater", "gallery"
        };
        return indoorCategories.Contains(category);
    }

    private bool IsOutdoorActivity(string category)
    {
        var outdoorCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "park", "beach", "hiking", "sports", "garden", "playground", "zoo", "tourist_attraction"
        };
        return outdoorCategories.Contains(category);
    }
}