using ECafe.Application.Services.Reservation.Concrete;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using Xunit;

namespace ECafe.Tests;

public sealed class ReservationWaitingPolicyTests
{
    private static readonly DateTime BookingAt = new(2026, 10, 11, 11, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(-10, -1, 9, 0, false)]
    [InlineData(-10, 14, 24, 14, false)]
    [InlineData(-10, 15, 25, 15, true)]
    [InlineData(10, 24, 14, 14, false)]
    [InlineData(10, 25, 15, 15, true)]
    public void WaitingAndRestaurantDelayAreSeparate(int arrivedOffset, int nowOffset,
        int waitingMinutes, int delayMinutes, bool overdue)
    {
        var reservation = new Reservation
        {
            ReservedAt = BookingAt, ArrivedAt = BookingAt.AddMinutes(arrivedOffset),
            StatusId = StatusIds.Reservation(ReservationStatus.Confirmed)
        };
        var info = ReservationWaitingPolicy.GetInfo(reservation, BookingAt.AddMinutes(nowOffset), 15);
        Assert.NotNull(info);
        Assert.Equal(waitingMinutes, info.WaitingMinutes);
        Assert.Equal(delayMinutes, info.ServiceDelayMinutes);
        Assert.Equal(overdue, info.IsOverdue);
    }

    [Theory]
    [InlineData(ReservationStatus.Cancelled)]
    [InlineData(ReservationStatus.NoShow)]
    [InlineData(ReservationStatus.Seated)]
    public void ClosedOrSeatedReservationsDoNotShowWaiting(ReservationStatus status)
    {
        var reservation = new Reservation
        {
            ReservedAt = BookingAt, ArrivedAt = BookingAt.AddMinutes(-10),
            StatusId = StatusIds.Reservation(status)
        };
        Assert.Null(ReservationWaitingPolicy.GetInfo(reservation, BookingAt.AddHours(1), 15));
    }
}
