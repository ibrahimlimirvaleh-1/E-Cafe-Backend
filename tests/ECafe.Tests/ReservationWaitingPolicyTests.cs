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
            StatusId = StatusIds.Reservation(ReservationStatus.Confirmed), Restaurant = CreateRestaurant()
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
        Assert.False(ReservationWaitingPolicy.RequiresReceptionReview(reservation, BookingAt.AddDays(3)));
    }

    [Theory]
    [InlineData(19, 59, false)]
    [InlineData(20, 0, true)]
    [InlineData(20, 1, true)]
    public void ClosingBoundaryStopsWaitingAndRequiresReview(int hour, int minute, bool review)
    {
        var reservation = new Reservation
        {
            ReservedAt = BookingAt, ArrivedAt = BookingAt,
            StatusId = StatusIds.Reservation(ReservationStatus.Confirmed), Restaurant = CreateRestaurant()
        };
        var now = new DateTime(2026, 10, 11, hour, minute, 0, DateTimeKind.Utc);
        Assert.Equal(review, ReservationWaitingPolicy.GetInfo(reservation, now, 15) == null);
        Assert.Equal(review, ReservationWaitingPolicy.RequiresReceptionReview(reservation, now));
    }

    [Fact]
    public void YesterdayAndMustVacateBoundaryAreNotLiveWaiting()
    {
        var reservation = new Reservation
        {
            ReservedAt = BookingAt, ArrivedAt = BookingAt,
            MustVacateAt = BookingAt.AddMinutes(30),
            StatusId = StatusIds.Reservation(ReservationStatus.Confirmed), Restaurant = CreateRestaurant()
        };
        Assert.Null(ReservationWaitingPolicy.GetInfo(reservation, BookingAt.AddMinutes(30), 15));
        Assert.True(ReservationWaitingPolicy.RequiresReceptionReview(reservation, BookingAt.AddMinutes(30)));
        reservation.MustVacateAt = null;
        Assert.Null(ReservationWaitingPolicy.GetInfo(reservation, BookingAt.AddDays(3), 15));
        Assert.True(ReservationWaitingPolicy.RequiresReceptionReview(reservation, BookingAt.AddDays(3)));
    }

    [Fact]
    public void OvernightServiceRemainsLiveAfterCalendarMidnight()
    {
        var restaurant = CreateRestaurant();
        restaurant.WorkingHours = [new RestaurantWorkingHour
        {
            DayOfWeek = DayOfWeek.Sunday, OpensAt = new TimeOnly(18, 0),
            ClosesAt = new TimeOnly(2, 0), CloseDayOffset = 1
        }];
        var reservation = new Reservation
        {
            Restaurant = restaurant, ReservedAt = new DateTime(2026, 10, 11, 19, 30, 0, DateTimeKind.Utc),
            ArrivedAt = new DateTime(2026, 10, 11, 19, 30, 0, DateTimeKind.Utc),
            StatusId = StatusIds.Reservation(ReservationStatus.Confirmed)
        };
        Assert.NotNull(ReservationWaitingPolicy.GetInfo(reservation, reservation.ReservedAt.AddHours(1), 15));
        Assert.False(ReservationWaitingPolicy.RequiresReceptionReview(reservation, reservation.ReservedAt.AddHours(1)));
        Assert.Null(ReservationWaitingPolicy.GetInfo(reservation, new DateTime(2026, 10, 11, 22, 0, 0, DateTimeKind.Utc), 15));
    }

    [Fact]
    public void MissingScheduleRequiresReviewInsteadOfInventingWaitingDuration()
    {
        var reservation = new Reservation
        {
            ReservedAt = BookingAt, ArrivedAt = BookingAt,
            StatusId = StatusIds.Reservation(ReservationStatus.Confirmed), Restaurant = CreateRestaurant()
        };
        reservation.Restaurant.WorkingHours.Clear();
        Assert.Null(ReservationWaitingPolicy.GetInfo(reservation, BookingAt.AddDays(3), 15));
        Assert.True(ReservationWaitingPolicy.RequiresReceptionReview(reservation, BookingAt.AddDays(3)));
    }

    /// <summary>Azərbaycan saat qurşağında bağlanma sərhədi məlum olan restoran hazırlayır.</summary>
    private static Restaurant CreateRestaurant()
        => new()
        {
            TimeZone = "Asia/Baku", IsActive = true,
            WorkingHours = [new RestaurantWorkingHour
            {
                DayOfWeek = DayOfWeek.Sunday, OpensAt = new TimeOnly(9, 0),
                ClosesAt = TimeOnly.MinValue, CloseDayOffset = 1
            }]
        };
}
