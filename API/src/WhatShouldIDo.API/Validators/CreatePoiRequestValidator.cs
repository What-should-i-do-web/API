using FluentValidation;
using WhatShouldIDo.Application.DTOs.Requests;

namespace WhatShouldIDo.API.Validators
{
    public class CreatePoiRequestValidator : AbstractValidator<CreatePoiRequest>
    {
        public CreatePoiRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("POI name is required.")
                .MaximumLength(100).WithMessage("POI name must be at most 100 characters.");

            RuleFor(x => x.Latitude)
                .InclusiveBetween(-90, 90).WithMessage("Latitude must be between -90 and 90.");

            RuleFor(x => x.Longitude)
                .InclusiveBetween(-180, 180).WithMessage("Longitude must be between -180 and 180.");
        }
    }
}
