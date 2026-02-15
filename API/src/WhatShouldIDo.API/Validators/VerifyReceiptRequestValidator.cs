using FluentValidation;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Domain.Enums;

namespace WhatShouldIDo.API.Validators
{
    /// <summary>
    /// Validator for VerifyReceiptRequest
    /// </summary>
    public class VerifyReceiptRequestValidator : AbstractValidator<VerifyReceiptRequest>
    {
        public VerifyReceiptRequestValidator()
        {
            RuleFor(x => x.Provider)
                .IsInEnum()
                .WithMessage("Provider must be a valid subscription provider")
                .Must(p => p != SubscriptionProvider.None)
                .WithMessage("Provider cannot be None for verification")
                .Must(p => p != SubscriptionProvider.Manual)
                .WithMessage("Manual provider cannot be used for receipt verification");

            RuleFor(x => x.Plan)
                .IsInEnum()
                .WithMessage("Plan must be a valid subscription plan")
                .Must(p => p != SubscriptionPlan.Free)
                .WithMessage("Cannot verify a free plan receipt");

            RuleFor(x => x.ReceiptData)
                .NotEmpty()
                .WithMessage("ReceiptData is required")
                .MinimumLength(1)
                .WithMessage("ReceiptData cannot be empty")
                .MaximumLength(50000) // Receipts can be large but should have a limit
                .WithMessage("ReceiptData is too large");
        }
    }
}
