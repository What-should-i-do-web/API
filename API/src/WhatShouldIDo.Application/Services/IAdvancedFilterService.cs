using WhatShouldIDo.Application.DTOs.Request;
using WhatShouldIDo.Application.DTOs.Response;

namespace WhatShouldIDo.Application.Services;

public interface IAdvancedFilterService
{
    Task<IEnumerable<SuggestionDto>> ApplyFiltersAsync(IEnumerable<SuggestionDto> suggestions, FilterCriteria criteria);
    Task<FilterCriteria> GetSmartFiltersAsync(double latitude, double longitude, string? userHash = null);
    Task<Dictionary<string, object>> GetFilterStatisticsAsync(IEnumerable<SuggestionDto> suggestions);
    Task<FilterCriteria> OptimizeFiltersAsync(FilterCriteria criteria, int targetResultCount = 20);
    bool ValidateFilters(FilterCriteria criteria, out List<string> errors);
    Task<IEnumerable<string>> GetPopularFiltersAsync(string? userHash = null);
    Task<FilterCriteria> GetRecommendedFiltersAsync(string? userHash, double? latitude = null, double? longitude = null);
}