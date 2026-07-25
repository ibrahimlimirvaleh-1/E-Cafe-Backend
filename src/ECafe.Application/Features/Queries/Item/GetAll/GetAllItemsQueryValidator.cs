using FluentValidation;

namespace ECafe.Application.Features.Queries.Item.GetAll
{
    public sealed class GetAllItemsQueryValidator : AbstractValidator<GetAllItemsQuery>
    {
        public GetAllItemsQueryValidator()
        {
            RuleFor(x => x.PageNumber)
                .GreaterThan(0)
                .WithMessage("PageNumber must be greater than 0.");

            RuleFor(x => x.PageSize)
                .InclusiveBetween(1, 100)
                .WithMessage("PageSize must be between 1 and 100.");

            RuleFor(x => x.RestaurantId)
                .GreaterThanOrEqualTo(0)
                .WithMessage("RestaurantId cannot be negative.");

            RuleFor(x => x.CategoryId)
                .GreaterThanOrEqualTo(0)
                .WithMessage("CategoryId cannot be negative.");

            RuleFor(x => x.StatusId)
                .GreaterThanOrEqualTo(0)
                .WithMessage("StatusId cannot be negative.");
        }
    }
}
