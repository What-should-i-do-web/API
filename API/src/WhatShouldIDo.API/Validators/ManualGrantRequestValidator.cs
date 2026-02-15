using FluentValidation;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Domain.Enums;

namespace WhatShouldIDo.API.Validators
{
    /// <summary>
    /// Validator for ManualGrantRequest (admin-only operation)
    /// </summary>
    public class ManualGrantRequestValidator : AbstractValidator<ManualGrantRequest>
    {
        public ManualGrantRequestValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty()
                .WithMessage("UserId is required");

            RuleFor(x => x.Plan)
                .IsInEnum()
                .WithMessage("Plan must be a valid subscription plan")
                .Must(p => p != SubscriptionPlan.Free)
                .WithMessage("Cannot manually grant Free plan");

            RuleFor(x => x.ExpiresAtUtc)
                .NotEmpty()
                .WithMessage("ExpiresAtUtc is required")
                .GreaterThan(DateTime.UtcNow)
                .WithMessage("ExpiresAtUtc must be in the future");

            RuleFor(x => x.Notes)
                .NotEmpty()
                .WithMessage("Notes is required for audit trail")
                .MinimumLength(5)
                .WithMessage("Notes must be at least 5 characters")
                .MaximumLength(500)
                .WithMessage("Notes cannot exceed 500 characters")
                .Must(notes => !ContainsPotentialPii(notes))
                .WithMessage("Notes should not contain email addresses or phone numbers");
        }

        private static bool ContainsPotentialPii(string notes)
        {
            if (string.IsNullOrEmpty(notes))
                return false;

            // Basic check for email pattern
            if (notes.Contains('@') && notes.Contains('.'))
                return true;

            // Basic check for phone number patterns (10+ consecutive digits)
            var digitCount = 0;
            foreach (var c in notes)
            {
                if (char.IsDigit(c))
                {
                    digitCount++;
                    if (digitCount >= 10)
                        return true;
                }
                else
                {
                    digitCount = 0;
                }
            }

            return false;
        }
    }
}
