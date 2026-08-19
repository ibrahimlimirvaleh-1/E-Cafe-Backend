using System;

namespace ECafe.Application.Common.Dates
{
    public static class DateTimeRangeNormalizer
    {
        public static System.DateTime ToUtcInstant(System.DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => System.DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }

        public static System.DateTime ToUtcRangeStart(System.DateTime value)
            => ToUtcInstant(value);

        public static System.DateTime ToUtcRangeEnd(System.DateTime value)
        {
            var utc = ToUtcInstant(value);
            return utc.TimeOfDay == TimeSpan.Zero
                ? utc.Date.AddDays(1).AddTicks(-1)
                : utc;
        }
    }
}
