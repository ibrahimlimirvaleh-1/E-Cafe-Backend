using FluentValidation;

namespace ECafe.Application.Features.Commands.Table
{
    public class CreateTableCommandValidator : AbstractValidator<CreateTableCommand>
    {
        public CreateTableCommandValidator()
        {
            RuleFor(x => x.RestaurantId)
                .GreaterThan(0)
                .WithMessage("Restaurant ID is required.");

            RuleFor(x => x.TableNo)
                .GreaterThan(0)
                .WithMessage("Table number must be greater than zero.");

            RuleFor(x => x.Name)
                .MaximumLength(100)
                .WithMessage("Table name cannot exceed 100 characters.");

            RuleFor(x => x.Capacity)
                .GreaterThan(0)
                .WithMessage("Table capacity must be greater than zero.");
        }
    }
}
