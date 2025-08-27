using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Services;
using System.Globalization;

namespace WhatShouldIDo.Infrastructure.Services;

public class LocalizationService : ILocalizationService
{
    private readonly IStringLocalizer _localizer;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LocalizationService> _logger;

    private static readonly string[] SupportedCultures = { "en-US", "tr-TR", "es-ES", "fr-FR", "de-DE", "it-IT", "pt-PT", "ru-RU", "ja-JP", "ko-KR" };
    private const string DefaultCulture = "en-US";
    private const int CacheExpirationMinutes = 60;

    public LocalizationService(
        IStringLocalizer<LocalizationService> localizer,
        IHttpContextAccessor httpContextAccessor,
        IMemoryCache cache,
        ILogger<LocalizationService> logger)
    {
        _localizer = localizer;
        _httpContextAccessor = httpContextAccessor;
        _cache = cache;
        _logger = logger;
    }

    public async Task<string> GetLocalizedTextAsync(string key, string culture = DefaultCulture)
    {
        try
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            
            var cacheKey = $"localization_{key}_{culture}";
            if (_cache.TryGetValue(cacheKey, out string? cachedValue) && !string.IsNullOrEmpty(cachedValue))
            {
                return cachedValue;
            }

            // Set thread culture for proper localization
            var previousCulture = CultureInfo.CurrentCulture;
            var previousUICulture = CultureInfo.CurrentUICulture;
            
            try
            {
                var cultureInfo = new CultureInfo(IsSupportedCulture(culture) ? culture : DefaultCulture);
                CultureInfo.CurrentCulture = cultureInfo;
                CultureInfo.CurrentUICulture = cultureInfo;

                var localizedString = _localizer[key];
                var result = localizedString.ResourceNotFound ? key : localizedString.Value;

                _cache.Set(cacheKey, result, TimeSpan.FromMinutes(CacheExpirationMinutes));
                return result;
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUICulture;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error localizing text for key {Key} and culture {Culture}", key, culture);
            return key;
        }
    }

    public async Task<SuggestionDto> LocalizeSuggestionAsync(SuggestionDto suggestion, string culture)
    {
        try
        {
            var localizedSuggestion = new SuggestionDto
            {
                Id = suggestion.Id,
                PlaceName = suggestion.PlaceName,
                Latitude = suggestion.Latitude,
                Longitude = suggestion.Longitude,
                Category = await GetLocalizedCategoryAsync(suggestion.Category, culture),
                Source = suggestion.Source,
                Reason = await GetLocalizedReasonAsync(suggestion.Reason, culture),
                Score = suggestion.Score,
                CreatedAt = suggestion.CreatedAt,
                UserHash = suggestion.UserHash,
                IsSponsored = suggestion.IsSponsored,
                SponsoredUntil = suggestion.SponsoredUntil,
                PhotoReference = suggestion.PhotoReference,
                PhotoUrl = suggestion.PhotoUrl
            };

            return localizedSuggestion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error localizing suggestion {SuggestionId} for culture {Culture}", suggestion.Id, culture);
            return suggestion;
        }
    }

    public async Task<IEnumerable<SuggestionDto>> LocalizeSuggestionsAsync(IEnumerable<SuggestionDto> suggestions, string culture)
    {
        var tasks = suggestions.Select(s => LocalizeSuggestionAsync(s, culture));
        return await Task.WhenAll(tasks);
    }

    public string GetUserCultureFromRequest()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return DefaultCulture;

            // Check Accept-Language header
            var acceptLanguageHeader = httpContext.Request.Headers["Accept-Language"].FirstOrDefault();
            if (!string.IsNullOrEmpty(acceptLanguageHeader))
            {
                var preferredLanguages = acceptLanguageHeader.Split(',')
                    .Select(lang => lang.Split(';')[0].Trim())
                    .Where(lang => IsSupportedCulture(lang));

                var firstSupportedLanguage = preferredLanguages.FirstOrDefault();
                if (!string.IsNullOrEmpty(firstSupportedLanguage))
                {
                    return firstSupportedLanguage;
                }
            }

            // Check query parameter
            var cultureParam = httpContext.Request.Query["culture"].FirstOrDefault();
            if (!string.IsNullOrEmpty(cultureParam) && IsSupportedCulture(cultureParam))
            {
                return cultureParam;
            }

            return DefaultCulture;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining user culture from request");
            return DefaultCulture;
        }
    }

    public bool IsSupportedCulture(string culture)
    {
        return !string.IsNullOrEmpty(culture) && SupportedCultures.Contains(culture, StringComparer.OrdinalIgnoreCase);
    }

    public string GetDefaultCulture()
    {
        return DefaultCulture;
    }

    public IEnumerable<string> GetSupportedCultures()
    {
        return SupportedCultures;
    }

    private async Task<string> GetLocalizedCategoryAsync(string category, string culture)
    {
        if (string.IsNullOrEmpty(category)) return category;

        var categoryKey = $"category.{category.ToLowerInvariant()}";
        return await GetLocalizedTextAsync(categoryKey, culture);
    }

    private async Task<string> GetLocalizedReasonAsync(string reason, string culture)
    {
        if (string.IsNullOrEmpty(reason)) return reason;

        // Try to match common reason patterns and localize them
        var reasonPatterns = new Dictionary<string, string>
        {
            ["highly rated"] = "reason.highly_rated",
            ["popular"] = "reason.popular",
            ["nearby"] = "reason.nearby",
            ["trending"] = "reason.trending",
            ["recommended"] = "reason.recommended",
            ["matches preferences"] = "reason.matches_preferences",
            ["good weather"] = "reason.good_weather",
            ["seasonal"] = "reason.seasonal"
        };

        var lowerReason = reason.ToLowerInvariant();
        var matchingPattern = reasonPatterns.FirstOrDefault(kv => lowerReason.Contains(kv.Key));
        
        if (!string.IsNullOrEmpty(matchingPattern.Value))
        {
            return await GetLocalizedTextAsync(matchingPattern.Value, culture);
        }

        return reason;
    }
}