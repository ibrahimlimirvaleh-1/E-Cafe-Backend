using ECafe.Application.Common.Validation;
using FluentValidation;

namespace ECafe.Application.Features.Commands.User.UpdateStaff
{
    public sealed class UpdateStaffCommandValidator : AbstractValidator<UpdateStaffCommand>
    {
        public UpdateStaffCommandValidator()
        {
            RuleFor(x => x.RestaurantId)
                .GreaterThan(0)
                .WithMessage("Restaurant ID is required.");

            RuleFor(x => x.StaffId)
                .GreaterThan(0)
                .WithMessage("Staff ID is required.");

            RuleFor(x => x.Name)
                .NotEmpty()
                .WithMessage("Name is required.")
                .MaximumLength(100)
                .WithMessage("Name cannot exceed 100 characters.");

            RuleFor(x => x.Surname)
                .NotEmpty()
                .WithMessage("Surname is required.")
                .MaximumLength(100)
                .WithMessage("Surname cannot exceed 100 characters.");

            RuleFor(x => x.Email)
                .NotEmpty()
                .WithMessage("Email is required.")
                .EmailAddress()
                .WithMessage("Email format is invalid.")
                .MaximumLength(256)
                .WithMessage("Email cannot exceed 256 characters.");

            RuleFor(x => x.Phone)
                .MustBePhoneNumber();

            RuleFor(x => x.ServiceFeePercent)
                .GreaterThanOrEqualTo(0)
                .When(x => x.ServiceFeePercent.HasValue)
                .WithMessage("Service fee percent cannot be negative.");

            RuleFor(x => x.MaxActiveTableCount)
                .GreaterThan(0)
                .When(x => x.MaxActiveTableCount.HasValue)
                .WithMessage("Max active table count must be greater than zero.");
        }
    }
}
