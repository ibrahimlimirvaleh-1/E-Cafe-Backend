using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace ECafe.Application.DTOs.Item
{
    public sealed class CreateItemRequestValidator : AbstractValidator<CreateItemRequest>
    {
        private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
        private const long MaxFileSize = 5 * 1024 * 1024;

        public CreateItemRequestValidator()
        {
            RuleFor(x => x.RestaurantId)
                .GreaterThan(0).WithMessage("RestaurantId must be greater than 0.");

            RuleFor(x => x.CategoryId)
                .GreaterThan(0).WithMessage("CategoryId must be greater than 0.");

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

            RuleFor(x => x.File)
                .Must(BeValidExtension)
                .When(x => x.File is not null)
                .WithMessage("Only .jpg, .jpeg, .png and .webp files are allowed.");

            RuleFor(x => x.File)
                .Must(BeValidSize)
                .When(x => x.File is not null)
                .WithMessage("File size must not exceed 5 MB.");
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
}
