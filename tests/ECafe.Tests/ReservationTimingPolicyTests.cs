using ECafe.Application.Services.Reservation.Concrete;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Services;
using Xunit;

namespace ECafe.Tests;

public sealed class ReservationTimingPolicyTests
{
    internal static readonly DateTime Start = new(2026, 10, 1, 11, 0, 0, DateTimeKind.Utc);

    internal static Reservation CreateReservation()
        => new()
        {
            Id = 1, RestaurantId = 2, TableId = 3, CustomerUserId = 4,
            StatusId = StatusIds.Reservation(ReservationStatus.Confirmed),
            ReservedAt = Start, NoShowDeadlineAt = Start.AddMinutes(15), PeopleCount = 2,
            Restaurant = new Restaurant
            {
                Id = 2, IsActive = true, TimeZone = "Asia/Baku", NoShowGraceMinutes = 15,
                TableTurnoverBufferMinutes = 15,
                WorkingHours = [new RestaurantWorkingHour
                {
                    DayOfWeek = DayOfWeek.Thursday, OpensAt = new TimeOnly(9, 0),
                    ClosesAt = new TimeOnly(23, 0)
                }]
            },
            Table = new Table { Id = 3, IsActive = true, Capacity = 2 }
        };

    [Theory]
    [InlineData(5, true)]
    [InlineData(10, false)]
    [InlineData(20, false)]
    public void ShortNoticeBooking_GetsOnlyPersistedGrace(int minutes, bool expected)
    {
        var reservation = CreateReservation();
        var confirmedAt = Start.AddHours(-1);
        reservation.CancellationDeadline = confirmedAt;
        reservation.ConfirmedAt = confirmedAt;
        reservation.CancellationGraceDeadlineAt = ReservationCancellationRules.GetGraceDeadline(Start, confirmedAt, 10);
        reservation.PaymentProofs.Add(new() { StatusId = StatusIds.Reservation(ReservationStatus.Confirmed) });
        Assert.Equal(expected, ReservationCancellationRules.CanRefund(reservation, confirmedAt.AddMinutes(minutes), true));
    }

    [Fact]
    public void Grace_NeverExtendsPastOriginalArrival()
        => Assert.Equal(Start, ReservationCancellationRules.GetGraceDeadline(Start, Start.AddMinutes(-3), 10));

    [Fact]
    public void EarlyBooking_OrdinaryRefundWindowIsNotShortenedByGrace()
    {
        var reservation = CreateReservation();
        reservation.CancellationDeadline = Start.AddHours(-1);
        reservation.CancellationGraceDeadlineAt = Start.AddHours(-3);
        Assert.Equal(Start.AddHours(-1), ReservationCancellationRules.GetRefundDeadline(reservation));
    }

    [Fact]
    public void HistoricalBooking_DoesNotReceiveRetroactiveGrace()
    {
        var reservation = CreateReservation();
        reservation.CancellationDeadline = Start.AddHours(-1);
        reservation.ConfirmedAt = Start.AddMinutes(-50);
        Assert.False(ReservationCancellationRules.IsWithinRefundWindow(reservation, Start.AddMinutes(-45)));
    }

    [Fact]
    public void ArrivalExtension_DoesNotResetRefundCutoff()
    {
        var reservation = CreateReservation();
        reservation.CancellationDeadline = Start.AddHours(-1);
        reservation.CancellationGraceDeadlineAt = Start.AddMinutes(-50);
        reservation.ArrivalAdjustment = new() { RequestedArrivalAt = Start.AddMinutes(30), AcceptedAt = Start };
        reservation.NoShowDeadlineAt = Start.AddMinutes(45);
        Assert.Equal(Start.AddMinutes(-50), ReservationCancellationRules.GetRefundDeadline(reservation));
        Assert.False(ReservationCancellationRules.IsWithinRefundWindow(reservation, Start));
    }

    [Fact]
    public void RestaurantCancellation_RefundsConfirmedDepositAfterCutoff()
    {
        var reservation = CreateReservation();
        reservation.PaymentProofs.Add(new() { StatusId = StatusIds.Reservation(ReservationStatus.Confirmed) });
        reservation.CancellationDeadline = Start.AddHours(-1);
        Assert.True(ReservationCancellationRules.CanRefund(reservation, Start, false));
        Assert.False(ReservationCancellationRules.CanRefund(reservation, Start, true));
        reservation.SeatedAt = Start;
        Assert.False(ReservationCancellationRules.CanRefund(reservation, Start, false));
    }

