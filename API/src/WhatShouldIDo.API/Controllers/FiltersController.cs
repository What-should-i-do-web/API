using Microsoft.AspNetCore.Mvc;
using WhatShouldIDo.Application.DTOs.Request;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Services;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FiltersController : ControllerBase
{
    private readonly IAdvancedFilterService _filterService;
    private readonly ISuggestionService _suggestionService;

    public FiltersController(IAdvancedFilterService filterService, ISuggestionService suggestionService)
    {
        _filterService = filterService;
        _suggestionService = suggestionService;
    }

    [HttpPost("apply")]
    public async Task<ActionResult<IEnumerable<SuggestionDto>>> ApplyFilters([FromBody] ApplyFiltersRequest request)
    {
        if (!_filterService.ValidateFilters(request.Criteria, out var errors))
        {
            return BadRequest(new { errors });
        }

        try
        {
            // Get base suggestions if not provided
            IEnumerable<SuggestionDto> suggestions;
            if (request.Suggestions?.Any() == true)
            {
                suggestions = request.Suggestions;
            }
            else if (request.Criteria.Latitude.HasValue && request.Criteria.Longitude.HasValue)
            {
                suggestions = await _suggestionService.GetNearbySuggestionsAsync(
                    (float)request.Criteria.Latitude.Value,
                    (float)request.Criteria.Longitude.Value,
                    request.Criteria.Radius ?? 5000);
            }
            else
            {
                return BadRequest("Either suggestions or location (latitude/longitude) must be provided");
            }

            var filteredResults = await _filterService.ApplyFiltersAsync(suggestions, request.Criteria);
            var statistics = await _filterService.GetFilterStatisticsAsync(filteredResults);

            return Ok(new
            {
                suggestions = filteredResults,
                statistics,
                total_results = filteredResults.Count(),
                applied_filters = request.Criteria
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to apply filters", details = ex.Message });
        }
    }

    [HttpGet("smart")]
    public async Task<ActionResult<FilterCriteria>> GetSmartFilters(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] string? userHash = null)
    {
        try
        {
            var smartFilters = await _filterService.GetSmartFiltersAsync(latitude, longitude, userHash);
            return Ok(smartFilters);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to generate smart filters", details = ex.Message });
        }
    }

    [HttpGet("recommended")]
    public async Task<ActionResult<FilterCriteria>> GetRecommendedFilters(
        [FromQuery] string? userHash = null,
        [FromQuery] double? latitude = null,
        [FromQuery] double? longitude = null)
    {
        try
        {
            var recommendedFilters = await _filterService.GetRecommendedFiltersAsync(userHash, latitude, longitude);
            return Ok(recommendedFilters);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to get recommended filters", details = ex.Message });
        }
    }

    [HttpGet("popular")]
    public async Task<ActionResult<IEnumerable<string>>> GetPopularFilters([FromQuery] string? userHash = null)
    {
        try
        {
            var popularFilters = await _filterService.GetPopularFiltersAsync(userHash);
            return Ok(new { popular_filters = popularFilters });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to get popular filters", details = ex.Message });
        }
    }

    [HttpPost("validate")]
    public ActionResult ValidateFilters([FromBody] FilterCriteria criteria)
    {
        var isValid = _filterService.ValidateFilters(criteria, out var errors);
        
        return Ok(new
        {
            is_valid = isValid,
            errors = errors,
            criteria = criteria
        });
    }

    [HttpPost("optimize")]
    public async Task<ActionResult<FilterCriteria>> OptimizeFilters(
        [FromBody] OptimizeFiltersRequest request)
    {
        try
        {
            var optimizedFilters = await _filterService.OptimizeFiltersAsync(
                request.Criteria, 
                request.TargetResultCount ?? 20);
            
            return Ok(new
            {
                original_criteria = request.Criteria,
                optimized_criteria = optimizedFilters,
                target_result_count = request.TargetResultCount ?? 20
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to optimize filters", details = ex.Message });
        }
    }

    [HttpGet("categories")]
    public ActionResult<IEnumerable<string>> GetAvailableCategories()
    {
        var categories = new[]
        {
            "restaurant", "cafe", "museum", "park", "shopping", "entertainment",
            "tourist_attraction", "hotel", "hospital", "gas_station", "bank",
            "gym", "spa", "beach", "hiking", "sports", "theater", "cinema",
            "gallery", "library", "school", "pharmacy", "supermarket"
        };

        return Ok(new { categories = categories.OrderBy(c => c) });
    }

    [HttpGet("enums")]
    public ActionResult GetFilterEnums()
    {
        return Ok(new
        {
            time_of_day = Enum.GetNames<TimeOfDay>(),
            weather_conditions = Enum.GetNames<WeatherCondition>(),
            price_levels = Enum.GetNames<PriceLevel>(),
            sort_options = Enum.GetNames<SortBy>()
        });
    }
}

public class ApplyFiltersRequest
{
    public FilterCriteria Criteria { get; set; } = new();
    public List<SuggestionDto>? Suggestions { get; set; }
}

public class OptimizeFiltersRequest
{
    public FilterCriteria Criteria { get; set; } = new();
    public int? TargetResultCount { get; set; } = 20;
}