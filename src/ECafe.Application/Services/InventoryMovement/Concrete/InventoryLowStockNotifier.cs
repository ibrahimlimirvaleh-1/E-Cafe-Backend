using System.Text.Json;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Services.InventoryMovement.Abstract;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;

namespace ECafe.Application.Services.InventoryMovement.Concrete;

public sealed class InventoryLowStockNotifier(IUserRestaurantRepository assignments, INotificationService notifications,
    TimeProvider clock) : IInventoryLowStockNotifier
{
    /// <summary>Minimum həddə ilk enişdə həmin restoranın sahibi və mətbəxinə transaction daxilində bildiriş hazırlayır; təkrarı susdurur.</summary>
    public async Task NotifyAsync(Domain.Entities.InventoryItem item, Unit unit, decimal before, CancellationToken cancellationToken = default)
    {
        if (item.QuantityOnHand > item.LowStockThreshold ||
            before <= item.LowStockThreshold && item.LastLowStockNotifiedAt.HasValue) return;
        item.LastLowStockNotifiedAt = clock.GetUtcNow().UtcDateTime;
        var staff = await assignments.GetActiveByRestaurantAndRolesAsync(item.RestaurantId, [(int)RoleCode.Owner, (int)RoleCode.Kitchen]);
        foreach (var userId in staff.Select(member => member.UserId).Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await notifications.CreateAsync(new CreateNotificationRequest
            {
                UserId = userId, RestaurantId = item.RestaurantId, Title = "Stok minimum həddə düşüb",
                Message = $"{item.Name} stoku minimum həddə düşüb. Cari stok: {item.QuantityOnHand} {unit.Code}.",
                TypeId = (int)NotificationType.InventoryLowStock, ChannelId = (int)NotificationChannel.InApp,
                RelatedEntityType = nameof(Domain.Entities.InventoryItem), RelatedEntityId = item.Id,
                PayloadJson = JsonSerializer.Serialize(new { inventoryItemId = item.Id, inventoryItemName = item.Name,
                    quantityOnHand = item.QuantityOnHand, lowStockThreshold = item.LowStockThreshold, unitId = unit.Id, unitCode = unit.Code })
            });
        }
    }
}