    [Theory]
    [InlineData(ReservationStatus.Confirmed)]
    [InlineData(ReservationStatus.PaymentSubmitted)]
    [InlineData(ReservationStatus.NoShow)]
    public void WithoutConfirmedPayment_NoRefund(ReservationStatus status)
    {
        var reservation = CreateReservation();
        reservation.StatusId = StatusIds.Reservation(status);
        Assert.False(ReservationCancellationRules.CanRefund(reservation, Start.AddMinutes(-30), false));
    }

    [Fact]
    public void NoNextReservation_DoesNotInventTableVacateTime()
    {
        var reservation = CreateReservation();
        var window = ReservationArrivalPolicy.GetWindow(reservation, null, new())!;
        Assert.Null(window.MustVacateAt);
        Assert.Equal(Start.AddMinutes(45), window.MaximumDeadlineAt);
        Assert.Equal(Start, reservation.ReservedAt);
    }

    [Fact]
    public void NextReservation_CapsArrivalAndVacateAtTurnoverBuffer()
    {
        var reservation = CreateReservation();
        var window = ReservationArrivalPolicy.GetWindow(reservation, Start.AddMinutes(40), new())!;
        Assert.Equal(Start.AddMinutes(25), window.MustVacateAt);
        Assert.Equal(Start.AddMinutes(25), window.MaximumDeadlineAt);
        Assert.Equal(Start.AddMinutes(25), ReservationArrivalPolicy.GetProposedDeadline(reservation, Start.AddMinutes(20), window));
    }

    [Fact]
    public void OvernightWorkday_CapsArrivalAtRealClosingTime()
    {
        var reservation = CreateReservation();
        reservation.ReservedAt = new DateTime(2026, 10, 1, 22, 30, 0, DateTimeKind.Utc);
        reservation.Restaurant.WorkingHours.Single().ClosesAt = new TimeOnly(3, 0);
        reservation.Restaurant.WorkingHours.Single().CloseDayOffset = 1;
        var window = ReservationArrivalPolicy.GetWindow(reservation, null, new())!;
        Assert.Equal(new DateTime(2026, 10, 1, 23, 0, 0, DateTimeKind.Utc), window.MaximumDeadlineAt);
    }

    [Fact]
    public void RepeatedOffer_CannotMoveConsentHoldPastOriginalDeadlineBuffer()
    {
        var reservation = CreateReservation();
        var originalDeadline = reservation.NoShowDeadlineAt;
        reservation.ArrivalAdjustment = new() { OriginalNoShowDeadlineAt = originalDeadline, MaximumNoShowDeadlineAt = Start.AddMinutes(45) };
        var window = ReservationArrivalPolicy.GetWindow(reservation, null, new())!;
        Assert.Equal(originalDeadline.AddMinutes(3), ReservationArrivalPolicy.GetDecisionDeadline(reservation, originalDeadline.AddMinutes(2), window, new()));
    }

    [Fact]
    public void AcceptedExtension_CannotBeRequestedAgain()
    {
        var reservation = CreateReservation();
        reservation.ArrivalAdjustment = new() { AcceptedAt = Start };
        Assert.False(ReservationArrivalPolicy.CanRequest(reservation, Start));
    }

    [Fact]
    public void PendingDecision_OnlyProtectsUntilItsBoundedDeadline()
    {
        var reservation = CreateReservation();
        reservation.ArrivalAdjustment = new() { DecisionExpiresAt = Start.AddMinutes(18) };
        Assert.True(ReservationArrivalPolicy.CanRequest(reservation, Start.AddMinutes(16)));
        Assert.False(ReservationArrivalPolicy.CanRequest(reservation, Start.AddMinutes(18)));
    }

    [Fact]
    public void Choices_ExcludePastTimesAndMaximumBoundary()
    {
        var reservation = CreateReservation();
        var window = ReservationArrivalPolicy.GetWindow(reservation, null, new())!;
        var choices = ReservationArrivalPolicy.GetChoices(reservation, Start.AddMinutes(12), window, new());
        Assert.Equal(Start.AddMinutes(15), choices[0].UtcDateTime);
        Assert.All(choices, value => Assert.True(value.UtcDateTime > Start.AddMinutes(12) && value.UtcDateTime < window.MaximumDeadlineAt));
    }
}
