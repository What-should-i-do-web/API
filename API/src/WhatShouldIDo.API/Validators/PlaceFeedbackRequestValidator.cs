using FluentValidation;
using WhatShouldIDo.Application.DTOs.Requests;

namespace WhatShouldIDo.API.Validators
{
    /// <summary>
    /// Validator for place feedback requests.
    /// </summary>
    public class PlaceFeedbackRequestValidator : AbstractValidator<PlaceFeedbackRequest>
    {
        private static readonly string[] ValidFeedbackTypes = { "like", "dislike", "skip" };

        public PlaceFeedbackRequestValidator()
        {
            RuleFor(x => x.PlaceId)
                .NotEmpty()
                .WithMessage("Place ID is required")
                .MaximumLength(200)
                .WithMessage("Place ID must not exceed 200 characters");

            RuleFor(x => x.PlaceCategory)
                .NotEmpty()
                .WithMessage("Place category is required")
                .MaximumLength(200)
                .WithMessage("Place category must not exceed 200 characters");

            RuleFor(x => x.FeedbackType)
                .NotEmpty()
                .WithMessage("Feedback type is required")
                .Must(type => ValidFeedbackTypes.Contains(type.ToLower()))
                .WithMessage($"Feedback type must be one of: {string.Join(", ", ValidFeedbackTypes)}");
        }
    }
}
