using FluentValidation;

namespace ECafe.Application.Features.Commands.Table
{
    public class UpdateTableCommandValidator : AbstractValidator<UpdateTableCommand>
    {
        public UpdateTableCommandValidator()
        {
            RuleFor(x => x.RestaurantId)
                .GreaterThan(0)
                .WithMessage("Restaurant ID is required.");

            RuleFor(x => x.TableId)
                .GreaterThan(0)
                .WithMessage("Table ID is required.");

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
