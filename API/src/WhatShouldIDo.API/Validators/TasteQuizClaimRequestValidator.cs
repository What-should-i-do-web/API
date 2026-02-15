using FluentValidation;
using WhatShouldIDo.Application.DTOs.Requests;

namespace WhatShouldIDo.API.Validators
{
    /// <summary>
    /// Validator for taste quiz claim requests.
    /// </summary>
    public class TasteQuizClaimRequestValidator : AbstractValidator<TasteQuizClaimRequest>
    {
        public TasteQuizClaimRequestValidator()
        {
            RuleFor(x => x.ClaimToken)
                .NotEmpty()
                .WithMessage("Claim token is required")
                .MinimumLength(32)
                .WithMessage("Claim token appears to be invalid (too short)")
                .MaximumLength(100)
                .WithMessage("Claim token must not exceed 100 characters");
        }
    }
}
