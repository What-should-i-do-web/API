using FluentValidation;
using WhatShouldIDo.Application.DTOs.Requests;

namespace WhatShouldIDo.API.Validators
{
    /// <summary>
    /// Validator for taste profile update requests.
    /// </summary>
    public class UpdateTasteProfileRequestValidator : AbstractValidator<UpdateTasteProfileRequest>
    {
        private static readonly string[] ValidWeightKeys = new[]
        {
            "CultureWeight", "FoodWeight", "NatureWeight", "NightlifeWeight",
            "ShoppingWeight", "ArtWeight", "WellnessWeight", "SportsWeight",
            "TasteQualityWeight", "AtmosphereWeight", "DesignWeight",
            "CalmnessWeight", "SpaciousnessWeight", "NoveltyTolerance"
        };

        public UpdateTasteProfileRequestValidator()
        {
            RuleFor(x => x.Weights)
                .NotNull()
                .WithMessage("Weights are required")
                .Must(weights => weights != null && weights.Count > 0)
                .WithMessage("At least one weight must be provided");

            RuleForEach(x => x.Weights.Keys)
                .Must(key => ValidWeightKeys.Contains(key))
                .WithMessage(key => $"Invalid weight key: {key}. Must be one of: {string.Join(", ", ValidWeightKeys)}");

            RuleForEach(x => x.Weights.Values)
                .InclusiveBetween(0.0, 1.0)
                .WithMessage("Weight values must be between 0.0 and 1.0");
        }
    }
}
