using FluentValidation;

namespace ECafe.Application.Features.Queries.Geocoding;

public sealed class GeocodeAddressQueryValidator : AbstractValidator<GeocodeAddressQuery>
{
    public GeocodeAddressQueryValidator()
    {
        RuleFor(x => x.Address)
            .NotEmpty()
            .WithMessage("Məkan yazın və yenidən yoxlayın.")
            .MaximumLength(300)
            .WithMessage("Məkan 300 simvoldan uzun olmamalıdır.");
    }
}
