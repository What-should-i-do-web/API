using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.Infrastructure.Services
{
    /// <summary>
    /// Maps place categories/types to taste profile interest dimensions.
    /// Handles both Google Places types and OpenTripMap kinds.
    /// </summary>
    public class PlaceCategoryMapper : IPlaceCategoryMapper
    {
        // Interest dimensions (matches UserTasteProfile weights)
        private static readonly List<string> Interests = new()
        {
            "Culture", "Food", "Nature", "Nightlife", "Shopping", "Art", "Wellness", "Sports"
        };

        // Category mappings: category/type → interests with weights
        // A place can map to multiple interests (e.g., cafe → Food:0.8, Culture:0.2)
        private static readonly Dictionary<string, Dictionary<string, double>> CategoryMappings = new()
        {
            // Food & Dining
            ["restaurant"] = new() { ["Food"] = 1.0 },
            ["cafe"] = new() { ["Food"] = 0.8, ["Culture"] = 0.2 },
            ["bar"] = new() { ["Nightlife"] = 0.8, ["Food"] = 0.2 },
            ["bakery"] = new() { ["Food"] = 0.9, ["Shopping"] = 0.1 },
            ["meal_delivery"] = new() { ["Food"] = 1.0 },
            ["meal_takeaway"] = new() { ["Food"] = 1.0 },
            ["food"] = new() { ["Food"] = 1.0 },

            // Culture & History
            ["museum"] = new() { ["Culture"] = 1.0 },
            ["art_gallery"] = new() { ["Art"] = 0.8, ["Culture"] = 0.2 },
            ["tourist_attraction"] = new() { ["Culture"] = 0.7, ["Nature"] = 0.3 },
            ["point_of_interest"] = new() { ["Culture"] = 0.5, ["Nature"] = 0.3 },
            ["historical"] = new() { ["Culture"] = 1.0 },
            ["church"] = new() { ["Culture"] = 0.8, ["Art"] = 0.2 },
            ["mosque"] = new() { ["Culture"] = 0.8, ["Art"] = 0.2 },
            ["synagogue"] = new() { ["Culture"] = 0.8, ["Art"] = 0.2 },
            ["hindu_temple"] = new() { ["Culture"] = 0.8, ["Art"] = 0.2 },
            ["place_of_worship"] = new() { ["Culture"] = 0.7, ["Art"] = 0.3 },
            ["library"] = new() { ["Culture"] = 0.8, ["Art"] = 0.2 },

            // Nature & Outdoors
            ["park"] = new() { ["Nature"] = 1.0 },
            ["natural_feature"] = new() { ["Nature"] = 1.0 },
            ["campground"] = new() { ["Nature"] = 0.9, ["Sports"] = 0.1 },
            ["rv_park"] = new() { ["Nature"] = 0.8 },
            ["hiking_area"] = new() { ["Nature"] = 0.7, ["Sports"] = 0.3 },
            ["zoo"] = new() { ["Nature"] = 0.7, ["Culture"] = 0.3 },
            ["aquarium"] = new() { ["Nature"] = 0.7, ["Culture"] = 0.3 },
            ["botanical_garden"] = new() { ["Nature"] = 0.9, ["Art"] = 0.1 },
            ["beach"] = new() { ["Nature"] = 0.9, ["Sports"] = 0.1 },
            ["mountain"] = new() { ["Nature"] = 1.0 },
            ["lake"] = new() { ["Nature"] = 1.0 },
            ["river"] = new() { ["Nature"] = 1.0 },

            // Nightlife & Entertainment
            ["night_club"] = new() { ["Nightlife"] = 1.0 },
            ["casino"] = new() { ["Nightlife"] = 0.9, ["Culture"] = 0.1 },
            ["movie_theater"] = new() { ["Nightlife"] = 0.6, ["Culture"] = 0.4 },
            ["bowling_alley"] = new() { ["Nightlife"] = 0.5, ["Sports"] = 0.5 },
            ["amusement_park"] = new() { ["Nightlife"] = 0.6, ["Nature"] = 0.4 },
            ["stadium"] = new() { ["Sports"] = 0.7, ["Nightlife"] = 0.3 },
            ["performing_arts_theater"] = new() { ["Art"] = 0.8, ["Nightlife"] = 0.2 },

            // Shopping
            ["shopping_mall"] = new() { ["Shopping"] = 1.0 },
            ["store"] = new() { ["Shopping"] = 1.0 },
            ["clothing_store"] = new() { ["Shopping"] = 1.0 },
            ["shoe_store"] = new() { ["Shopping"] = 1.0 },
            ["jewelry_store"] = new() { ["Shopping"] = 0.9, ["Art"] = 0.1 },
            ["book_store"] = new() { ["Shopping"] = 0.7, ["Culture"] = 0.3 },
            ["electronics_store"] = new() { ["Shopping"] = 1.0 },
            ["furniture_store"] = new() { ["Shopping"] = 1.0 },
            ["home_goods_store"] = new() { ["Shopping"] = 1.0 },
            ["supermarket"] = new() { ["Shopping"] = 1.0 },
            ["convenience_store"] = new() { ["Shopping"] = 1.0 },
            ["market"] = new() { ["Shopping"] = 0.8, ["Culture"] = 0.2 },
            ["bazaar"] = new() { ["Shopping"] = 0.8, ["Culture"] = 0.2 },

            // Art & Creativity
            ["art_studio"] = new() { ["Art"] = 1.0 },
            ["cultural_center"] = new() { ["Art"] = 0.6, ["Culture"] = 0.4 },
            ["art"] = new() { ["Art"] = 1.0 },
            ["gallery"] = new() { ["Art"] = 1.0 },
            ["theater"] = new() { ["Art"] = 0.8, ["Nightlife"] = 0.2 },
            ["opera_house"] = new() { ["Art"] = 0.9, ["Culture"] = 0.1 },
            ["concert_hall"] = new() { ["Art"] = 0.7, ["Nightlife"] = 0.3 },

            // Wellness & Health
            ["spa"] = new() { ["Wellness"] = 1.0 },
            ["gym"] = new() { ["Wellness"] = 0.7, ["Sports"] = 0.3 },
            ["fitness_center"] = new() { ["Wellness"] = 0.7, ["Sports"] = 0.3 },
            ["yoga_studio"] = new() { ["Wellness"] = 1.0 },
            ["beauty_salon"] = new() { ["Wellness"] = 0.9 },
            ["hair_salon"] = new() { ["Wellness"] = 0.8 },
            ["wellness"] = new() { ["Wellness"] = 1.0 },
            ["health"] = new() { ["Wellness"] = 1.0 },

            // Sports & Activities
            ["sports_complex"] = new() { ["Sports"] = 1.0 },
            ["sports_club"] = new() { ["Sports"] = 1.0 },
            ["golf_course"] = new() { ["Sports"] = 1.0 },
            ["tennis_court"] = new() { ["Sports"] = 1.0 },
            ["swimming_pool"] = new() { ["Sports"] = 0.7, ["Wellness"] = 0.3 },
            ["gym"] = new() { ["Sports"] = 0.7, ["Wellness"] = 0.3 },
            ["ski_resort"] = new() { ["Sports"] = 0.8, ["Nature"] = 0.2 },
            ["marina"] = new() { ["Sports"] = 0.6, ["Nature"] = 0.4 },
        };

        public Dictionary<string, double> MapToInterests(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return new Dictionary<string, double>();

            var normalizedCategory = category.ToLowerInvariant().Trim();

            // Direct match
            if (CategoryMappings.TryGetValue(normalizedCategory, out var mapping))
            {
                return new Dictionary<string, double>(mapping);
            }

            // Partial match (contains keyword)
            foreach (var (key, value) in CategoryMappings)
            {
                if (normalizedCategory.Contains(key) || key.Contains(normalizedCategory))
                {
                    return new Dictionary<string, double>(value);
                }
            }

            // No match - return neutral scores
            return new Dictionary<string, double>();
        }

        public string? GetDominantInterest(string category)
        {
            var interests = MapToInterests(category);

            if (!interests.Any())
                return null;

            return interests.OrderByDescending(kvp => kvp.Value).First().Key;
        }

        public bool IsRecognizedCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return false;

            var normalizedCategory = category.ToLowerInvariant().Trim();

            // Direct match
            if (CategoryMappings.ContainsKey(normalizedCategory))
                return true;

            // Partial match
            return CategoryMappings.Keys.Any(key =>
                normalizedCategory.Contains(key) || key.Contains(normalizedCategory));
        }

        public List<string> GetAllInterests()
        {
            return new List<string>(Interests);
        }

        public List<string> GetExampleCategoriesForInterest(string interest)
        {
            return CategoryMappings
                .Where(kvp => kvp.Value.ContainsKey(interest))
                .OrderByDescending(kvp => kvp.Value[interest])
                .Take(5)
                .Select(kvp => kvp.Key)
                .ToList();
        }
    }
}
