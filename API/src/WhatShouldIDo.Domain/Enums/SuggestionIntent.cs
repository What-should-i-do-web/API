namespace WhatShouldIDo.Domain.Enums
{
    /// <summary>
    /// Represents the user's intent when requesting suggestions.
    /// Drives orchestration logic and determines response format.
    /// </summary>
    public enum SuggestionIntent
    {
        /// <summary>
        /// Quick single suggestion for immediate decision ("Bana öner")
        /// Returns: Single or few suggestions, no route
        /// </summary>
        QUICK_SUGGESTION = 0,

        /// <summary>
        /// Food/dining only - restaurants, cafes, dessert places
        /// Returns: Only food-related suggestions, no route
        /// Constraint: No non-food categories
        /// </summary>
        FOOD_ONLY = 1,

        /// <summary>
        /// Activities only - fun, entertainment, sports, cultural
        /// Returns: Only activity suggestions, no route
        /// Constraint: No food/restaurant categories
        /// </summary>
        ACTIVITY_ONLY = 2,

        /// <summary>
        /// Multi-stop day plan with category diversity and time ordering
        /// Returns: RouteDto or DayPlanDto with optimized stops
        /// Constraint: Walking distance, time windows, category variety
        /// </summary>
        ROUTE_PLANNING = 3,

        /// <summary>
        /// Try something new based on user history and profile
        /// Returns: Novel activity suggestions with safety constraints
        /// Constraint: Must satisfy novelty, walkability, budget, no dislikes
        /// </summary>
        TRY_SOMETHING_NEW = 4
    }

    /// <summary>
    /// Extension methods for SuggestionIntent
    /// </summary>
    public static class SuggestionIntentExtensions
    {
        /// <summary>
        /// Determines if this intent should build a route/day plan
        /// </summary>
        public static bool RequiresRoute(this SuggestionIntent intent)
        {
            return intent == SuggestionIntent.ROUTE_PLANNING;
        }

        /// <summary>
        /// Determines if this intent should apply category restrictions
        /// </summary>
        public static bool HasCategoryRestrictions(this SuggestionIntent intent)
        {
            return intent == SuggestionIntent.FOOD_ONLY || intent == SuggestionIntent.ACTIVITY_ONLY;
        }

        /// <summary>
        /// Determines if this intent emphasizes novelty/discovery
        /// </summary>
        public static bool EmphasizesNovelty(this SuggestionIntent intent)
        {
            return intent == SuggestionIntent.TRY_SOMETHING_NEW;
        }

        /// <summary>
        /// Gets the maximum number of suggestions to return
        /// </summary>
        public static int GetMaxSuggestions(this SuggestionIntent intent)
        {
            return intent switch
            {
                SuggestionIntent.QUICK_SUGGESTION => 3,
                SuggestionIntent.FOOD_ONLY => 10,
                SuggestionIntent.ACTIVITY_ONLY => 10,
                SuggestionIntent.ROUTE_PLANNING => 8,
                SuggestionIntent.TRY_SOMETHING_NEW => 5,
                _ => 10
            };
        }

        /// <summary>
        /// Gets allowed categories for restricted intents
        /// </summary>
        public static string[] GetAllowedCategories(this SuggestionIntent intent)
        {
            return intent switch
            {
                SuggestionIntent.FOOD_ONLY => new[]
                {
                    "restaurant", "cafe", "bar", "bakery", "meal_takeaway",
                    "meal_delivery", "food", "dessert", "coffee", "breakfast",
                    "lunch", "dinner", "brunch"
                },
                SuggestionIntent.ACTIVITY_ONLY => new[]
                {
                    "amusement_park", "aquarium", "art_gallery", "bowling_alley",
                    "casino", "movie_theater", "museum", "night_club", "park",
                    "spa", "stadium", "tourist_attraction", "zoo", "gym",
                    "shopping_mall", "library", "theater", "concert_hall"
                },
                _ => Array.Empty<string>()
            };
        }

        /// <summary>
        /// Gets display name for the intent
        /// </summary>
        public static string ToDisplayName(this SuggestionIntent intent)
        {
            return intent switch
            {
                SuggestionIntent.QUICK_SUGGESTION => "Quick Suggestion",
                SuggestionIntent.FOOD_ONLY => "Food & Dining",
                SuggestionIntent.ACTIVITY_ONLY => "Activities & Entertainment",
                SuggestionIntent.ROUTE_PLANNING => "Day Plan / Route",
                SuggestionIntent.TRY_SOMETHING_NEW => "Try Something New",
                _ => intent.ToString()
            };
        }
    }
}
