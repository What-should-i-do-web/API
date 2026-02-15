using FluentValidation;
using WhatShouldIDo.Application.DTOs.Requests;

namespace WhatShouldIDo.API.Validators
{
    /// <summary>
    /// Validator for taste quiz submission requests.
    /// </summary>
    public class TasteQuizSubmitRequestValidator : AbstractValidator<TasteQuizSubmitRequest>
    {
        public TasteQuizSubmitRequestValidator()
        {
            RuleFor(x => x.QuizVersion)
                .NotEmpty()
                .WithMessage("Quiz version is required")
                .MaximumLength(20)
                .WithMessage("Quiz version must not exceed 20 characters");

            RuleFor(x => x.Answers)
                .NotNull()
                .WithMessage("Answers are required")
                .Must(answers => answers != null && answers.Count >= 3)
                .WithMessage("At least 3 quiz answers are required");

            RuleForEach(x => x.Answers.Keys)
                .NotEmpty()
                .WithMessage("Step ID cannot be empty")
                .MaximumLength(50)
                .WithMessage("Step ID must not exceed 50 characters");

            RuleForEach(x => x.Answers.Values)
                .NotEmpty()
                .WithMessage("Option ID cannot be empty")
                .MaximumLength(50)
                .WithMessage("Option ID must not exceed 50 characters");
        }
    }
}
