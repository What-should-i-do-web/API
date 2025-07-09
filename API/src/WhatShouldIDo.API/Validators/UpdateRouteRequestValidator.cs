using FluentValidation;
using WhatShouldIDo.Application.DTOs.Requests;

namespace WhatShouldIDo.API.Validators
{
    public class UpdateRouteRequestValidator : AbstractValidator<UpdateRouteRequest>
    {
        public UpdateRouteRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Route name is required.")
                .MaximumLength(100).WithMessage("Route name must be at most 100 characters.");
        }
    }
}
