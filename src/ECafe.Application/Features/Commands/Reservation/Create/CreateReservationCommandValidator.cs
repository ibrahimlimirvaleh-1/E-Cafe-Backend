using FluentValidation;

namespace ECafe.Application.Features.Commands.Reservation.Create;

public sealed class CreateReservationCommandValidator : AbstractValidator<CreateReservationCommand>
{
    public CreateReservationCommandValidator()
    {
        RuleFor(x => x.RestaurantId)
            .GreaterThan(0)
            .WithMessage("RestaurantId must be greater than zero.");

        RuleFor(x => x.TableId)
            .GreaterThan(0)
            .WithMessage("TableId must be greater than zero.");

        RuleFor(x => x.PeopleCount)
            .InclusiveBetween(1, 100)
            .WithMessage("PeopleCount must be between 1 and 100.");

        RuleFor(x => x.Note)
            .MaximumLength(500)
            .WithMessage("Note must be at most 500 characters.");
    }
}
