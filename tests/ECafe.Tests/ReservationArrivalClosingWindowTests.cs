using ECafe.Application.Services.Reservation.Concrete;
using Xunit;

namespace ECafe.Tests;

public sealed class ReservationArrivalClosingWindowTests
{
    private static readonly DateTime Closing = new(2026, 10, 1, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NoConfiguredCutoff_DoesNotInventThirtyMinuteRestriction()
    {
        var reservation = NearMidnight(39);
        var window = ReservationArrivalPolicy.GetWindow(reservation, null, new())!;
        Assert.Equal(Closing, window.MustVacateAt);
        Assert.Null(window.ReservationArrivalCutoffAt);
        var choices = ReservationArrivalPolicy.GetChoices(reservation, Closing.AddMinutes(-33), window, new());
        Assert.Contains(choices, value => value.UtcDateTime == Closing.AddMinutes(-5));
        Assert.Null(reservation.MustVacateAt);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(40)]
    public void ReservationSnapshot_AllowsExactCutoffButNotLaterChoice(int minute)
    {
        var reservation = NearMidnight(0);
        reservation.ReservationArrivalCutoffAt = reservation.ReservedAt.AddMinutes(minute);
        var window = ReservationArrivalPolicy.GetWindow(reservation, null, new())!;
        var choices = ReservationArrivalPolicy.GetChoices(reservation, reservation.ReservedAt, window, new());
        Assert.Equal(reservation.ReservationArrivalCutoffAt, choices[^1].UtcDateTime);
        Assert.All(choices, value => Assert.True(value.UtcDateTime <= reservation.ReservationArrivalCutoffAt));
    }

    [Fact]
    public void NoShowGrace_IsSeparateFromLastBookableArrivalTime()
    {
        var reservation = NearMidnight(0);
        reservation.ReservationArrivalCutoffAt = Closing.AddMinutes(-30);
        var window = ReservationArrivalPolicy.GetWindow(reservation, null, new())!;
        Assert.Equal(Closing.AddMinutes(-15), ReservationArrivalPolicy.GetProposedDeadline(reservation,
            reservation.ReservationArrivalCutoffAt.Value, window));
    }

    [Fact]
    public void NewRestaurantCutoff_DoesNotRewriteExistingReservationSnapshot()
    {
        var reservation = NearMidnight(0);
        reservation.ReservationArrivalCutoffAt = Closing.AddMinutes(-30);
        var hour = reservation.Restaurant.WorkingHours.Single();
        hour.LastReservationArrivalAt = new TimeOnly(23, 10);
        hour.LastReservationArrivalDayOffset = 0;
        var window = ReservationArrivalPolicy.GetWindow(reservation, null, new())!;
        Assert.Contains(ReservationArrivalPolicy.GetChoices(reservation, reservation.ReservedAt, window, new()),
            value => value.UtcDateTime == Closing.AddMinutes(-30));
        Assert.Equal(Closing.AddMinutes(-30), reservation.ReservationArrivalCutoffAt);
    }

    [Fact]
    public void LegacyReservationWithoutSnapshot_IsNotRetrospectivelyLimited()
    {
        var reservation = NearMidnight(39);
        var hour = reservation.Restaurant.WorkingHours.Single();
        hour.LastReservationArrivalAt = new TimeOnly(23, 0);
        hour.LastReservationArrivalDayOffset = 0;
        var window = ReservationArrivalPolicy.GetWindow(reservation, null, new())!;
        Assert.Contains(ReservationArrivalPolicy.GetChoices(reservation, reservation.ReservedAt, window, new()),
            value => value.UtcDateTime == Closing.AddMinutes(-5));
    }

    [Fact]
    public void ClosingAndTableTurnover_StillCapActualArrivalDeadline()
    {
        var reservation = NearMidnight(0);
        var next = Closing.AddMinutes(-10);
        var window = ReservationArrivalPolicy.GetWindow(reservation, next, new())!;
        Assert.Equal(next.AddMinutes(-15), window.MustVacateAt);
        Assert.Equal(next.AddMinutes(-15), window.MaximumDeadlineAt);
        Assert.Equal(next.AddMinutes(-15), ReservationArrivalPolicy.GetProposedDeadline(reservation, Closing.AddMinutes(-30), window));
    }

    /// <summary>Gecəyarısı bağlanışını və rezervasiyanın ilkin vaxtını UTC-də təcrid olunmuş şəkildə hazırlayır.</summary>
    private static ECafe.Domain.Entities.Reservation NearMidnight(int minute)
    {
        var reservation = ReservationTimingPolicyTests.CreateReservation();
        reservation.ReservedAt = Closing.AddHours(-1).AddMinutes(minute);
        reservation.NoShowDeadlineAt = reservation.ReservedAt.AddMinutes(15);
        var hours = reservation.Restaurant.WorkingHours.Single();
        hours.ClosesAt = TimeOnly.MinValue;
        hours.CloseDayOffset = 1;
        return reservation;
    }
}
