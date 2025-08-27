using WhatShouldIDo.Application.DTOs.Response;

namespace WhatShouldIDo.Application.Services;

public interface ILocalizationService
{
    Task<string> GetLocalizedTextAsync(string key, string culture = "en-US");
    Task<SuggestionDto> LocalizeSuggestionAsync(SuggestionDto suggestion, string culture);
    Task<IEnumerable<SuggestionDto>> LocalizeSuggestionsAsync(IEnumerable<SuggestionDto> suggestions, string culture);
    string GetUserCultureFromRequest();
    bool IsSupportedCulture(string culture);
    string GetDefaultCulture();
    IEnumerable<string> GetSupportedCultures();
}