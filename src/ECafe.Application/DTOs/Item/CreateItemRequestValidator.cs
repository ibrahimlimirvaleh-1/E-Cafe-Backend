using ECafe.Domain.Enums;
using FluentValidation;

namespace ECafe.Application.DTOs.Item
{
    public sealed class CreateItemRequestValidator : AbstractValidator<CreateItemRequest>
    {
        public CreateItemRequestValidator()
        {
            RuleFor(x => x.RestaurantId)
                .GreaterThan(0).WithMessage("RestaurantId must be greater than 0.");

            RuleFor(x => x.CategoryId)
                .GreaterThan(0).WithMessage("CategoryId must be greater than 0.");

            RuleFor(x => x.StatusId)
                .Must(BeValidItemStatus).WithMessage("StatusId is not a valid item status.");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(150).WithMessage("Name must be at most 150 characters.");

            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("Description must be at most 500 characters.")
                .When(x => x.Description is not null);

            RuleFor(x => x.BasePrice)
                .GreaterThan(0).WithMessage("BasePrice must be greater than 0.");

            RuleFor(x => x.UnavailableReason)
                .MaximumLength(500).WithMessage("UnavailableReason must be at most 500 characters.")
                .When(x => x.UnavailableReason is not null);

            RuleFor(x => x.SalesCount)
                .GreaterThanOrEqualTo(0).WithMessage("SalesCount cannot be negative.");

            RuleFor(x => x.FileId)
                .GreaterThan(0)
                .When(x => x.FileId.HasValue)
                .WithMessage("FileId must be greater than 0.");
        }

        private static bool BeValidItemStatus(int statusId)
        {
            const int itemStatusTypeId = (int)StatusType.ItemStatus;
            return Enum.GetValues<ItemStatus>()
                .Select(status => (itemStatusTypeId * 1000) + Convert.ToInt32(status))
                .Contains(statusId);
        }
    }
}
