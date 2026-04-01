using ECafe.Application.Features.Commands.User;
using FluentValidation;
using Microsoft.AspNetCore.Http;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private const long MaxFileSize = 5 * 1024 * 1024;

    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(50).WithMessage("Name must be at most 50 characters.");

        RuleFor(x => x.Surname)
            .NotEmpty().WithMessage("Surname is required.")
            .MaximumLength(50).WithMessage("Surname must be at most 50 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email format is invalid.")
            .MaximumLength(100).WithMessage("Email must be at most 100 characters.");

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Phone is required.")
            .MaximumLength(20).WithMessage("Phone must be at most 20 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters.")
            .MaximumLength(100).WithMessage("Password must be at most 100 characters.");

        RuleFor(x => x.RestaurantId)
            .GreaterThan(0).WithMessage("RestaurantId must be greater than 0.");

        RuleFor(x => x.RoleId)
            .GreaterThan(0).WithMessage("RoleId must be greater than 0.");

        RuleFor(x => x.Rating)
            .InclusiveBetween(0, 5)
            .When(x => x.Rating.HasValue)
            .WithMessage("Rating must be between 0 and 5.");

        RuleFor(x => x.Image)
            .Must(BeValidExtension)
            .When(x => x.Image is not null)
            .WithMessage("Only .jpg, .jpeg, .png and .webp files are allowed.");

        RuleFor(x => x.Image)
            .Must(BeValidSize)
            .When(x => x.Image is not null)
            .WithMessage("Image size must not exceed 5 MB.");
    }

    private static bool BeValidExtension(IFormFile? file)
    {
        if (file is null)
            return true;

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        return AllowedExtensions.Contains(extension);
    }

    private static bool BeValidSize(IFormFile? file)
    {
        if (file is null)
            return true;

        return file.Length <= MaxFileSize;
    }
}