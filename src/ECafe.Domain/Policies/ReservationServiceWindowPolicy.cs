namespace ECafe.Domain.Policies;

public static class ReservationServiceWindowPolicy
{
    /// <summary>Bağlanış, əvvəl razılaşdırılmış təhvil və növbəti rezervasiyanın hazırlıq vaxtından ən erkənini xidmətin sonu seçir.</summary>
    public static DateTime GetServiceEnd(DateTime closingAt, DateTime? mustVacateAt, DateTime? nextReservationAt,
        int turnoverBufferMinutes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(turnoverBufferMinutes);
        var end = mustVacateAt.HasValue && mustVacateAt.Value < closingAt ? mustVacateAt.Value : closingAt;
        if (nextReservationAt.HasValue)
        {
            var nextLimit = nextReservationAt.Value.AddMinutes(-turnoverBufferMinutes);
            if (nextLimit < end) end = nextLimit;
        }
        return end;
    }

}
