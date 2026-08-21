using FluentValidation;

namespace ECafe.Application.Features.Commands.Table
{
    public class CopyTableCommandValidator : AbstractValidator<CopyTableCommand>
    {
        public CopyTableCommandValidator()
        {
            RuleFor(x => x.RestaurantId)
                .GreaterThan(0)
                .WithMessage("Restaurant ID is required.");

            RuleFor(x => x.TableId)
                .GreaterThan(0)
                .WithMessage("Table ID is required.");

            RuleFor(x => x.TableNo)
                .GreaterThan(0)
                .When(x => x.TableNo.HasValue)
                .WithMessage("Table number must be greater than zero.");

            RuleFor(x => x.Name)
                .MaximumLength(100)
                .WithMessage("Table name cannot exceed 100 characters.");

            RuleFor(x => x.CopyCount)
                .InclusiveBetween(1, 50)
                .When(x => x.Copies is null || x.Copies.Count == 0)
                .WithMessage("Copy count must be between 1 and 50.");

            RuleFor(x => x.Copies)
                .Must(copies => copies is null || copies.Count <= 50)
                .WithMessage("A maximum of 50 tables can be copied at once.");

            RuleFor(x => x.Copies)
                .Must(copies =>
                {
                    if (copies is null)
                        return true;

                    var numbers = copies
                        .Where(x => x.TableNo.HasValue)
                        .Select(x => x.TableNo!.Value)
                        .ToList();

                    return numbers.Count == numbers.Distinct().Count();
                })
                .WithMessage("Table numbers must be unique.");

            RuleForEach(x => x.Copies)
                .ChildRules(copy =>
                {
                    copy.RuleFor(x => x.TableNo)
                        .GreaterThan(0)
                        .When(x => x.TableNo.HasValue)
                        .WithMessage("Table number must be greater than zero.");

                    copy.RuleFor(x => x.Name)
                        .MaximumLength(100)
                        .WithMessage("Table name cannot exceed 100 characters.");
                });
        }
    }
}
