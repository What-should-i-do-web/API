using FluentValidation;
using WhatShouldIDo.API.DTOs.Request;
using WhatShouldIDo.Domain.Enums;

namespace WhatShouldIDo.API.Validators
{
    /// <summary>
    /// Validator for CreateSuggestionsRequest using FluentValidation.
    /// Enforces intent-specific rules and constraints.
    /// </summary>
    public class CreateSuggestionsRequestValidator : AbstractValidator<CreateSuggestionsRequest>
    {
        public CreateSuggestionsRequestValidator()
        {
            RuleFor(x => x.Intent)
                .IsInEnum()
                .WithMessage("Invalid intent value");

            RuleFor(x => x.Latitude)
                .InclusiveBetween(-90, 90)
                .WithMessage("Latitude must be between -90 and 90");

            RuleFor(x => x.Longitude)
                .InclusiveBetween(-180, 180)
                .WithMessage("Longitude must be between -180 and 180");

            RuleFor(x => x.RadiusMeters)
                .InclusiveBetween(100, 50000)
                .WithMessage("Radius must be between 100 and 50,000 meters");

            // Intent-specific validation
            When(x => x.Intent == SuggestionIntent.ROUTE_PLANNING, () =>
            {
                RuleFor(x => x.WalkingDistanceMeters)
                    .NotNull()
                    .WithMessage("Walking distance is required for route planning")
                    .InclusiveBetween(500, 10000)
                    .WithMessage("Walking distance must be between 500 and 10,000 meters");
            });

            When(x => x.WalkingDistanceMeters.HasValue, () =>
            {
                RuleFor(x => x.WalkingDistanceMeters!.Value)
                    .InclusiveBetween(500, 10000)
                    .WithMessage("Walking distance must be between 500 and 10,000 meters when specified");
            });

            When(x => !string.IsNullOrWhiteSpace(x.BudgetLevel), () =>
            {
                RuleFor(x => x.BudgetLevel)
                    .Must(budget => new[] { "FREE", "INEXPENSIVE", "MODERATE", "EXPENSIVE", "VERY_EXPENSIVE" }
                        .Contains(budget!.ToUpperInvariant()))
                    .WithMessage("Budget level must be one of: FREE, INEXPENSIVE, MODERATE, EXPENSIVE, VERY_EXPENSIVE");
            });

            When(x => x.IncludeCategories != null && x.IncludeCategories.Any(), () =>
            {
                RuleFor(x => x.IncludeCategories!)
                    .Must(categories => categories.Count <= 10)
                    .WithMessage("Cannot include more than 10 categories");
            });

            When(x => x.ExcludeCategories != null && x.ExcludeCategories.Any(), () =>
            {
                RuleFor(x => x.ExcludeCategories!)
                    .Must(categories => categories.Count <= 10)
                    .WithMessage("Cannot exclude more than 10 categories");
            });

            When(x => x.DietaryRestrictions != null && x.DietaryRestrictions.Any(), () =>
            {
                RuleFor(x => x.DietaryRestrictions!)
                    .Must(restrictions => restrictions.Count <= 5)
                    .WithMessage("Cannot specify more than 5 dietary restrictions");
            });

            When(x => !string.IsNullOrWhiteSpace(x.AreaName), () =>
            {
                RuleFor(x => x.AreaName!)
                    .MaximumLength(100)
                    .WithMessage("Area name cannot exceed 100 characters");
            });
        }
    }
}
