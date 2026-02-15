using WhatShouldIDo.Domain.Enums;

namespace WhatShouldIDo.Application.Models
{
    /// <summary>
    /// Application layer input model for creating suggestions.
    /// Used by Commands/Handlers - NOT an API DTO.
    /// </summary>
    public sealed record CreateSuggestionsInput(
        SuggestionIntent Intent,
        double Latitude,
        double Longitude,
        int RadiusMeters,
        string? AreaName = null,
        int? WalkingDistanceMeters = null,
        string? BudgetLevel = null,
        IReadOnlyList<string>? IncludeCategories = null,
        IReadOnlyList<string>? ExcludeCategories = null,
        IReadOnlyList<string>? DietaryRestrictions = null,
        IReadOnlyList<string>? OnboardingPreferences = null,
        string? TimeOfDay = null,
        Guid? UserId = null
    );
}
