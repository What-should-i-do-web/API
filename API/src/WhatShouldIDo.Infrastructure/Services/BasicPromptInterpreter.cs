using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using System.Globalization;
using WhatShouldIDo.Application.DTOs.Prompt;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.Infrastructure.Services
{
    public class BasicPromptInterpreter : IPromptInterpreter
    {
        private readonly ILogger<BasicPromptInterpreter> _logger;

        public BasicPromptInterpreter(ILogger<BasicPromptInterpreter> logger)
        {
            _logger = logger;
        }

        public Task<InterpretedPrompt> InterpretAsync(string promptText)
        {
            // Lowercase ve sadeleştir
            var cleanedPrompt = promptText.ToLowerInvariant();

            // Basit eşleşme kuralları
            var pricePrefs = new List<string>();
            if (cleanedPrompt.Contains("ucuz") || cleanedPrompt.Contains("ekonomik"))
                pricePrefs.Add("PRICE_LEVEL_INEXPENSIVE");
            if (cleanedPrompt.Contains("orta fiyat") || cleanedPrompt.Contains("makul"))
                pricePrefs.Add("PRICE_LEVEL_MODERATE");
            if (cleanedPrompt.Contains("lüks") || cleanedPrompt.Contains("pahalı"))
                pricePrefs.Add("PRICE_LEVEL_EXPENSIVE");

            // Basit lokasyon çıkarımı (örnek)
            string? location = null;
            var locationTags = new[] { "kadıköy", "üsküdar", "taksim", "ümraniye", "beşiktaş" };
            foreach (var loc in locationTags)
            {
                if (cleanedPrompt.Contains(loc))
                {
                    location = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(loc);
                    break;
                }
            }

            // Query → direkt prompt’ın kendisi (ileride refine edilir)
            var result = new InterpretedPrompt
            {
                TextQuery = promptText, // şimdilik olduğu gibi gönder
                LocationText = location,
                PricePreferences = pricePrefs.ToArray()
            };

            _logger.LogInformation("Prompt interpreted → Query: {query}, Location: {loc}, Price: {price}",
                result.TextQuery, result.LocationText, string.Join(",", result.PricePreferences));

            return Task.FromResult(result);
        }

      
    }
}
