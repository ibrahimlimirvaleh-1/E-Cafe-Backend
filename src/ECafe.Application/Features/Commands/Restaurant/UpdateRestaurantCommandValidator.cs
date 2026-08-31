using ECafe.Application.Common.Validation;
using FluentValidation;

namespace ECafe.Application.Features.Commands.Restaurant
{
    public sealed class UpdateRestaurantCommandValidator : AbstractValidator<UpdateRestaurantCommand>
    {
        public UpdateRestaurantCommandValidator()
        {
            RuleFor(x => x.Phone)
                .MustBePhoneNumber("Restaurant phone");

            RuleFor(x => x.BranchName)
                .NotEmpty()
                .WithMessage("Branch name is required.");
        }
    }
}
