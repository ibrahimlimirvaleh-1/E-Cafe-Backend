using FluentValidation;

namespace ECafe.Application.Features.Commands.Auth.Register
{
    public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
    {
        public RegisterUserCommandValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(100).WithMessage("Name must be at most 100 characters.");

            RuleFor(x => x.Surname)
                .NotEmpty().WithMessage("Surname is required.")
                .MaximumLength(100).WithMessage("Surname must be at most 100 characters.");

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("Email format is invalid.")
                .MaximumLength(150).WithMessage("Email must be at most 150 characters.");

            RuleFor(x => x.Phone)
                .NotEmpty().WithMessage("Phone is required.")
                .MaximumLength(30).WithMessage("Phone must be at most 30 characters.")
                .Matches(@"^\+?[0-9\s\-\(\)]{7,30}$").WithMessage("Phone format is invalid.");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required.")
                .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
                .MaximumLength(100).WithMessage("Password must be at most 100 characters.");

            RuleFor(x => x.FileId)
                .GreaterThan(0)
                .When(x => x.FileId.HasValue)
                .WithMessage("FileId must be greater than 0.");
        }
    }
}
