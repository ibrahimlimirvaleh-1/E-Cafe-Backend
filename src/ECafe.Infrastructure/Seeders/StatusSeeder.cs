using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
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
            statuses.AddRange(CreatePaymentMethodStatuses());
            statuses.AddRange(CreateStatuses<ItemStatus>(Domain.Enums.StatusType.ItemStatus));
            statuses.AddRange(CreateStatuses<ContractStatus>(Domain.Enums.StatusType.Contract));

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

        private static IEnumerable<Domain.Entities.Status> CreatePaymentMethodStatuses()
        {
            const Domain.Enums.StatusType statusType = Domain.Enums.StatusType.PaymentMethod;
            var statusTypeId = (int)statusType;

            return Enum.GetValues<PaymentMethod>()
                .Select(paymentMethod => new Domain.Entities.Status
                {
                    Id = (statusTypeId * 1000) + Convert.ToInt32(paymentMethod),
                    Name = paymentMethod.GetName(),
                    StatusTypeId = statusTypeId,
                    IsDeleted = !PaymentMethodPolicy.IsMvpSupported(paymentMethod)
                });
        }

    }
}