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

            RuleFor(x => x.Location)
                .NotEmpty()
                .WithMessage("Location is required.");

            RuleFor(x => x.Latitude)
                .InclusiveBetween(-90, 90)
                .When(x => x.Latitude.HasValue)
                .WithMessage("Latitude must be between -90 and 90.");

            RuleFor(x => x.Longitude)
                .InclusiveBetween(-180, 180)
                .When(x => x.Longitude.HasValue)
                .WithMessage("Longitude must be between -180 and 180.");

            RuleFor(x => x)
                .Must(x => x.Latitude.HasValue == x.Longitude.HasValue)
                .WithMessage("Latitude and longitude must be provided together.");

            RuleFor(x => x.BranchName)
                .NotEmpty()
                .WithMessage("Branch name is required.");

            RuleFor(x => x.TimeZone)
                .MaximumLength(64)
                .Must(BeValidTimeZone)
                .When(x => !string.IsNullOrWhiteSpace(x.TimeZone))
                .WithMessage("Time zone is invalid.");

            RuleFor(x => x.WorkingHours)
                .Must(HaveUniqueDays)
                .WithMessage("Working hours must contain unique days.");

            RuleForEach(x => x.WorkingHours).ChildRules(hour =>
            {
                hour.RuleFor(x => x.DayOfWeek)
                    .IsInEnum()
                    .WithMessage("Working hour day is invalid.");

                hour.RuleFor(x => x)
                    .Must(x => x.IsClosed || x.OpensAt != x.ClosesAt)
                    .WithMessage("Opening time and closing time cannot be the same for an open day.");
            });

            RuleFor(x => x.RestaurantGroupEmail)
                .NotEmpty()
                .EmailAddress()
                .When(x => x.RestaurantGroupId.GetValueOrDefault() <= 0 && !string.IsNullOrWhiteSpace(x.RestaurantGroupName))
                .WithMessage("Restaurant group email is required.");
        }

        private static bool HaveUniqueDays(IEnumerable<ECafe.Application.DTOs.Restaurant.RestaurantWorkingHourDto>? workingHours)
        {
            if (workingHours is null)
                return true;

            var days = workingHours.Select(hour => hour.DayOfWeek).ToList();
            return days.Count == days.Distinct().Count();
        }

        private static bool BeValidTimeZone(string? timeZone)
        {
            var trimmedTimeZone = timeZone!.Trim();

            try
            {
                _ = TimeZoneInfo.FindSystemTimeZoneById(trimmedTimeZone);
                return true;
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }

            return TimeZoneInfo.TryConvertIanaIdToWindowsId(trimmedTimeZone, out _)
                || TimeZoneInfo.TryConvertWindowsIdToIanaId(trimmedTimeZone, out _);
        }
    }
}
