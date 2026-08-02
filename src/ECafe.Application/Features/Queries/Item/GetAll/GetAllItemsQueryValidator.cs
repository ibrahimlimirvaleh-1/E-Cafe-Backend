using FluentValidation;

namespace ECafe.Application.Features.Queries.Item.GetAll
{
    public sealed class GetAllItemsQueryValidator : AbstractValidator<GetAllItemsQuery>
    {
        public GetAllItemsQueryValidator()
        {
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
