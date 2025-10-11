using Microsoft.Extensions.Logging;
using System.Globalization;
using WhatShouldIDo.Application.DTOs.Prompt;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.Infrastructure.Services
{
    public class BasicPromptInterpreter : IPromptInterpreter
    {
        private readonly ILogger<BasicPromptInterpreter> _logger;

        // Enhanced food/restaurant keywords for EN/TR
        private static readonly string[] FoodKeywords =
        {
            "eat", "food", "hungry", "meal", "lunch", "dinner", "breakfast", "brunch",
            "yemek", "ye", "aç", "acıktım", "karnım", "kahvaltı", "öğle", "akşam"
        };

        private static readonly string[] CuisineTypes =
        {
            // Turkish
            "kebap", "kebab", "döner", "doner", "lahmacun", "pide", "balık", "balik", "fish",
            "meze", "rakı", "raki", "türk", "turk", "turkish", "ottoman",
            // International
            "pizza", "burger", "sushi", "chinese", "italian", "mexican", "indian", "japanese",
            "korean", "thai", "vietnamese", "cafe", "coffee", "kahve", "çay", "tea"
        };

        private static readonly string[] RestaurantCategories =
        {
            "restaurant", "cafe", "bistro", "eatery", "diner", "fast_food", "food_court"
        };

        public BasicPromptInterpreter(ILogger<BasicPromptInterpreter> logger)
        {
            _logger = logger;
        }

        public Task<InterpretedPrompt> InterpretAsync(string promptText)
        {
            if (string.IsNullOrWhiteSpace(promptText))
            {
                return Task.FromResult(GetDefaultFoodQuery());
            }

            // Normalize: lowercase, trim, remove extra spaces
            var cleaned = NormalizePrompt(promptText);
            var tokens = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // Extract components
            var pricePrefs = ExtractPricePreferences(cleaned);
            var location = ExtractLocation(cleaned);
            var cuisines = ExtractCuisines(tokens, cleaned);
            var normalizedQuery = BuildNormalizedQuery(cleaned, tokens, cuisines);

            var result = new InterpretedPrompt
            {
                TextQuery = normalizedQuery,
                LocationText = location,
                PricePreferences = pricePrefs.ToArray()
            };

            _logger.LogInformation(
                "Prompt interpreted → Original: '{original}' | Normalized: '{normalized}' | Location: {loc} | Cuisines: [{cuisines}] | Price: {price}",
                promptText,
                normalizedQuery,
                location ?? "null",
                string.Join(", ", cuisines),
                string.Join(",", result.PricePreferences));

            return Task.FromResult(result);
        }

        private static string NormalizePrompt(string prompt)
        {
            // Convert to lowercase
            var normalized = prompt.ToLowerInvariant();

            // Remove common filler words
            var fillerWords = new[] { "i want", "i want a", "i want to", "istiyorum", "isterim", "lütfen", "please" };
            foreach (var filler in fillerWords)
            {
                normalized = normalized.Replace(filler, " ");
            }

            // Fix common typos
            normalized = normalized.Replace("a eat", "eat");
            normalized = normalized.Replace("a drink", "drink");

            // Normalize whitespace
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ").Trim();

            return normalized;
        }

        private static List<string> ExtractPricePreferences(string cleaned)
        {
            var pricePrefs = new List<string>();

            if (cleaned.Contains("ucuz") || cleaned.Contains("ekonomik") || cleaned.Contains("cheap") || cleaned.Contains("inexpensive"))
                pricePrefs.Add("PRICE_LEVEL_INEXPENSIVE");

            if (cleaned.Contains("orta fiyat") || cleaned.Contains("makul") || cleaned.Contains("moderate") || cleaned.Contains("mid-range"))
                pricePrefs.Add("PRICE_LEVEL_MODERATE");

            if (cleaned.Contains("lüks") || cleaned.Contains("pahalı") || cleaned.Contains("expensive") || cleaned.Contains("luxury") || cleaned.Contains("fine dining"))
                pricePrefs.Add("PRICE_LEVEL_EXPENSIVE");

            return pricePrefs;
        }

        private static string? ExtractLocation(string cleaned)
        {
            // Extended location list - can be expanded or moved to config
            var locationTags = new[]
            {
                "kadıköy", "kadiköy", "kadikoy",
                "üsküdar", "uskudar",
                "taksim",
                "ümraniye", "umraniye",
                "beşiktaş", "besiktas",
                "şişli", "sisli",
                "beyoğlu", "beyoglu",
                "fatih",
                "bakırköy", "bakirkoy",
                "maltepe",
                "kartal",
                "pendik"
            };

            foreach (var loc in locationTags)
            {
                if (cleaned.Contains(loc))
                {
                    return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(loc);
                }
            }

            return null;
        }

        private static List<string> ExtractCuisines(string[] tokens, string cleaned)
        {
            var cuisines = new List<string>();

            // Check for specific cuisine types
            foreach (var cuisine in CuisineTypes)
            {
                if (cleaned.Contains(cuisine))
                {
                    cuisines.Add(cuisine);
                }
            }

            return cuisines;
        }

        private static string BuildNormalizedQuery(string cleaned, string[] tokens, List<string> cuisines)
        {
            // If specific cuisines found, use them
            if (cuisines.Count > 0)
            {
                return string.Join(" ", cuisines);
            }

            // Check if this is a food-related query
            var isFoodQuery = FoodKeywords.Any(keyword => cleaned.Contains(keyword));

            if (isFoodQuery)
            {
                // Extract meaningful words (skip filler words and food keywords)
                var meaningfulWords = tokens
                    .Where(t => t.Length > 2 && !FoodKeywords.Contains(t))
                    .Take(3)
                    .ToList();

                if (meaningfulWords.Count > 0)
                {
                    // Combine with restaurant category
                    return $"restaurant {string.Join(" ", meaningfulWords)}";
                }

                // Default to restaurant search
                return "restaurant cafe";
            }

            // If not food-related, clean up and return meaningful parts
            var cleanedTokens = tokens.Where(t => t.Length > 2).Take(5);
            return string.Join(" ", cleanedTokens);
        }

        private static InterpretedPrompt GetDefaultFoodQuery()
        {
            // Fallback when prompt is empty or unclear
            return new InterpretedPrompt
            {
                TextQuery = "restaurant cafe pizza burger",
                LocationText = null,
                PricePreferences = Array.Empty<string>()
            };
        }
    }
}
