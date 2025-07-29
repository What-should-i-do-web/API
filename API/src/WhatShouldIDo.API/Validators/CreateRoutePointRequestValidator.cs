using FluentValidation;
using WhatShouldIDo.Application.DTOs.Requests;

namespace WhatShouldIDo.API.Validators
{
    public class CreateRoutePointRequestValidator : AbstractValidator<CreateRoutePointRequest>
    {
        public CreateRoutePointRequestValidator()
        {
            RuleFor(x => x.RouteId)
                .NotEmpty().WithMessage("RouteId is required.");

            RuleFor(x => x.Latitude)
                .InclusiveBetween(-90, 90).WithMessage("Latitude must be between -90 and 90.");

            RuleFor(x => x.Longitude)
                .InclusiveBetween(-180, 180).WithMessage("Longitude must be between -180 and 180.");

            RuleFor(x => x.Order)
                .GreaterThanOrEqualTo(0).WithMessage("Order must be non-negative.");
        }
    }
}

