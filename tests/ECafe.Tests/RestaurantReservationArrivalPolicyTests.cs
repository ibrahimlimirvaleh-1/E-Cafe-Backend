using ECafe.Application.DTOs.Restaurant;
using ECafe.Application.Services.Restaurant.Schedule;
using ECafe.Domain.Entities;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using ECafe.Domain.Services;
using Xunit;

namespace ECafe.Tests;

public sealed class RestaurantReservationArrivalPolicyTests
{
    [Theory]
    [InlineData(22, 59, true)]
    [InlineData(23, 0, true)]
    [InlineData(23, 1, false)]
    public void CutoffIsInclusiveAndSeparateFromBusinessOpeningState(int hour, int minute, bool allowed)
    {
        var hours = Hour(new(23, 0));
        var local = new DateTimeOffset(2026, 10, 29, hour, minute, 0, TimeSpan.FromHours(4));
        Assert.True(RestaurantWorkingHoursCalculator.TryGetActiveInterval([hours], local, out var interval));
        Assert.Equal(allowed, RestaurantReservationArrivalPolicy.Allows(local.UtcDateTime, interval.ReservationArrivalCutoffAt?.UtcDateTime));
        Assert.Equal(new DateTimeOffset(2026, 10, 29, 23, 0, 0, TimeSpan.FromHours(4)), interval.ReservationArrivalCutoffAt);
    }

    [Fact]
    public void OvernightCutoffUsesThePreviousWorkdayAndExplicitNextDay()
    {
        var hours = Hour(new(1, 0));
        hours.ClosesAt = new TimeOnly(2, 0);
        hours.LastReservationArrivalDayOffset = 1;
        var local = new DateTimeOffset(2026, 10, 30, 1, 0, 0, TimeSpan.FromHours(4));
        Assert.True(RestaurantWorkingHoursCalculator.TryGetActiveInterval([hours], local, out var interval));
        Assert.Equal(local, interval.ReservationArrivalCutoffAt);
        Assert.True(RestaurantReservationArrivalPolicy.Allows(local.UtcDateTime, interval.ReservationArrivalCutoffAt?.UtcDateTime));
        Assert.False(RestaurantReservationArrivalPolicy.Allows(local.AddMinutes(1).UtcDateTime, interval.ReservationArrivalCutoffAt?.UtcDateTime));
    }

    [Fact]
    public void NoConfiguredCutoffKeepsLegacyOpeningHoursWithoutAnInventedBuffer()
    {
        var hours = Hour(null);
        var local = new DateTimeOffset(2026, 10, 29, 23, 55, 0, TimeSpan.FromHours(4));
        Assert.True(RestaurantWorkingHoursCalculator.TryGetActiveInterval([hours], local, out var interval));
        Assert.Null(interval.ReservationArrivalCutoffAt);
        Assert.True(RestaurantReservationArrivalPolicy.Allows(local.UtcDateTime, null));
        Assert.False(RestaurantWorkingHoursCalculator.TryGetActiveInterval([hours], local.AddMinutes(5), out _));
    }

    [Theory]
    [InlineData(8, 59, 0)]
    [InlineData(0, 0, 1)]
    [InlineData(0, 1, 1)]
    [InlineData(23, 0, -1)]
    [InlineData(23, 0, 2)]
    [InlineData(23, 0, int.MaxValue)]
    public void InvalidCutoffIsRejectedBeforeDateArithmetic(int hour, int minute, int offset)
    {
        var source = Hour(new(hour, minute));
        source.LastReservationArrivalDayOffset = offset;
        Assert.Equal(ErrorCode.RestaurantReservationArrivalCutoffInvalid,
            Assert.Throws<BusinessRuleException>(() => RestaurantReservationArrivalPolicy.NormalizeDayOffset(source)).Code);
    }

    [Fact]
    public void OffsetWithoutTimeAndClosedDayCutoffAreRejected()
    {
        var source = Hour(null);
        source.LastReservationArrivalDayOffset = 0;
        Assert.Throws<BusinessRuleException>(() => RestaurantReservationArrivalPolicy.NormalizeDayOffset(source));
        source = Hour(new(23, 0));
        source.IsClosed = true;
        Assert.Throws<BusinessRuleException>(() => RestaurantReservationArrivalPolicy.NormalizeDayOffset(source));
    }

    [Fact]
    public void ScheduleNormalizeRoundTripAndApplyPreserveEveryCutoffField()
    {
        var normalized = ScheduleTerms.Normalize([new RestaurantWorkingHourDto
        {
            DayOfWeek = DayOfWeek.Thursday, OpensAt = new(9, 0), ClosesAt = new(2, 0),
            CloseDayOffset = 1, LastReservationArrivalAt = new(1, 0)
        }]);
        Assert.Equal(1, normalized.Single().LastReservationArrivalDayOffset);
        var roundTrip = ScheduleTerms.Deserialize(ScheduleTerms.Serialize(normalized));
        Assert.True(ScheduleTerms.Equivalent(normalized, roundTrip));
        var restaurant = new Restaurant();
        ScheduleTerms.Apply(restaurant, roundTrip);
        Assert.Equal(new TimeOnly(1, 0), restaurant.WorkingHours.Single().LastReservationArrivalAt);
        Assert.Equal(1, restaurant.WorkingHours.Single().LastReservationArrivalDayOffset);
        restaurant.WorkingHours.Single().LastReservationArrivalAt = new(0, 30);
        Assert.False(ScheduleTerms.Equivalent(restaurant.WorkingHours, roundTrip));
        Assert.False(ScheduleTerms.CutoffsEquivalent(restaurant.WorkingHours, roundTrip));
    }

    /// <summary>Standart Bakı iş gününü yaratmaqla gün ofseti və boş limit sınaqlarını eyni girişdə saxlayır.</summary>
    private static RestaurantWorkingHour Hour(TimeOnly? cutoff) => new()
    {
        DayOfWeek = DayOfWeek.Thursday, OpensAt = new(9, 0), ClosesAt = TimeOnly.MinValue, CloseDayOffset = 1,
        LastReservationArrivalAt = cutoff, LastReservationArrivalDayOffset = cutoff.HasValue ? 0 : null
    };
}
