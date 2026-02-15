using WhatShouldIDo.Domain.Enums;

namespace WhatShouldIDo.Application.Models
{
    /// <summary>
    /// Application layer output model for suggestions.
    /// Used by Commands/Handlers - NOT an API DTO.
    /// </summary>
    public sealed record SuggestionsResult(
        SuggestionIntent Intent,
        bool IsPersonalized,
        Guid? UserId,
        IReadOnlyList<SuggestionItem> Suggestions,
        RouteResult? Route,
        DayPlanResult? DayPlan,
        int TotalCount,
        FilterInfo Filters,
        SuggestionMeta Metadata
    );

    /// <summary>
    /// Individual suggestion item in Application layer
    /// </summary>
    public sealed record SuggestionItem(
        Guid Id,
        string PlaceName,
        float Latitude,
        float Longitude,
        string Category,
        string Source,
        string Reason,
        double Score,
        DateTime CreatedAt,
        bool IsSponsored,
        DateTime? SponsoredUntil,
        string? PhotoReference,
        string? PhotoUrl,
        IReadOnlyList<string> Reasons
    );

    /// <summary>
    /// Route result in Application layer
    /// </summary>
    public sealed record RouteResult(
        Guid Id,
        string Name,
        string? Description,
        Guid UserId,
        double TotalDistance,
        double EstimatedDuration,
        int StopCount,
        string TransportationMode,
        string RouteType,
        IReadOnlyList<string> Tags,
        bool IsPublic,
        DateTime CreatedAt,
        DateTime UpdatedAt
    );

    /// <summary>
    /// Day plan result in Application layer
    /// </summary>
    public sealed record DayPlanResult(
        Guid Id,
        string Name,
        DateTime Date,
        IReadOnlyList<DayPlanStop> Stops,
        int TotalDurationMinutes,
        double TotalDistanceMeters
    );

    /// <summary>
    /// Individual stop in a day plan
    /// </summary>
    public sealed record DayPlanStop(
        int Order,
        Guid PlaceId,
        string PlaceName,
        TimeSpan ArrivalTime,
        int DurationMinutes,
        string? Notes
    );

    /// <summary>
    /// Filter information for the suggestion result
    /// </summary>
    public sealed record FilterInfo(
        int RadiusMeters,
        int? WalkingDistanceMeters,
        string? BudgetLevel,
        IReadOnlyList<string> IncludedCategories,
        IReadOnlyList<string> ExcludedCategories,
        IReadOnlyList<string> DietaryRestrictions,
        bool AppliedVariety,
        bool AppliedContextual
    );

    /// <summary>
    /// Metadata about suggestion generation
    /// </summary>
    public sealed record SuggestionMeta(
        DateTime GeneratedAt,
        string Source,
        double DiversityFactor,
        bool UsedAI,
        bool UsedPersonalization,
        bool UsedContextEngine,
        bool UsedVariabilityEngine,
        string? TimeOfDay,
        string? WeatherCondition,
        string? Season
    );
}
