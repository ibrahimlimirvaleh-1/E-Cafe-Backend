using ECafe.Application.DTOs.RestaurantContract;
using FluentValidation;

namespace ECafe.Application.Features.Commands.RestaurantContract
{
    internal sealed class RestaurantContractDateRequestValidator<TRequest> : AbstractValidator<TRequest>
        where TRequest : CreateRestaurantContractRequest
    {
        public RestaurantContractDateRequestValidator()
        {
            RuleFor(x => x.StartDate)
                .NotEmpty()
                .Must(startDate => startDate.Date >= DateTime.UtcNow.Date)
                .WithMessage("Contract start date cannot be in the past.");

            RuleFor(x => x.EndDate)
                .NotEmpty()
                .WithMessage("Contract end date is required.");

            RuleFor(x => x)
                .Must(x => x.StartDate < x.EndDate)
                .WithMessage("Contract end date must be later than start date.");

            RuleFor(x => x.CommissionPercent)
                .InclusiveBetween(0, 100)
                .When(x => x.CommissionPercent.HasValue);

            RuleFor(x => x.Amount)
                .GreaterThan(0)
                .WithMessage("Contract amount must be greater than zero.");

            RuleFor(x => x.ExpiryReminderDaysBefore)
                .InclusiveBetween(1, 365)
                .WithMessage("Expiry reminder days before must be between 1 and 365.");
        }
    }
}
