using FluentValidation;

namespace ECafe.Application.Features.Queries.Restaurant.Public
{
    public class GetPublicRestaurantsQueryValidator : AbstractValidator<GetPublicRestaurantsQuery>
    {
        public GetPublicRestaurantsQueryValidator()
        {
            RuleFor(x => x.PageNumber)
                .GreaterThanOrEqualTo(0)
                .WithMessage("PageNumber must be greater than or equal to 0.");

            RuleFor(x => x.PageSize)
                .InclusiveBetween(1, 100)
                .WithMessage("PageSize must be between 1 and 100.")
                .When(x => x.PageSize > 0);
        }
    }
}
