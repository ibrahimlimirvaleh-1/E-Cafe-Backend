using FluentValidation;

namespace ECafe.Application.Features.Commands.RestaurantGroup
{
    public sealed class CreateRestaurantGroupCommandValidator : AbstractValidator<CreateRestaurantGroupCommand>
    {
        public CreateRestaurantGroupCommandValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .WithMessage("Restaurant group name is required.");

            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress()
                .WithMessage("Restaurant group email is required.");
        }
    }
}
