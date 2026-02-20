using ECafe.Domain.Enums;
using ECafe.Shared.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Seeders
{
    public static class StatusSeeder
    {
        public static void Seed(ModelBuilder modelBuilder)
        {
            var statuses = new List<Domain.Entities.Status>();

            statuses.AddRange(CreateStatuses<OrderStatus>(Domain.Enums.StatusType.Order));
            statuses.AddRange(CreateStatuses<ReservationStatus>(Domain.Enums.StatusType.Reservation));
            statuses.AddRange(CreateStatuses<PaymentStatus>(Domain.Enums.StatusType.PaymentStatus));
            statuses.AddRange(CreateStatuses<PaymentMethod>(Domain.Enums.StatusType.PaymentMethod));

            modelBuilder.Entity<Domain.Entities.Status>().HasData(statuses);
        }

        private static IEnumerable<Domain.Entities.Status> CreateStatuses<TEnum>(Domain.Enums.StatusType statusType)
            where TEnum : struct, Enum
        {
            var statusTypeId = (int)statusType;

            return Enum.GetValues<TEnum>()
                .Select(status => new Domain.Entities.Status
                {
                    Id = (statusTypeId * 1000) + Convert.ToInt32(status),
                    Name = status.GetName(),
                    StatusTypeId = statusTypeId
                });
        }
    }
}