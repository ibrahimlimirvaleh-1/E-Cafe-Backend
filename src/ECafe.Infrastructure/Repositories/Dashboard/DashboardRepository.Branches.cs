using ECafe.Application.DTOs.Dashboard;
using ECafe.Application.Repositories.Dashboard;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using StatusType = ECafe.Domain.Enums.StatusType;

namespace ECafe.Infrastructure.Repositories.Dashboard;

public sealed partial class DashboardRepository
{
    /// <summary>Filial nəticələrini səhifələyir və müqaviləni bir dəfə seçir; müqaviləsiz filial da nəticədə saxlanılır.</summary>
    public async Task<DashboardPage<DashboardBranchRow>> GetBranchesAsync(DashboardScope scope, DashboardPeriod period, DateTime nowUtc,
        int pageIndex, CancellationToken cancellationToken)
    {
        var restaurants = Restaurants(scope);
        var count = await restaurants.CountAsync(cancellationToken);
        var receipts = Receipts(scope);
        var refunds = Refunds(scope);
        var cancelled = StatusIds.Build(StatusType.Order, (int)OrderStatus.Cancelled);
        var activeContract = StatusIds.Contract(ContractStatus.Active);
        var page = restaurants
            .OrderBy(restaurant => restaurant.Name)
            .ThenBy(restaurant => restaurant.Id)
            .Skip((pageIndex - 1) * DashboardPeriodPolicy.PageSize)
            .Take(DashboardPeriodPolicy.PageSize);

        var rows = await page.SelectMany(
            restaurant => db.RestaurantContracts
                .Where(contract => contract.RestaurantId == restaurant.Id)
                .OrderByDescending(contract => contract.StatusId == activeContract &&
                    contract.StartDate <= nowUtc && (contract.EndDate == null || contract.EndDate > nowUtc))
                .ThenByDescending(contract => contract.StartDate)
                .ThenByDescending(contract => contract.Id)
                .Take(1)
                .DefaultIfEmpty(),
            (restaurant, contract) => new { Restaurant = restaurant, Contract = contract })
            .OrderBy(row => row.Restaurant.Name)
            .ThenBy(row => row.Restaurant.Id)
            .Select(row => new DashboardBranchRow(
                row.Restaurant.Id, row.Restaurant.Name, row.Restaurant.BranchName, row.Restaurant.IsActive,
                row.Restaurant.Reservations.Count(reservation =>
                    reservation.ReservedAt >= period.StartUtc && reservation.ReservedAt < period.EndUtc),
                row.Restaurant.Orders.Count(order => order.StatusId != cancelled &&
                    order.OpenedAt >= period.StartUtc && order.OpenedAt < period.EndUtc),
                row.Restaurant.Orders
                    .Where(order => order.StatusId != cancelled && order.OpenedAt >= period.StartUtc && order.OpenedAt < period.EndUtc)
                    .SelectMany(order => order.OrderItems)
                    .Where(line => !line.IsDeleted)
                    .Sum(line => (decimal?)line.LineTotal) ?? 0,
                receipts
                    .Where(payment => payment.RestaurantId == row.Restaurant.Id && payment.PaidAt >= period.StartUtc && payment.PaidAt < period.EndUtc)
                    .Sum(payment => (decimal?)payment.Amount) ?? 0,
                refunds
                    .Where(refund => refund.Reservation.RestaurantId == row.Restaurant.Id && refund.RefundedAt >= period.StartUtc && refund.RefundedAt < period.EndUtc)
                    .Sum(refund => (decimal?)refund.Amount) ?? 0,
                row.Contract == null ? null : row.Contract.Id,
                row.Contract == null ? null : row.Contract.StatusId,
                row.Contract == null ? null : row.Contract.EndDate))
            .ToListAsync(cancellationToken);

        return new(rows, count);
    }
}
