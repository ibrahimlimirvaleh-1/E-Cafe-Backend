using ECafe.Application.Common.Validation;
using FluentValidation;

namespace ECafe.Application.Features.Commands.Restaurant
{
    public sealed class RegisterRestaurantCommandValidator : AbstractValidator<RegisterRestaurantCommand>
    {
        public RegisterRestaurantCommandValidator()
        {
            RuleFor(x => x.Phone)
                .MustBePhoneNumber("Restaurant phone");

            RuleFor(x => x.Location)
                .NotEmpty()
                .WithMessage("Location is required.");

            RuleFor(x => x.Latitude)
                .InclusiveBetween(-90, 90)
                .When(x => x.Latitude.HasValue)
                .WithMessage("Latitude must be between -90 and 90.");

            RuleFor(x => x.Longitude)
                .InclusiveBetween(-180, 180)
                .When(x => x.Longitude.HasValue)
                .WithMessage("Longitude must be between -180 and 180.");

            RuleFor(x => x)
                .Must(x => x.Latitude.HasValue == x.Longitude.HasValue)
                .WithMessage("Latitude and longitude must be provided together.");

            RuleFor(x => x.BranchName)
                .NotEmpty()
                .WithMessage("Branch name is required.");

            RuleFor(x => x)
                .Must(x => x.RestaurantGroupId.GetValueOrDefault() > 0 || !string.IsNullOrWhiteSpace(x.RestaurantGroupName))
                .WithMessage("Restaurant group is required.");

            RuleFor(x => x.Owner)
                .NotNull()
                .WithMessage("Restaurant owner is required.");

            When(x => x.Owner is not null, () =>
            {
                RuleFor(x => x.Owner!.Id)
                    .GreaterThan(0)
                    .When(x => x.Owner!.Id.HasValue)
                    .WithMessage("Owner id must be greater than 0.");

                RuleFor(x => x.Owner!.Email)
                    .EmailAddress()
                    .When(x =>
                        !x.Owner!.Id.HasValue &&
                        !string.IsNullOrWhiteSpace(x.Owner.Email))
                    .WithMessage("Owner email is invalid.");

                RuleFor(x => x.Owner!.Phone!)
                    .MustBePhoneNumber("Owner phone")
                    .When(x =>
                        !x.Owner!.Id.HasValue &&
                        !string.IsNullOrWhiteSpace(x.Owner.Phone));

                RuleFor(x => x.Owner!)
                    .Must(owner =>
                        owner.Id.GetValueOrDefault() > 0 ||
                        !string.IsNullOrWhiteSpace(owner.Email) ||
                        (!string.IsNullOrWhiteSpace(owner.SearchText) && owner.SearchText.Contains('@')))
                    .WithMessage("Select an existing owner or enter a new owner email.");
            });
        }
    }
}
