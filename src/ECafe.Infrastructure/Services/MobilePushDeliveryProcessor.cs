using System.Security.Cryptography;
using System.Text.Json;
using ECafe.Application.Common.Outbox;
using ECafe.Application.Features.MobileApp;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ECafe.Infrastructure.Services;

public sealed class MobilePushDeliveryProcessor(
    ECafeDbContext context,
    IConfiguration configuration,
    IDataProtectionProvider dataProtectionProvider,
    IExpoPushTransport transport)
{
    private const int MaxSendAttempts = 5;
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan FirstReceiptDelay = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ReceiptRetryDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ReceiptLifetime = TimeSpan.FromHours(24);
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("ECafe.MobilePush.InstallationToken.v1");

    public async Task<int> ExpandOutboxAsync(int batchSize, CancellationToken cancellationToken)
    {
        if (!IsReady() || batchSize <= 0)
            return 0;

        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        var events = context.Database.IsRelational()
            ? await context.OutboxEvents.FromSqlInterpolated($"""
                SELECT * FROM audit.outbox_events
                WHERE "EventType" = {OutboxEventTypes.MobilePushRequested}
                  AND "ProcessedAt" IS NULL
                ORDER BY "OccurredAt", "Id"
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
                """).ToListAsync(cancellationToken)
            : await context.OutboxEvents
                .Where(x => x.EventType == OutboxEventTypes.MobilePushRequested && x.ProcessedAt == null)
                .OrderBy(x => x.OccurredAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var eventItem in events)
        {
            if (eventItem.OccurredAt.Add(ReceiptLifetime) <= now)
            {
                eventItem.LastError = "ExpiredBeforeDelivery";
                eventItem.ProcessedAt = now;
                continue;
            }

            MobilePushOutboxPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<MobilePushOutboxPayload>(eventItem.Payload);
            }
            catch (JsonException)
            {
                payload = null;
            }

            if (payload is null || payload.UserId <= 0 || payload.RestaurantId <= 0)
            {
                eventItem.LastError = "InvalidMobilePushPayload";
                eventItem.ProcessedAt = now;
                continue;
            }

            if (await IsRestaurantEntitledAsync(payload.RestaurantId, cancellationToken) &&
                await IsRecipientEligibleAsync(payload.UserId, payload.RestaurantId, cancellationToken))
            {
                var installationIds = await context.MobilePushInstallations
                    .Where(x => x.UserId == payload.UserId && x.IsActive)
                    .Select(x => x.Id)
                    .ToListAsync(cancellationToken);
                foreach (var installationId in installationIds)
                {
                    context.MobilePushDeliveries.Add(new MobilePushDelivery
                    {
                        Id = Guid.NewGuid(),
                        OutboxEventId = eventItem.Id,
                        InstallationId = installationId,
                        UserId = payload.UserId,
                        RestaurantId = payload.RestaurantId,
                        Status = MobilePushDeliveryStatus.Pending,
                        CreatedAt = now,
                        NextAttemptAt = now
                    });
                }
            }

            eventItem.ProcessedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        return events.Count;
    }

    public async Task<int> SendPendingAsync(int batchSize, CancellationToken cancellationToken)
    {
        if (!IsReady() || batchSize <= 0)
            return 0;

        var now = DateTime.UtcNow;
        var ids = await context.MobilePushDeliveries.AsNoTracking()
            .Where(x => x.Status == MobilePushDeliveryStatus.Pending &&
                x.NextAttemptAt <= now && (x.LockedUntil == null || x.LockedUntil <= now))
            .OrderBy(x => x.NextAttemptAt).ThenBy(x => x.CreatedAt)
            .Take(batchSize).Select(x => x.Id).ToListAsync(cancellationToken);

        var handled = 0;
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await TryClaimAsync(id, MobilePushDeliveryStatus.Pending, cancellationToken))
                continue;

            var delivery = await context.MobilePushDeliveries.SingleAsync(x => x.Id == id, cancellationToken);
            var installation = await GetEligibleInstallationAsync(delivery, cancellationToken);
            if (installation is null)
            {
                Finish(delivery, MobilePushDeliveryStatus.Suppressed, "RecipientUnavailable");
                await context.SaveChangesAsync(cancellationToken);
                handled++;
                continue;
            }

            try
            {
                // Delivery is intentionally generic; private notification content stays in the authenticated inbox.
                var token = _protector.Unprotect(installation.ProtectedToken);
                var result = await transport.SendAsync(token, delivery.Id, cancellationToken);
                delivery.AttemptCount++;
                delivery.FirstSentAt ??= DateTime.UtcNow;
                delivery.SentTokenHash = installation.TokenHash;
                if (result.Success)
                {
                    delivery.Status = MobilePushDeliveryStatus.TicketPending;
                    delivery.TicketId = result.TicketId;
                    delivery.NextAttemptAt = DateTime.UtcNow.Add(FirstReceiptDelay);
                    delivery.LockedUntil = null;
                    delivery.LastErrorCode = null;
                }
                else if (result.ErrorCode == "DeviceNotRegistered")
                {
                    await DeactivateIfStillSameTokenAsync(installation, delivery, cancellationToken);
                    Finish(delivery, MobilePushDeliveryStatus.InvalidToken, result.ErrorCode);
                }
                else if (result.ErrorCode == "MessageRateExceeded")
                {
                    ScheduleRetry(delivery, result.ErrorCode);
                }
                else
                {
                    Finish(delivery, MobilePushDeliveryStatus.Failed, result.ErrorCode ?? "ProviderRejected");
                }
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested &&
                (ex is HttpRequestException or TaskCanceledException or CryptographicException or JsonException))
            {
                delivery.AttemptCount++;
                ScheduleRetry(delivery, ex is CryptographicException ? "TokenProtectionUnavailable" : "TransportUnavailable");
            }

            await context.SaveChangesAsync(cancellationToken);
            handled++;
        }

        return handled;
    }

    public async Task<int> CheckReceiptsAsync(int batchSize, CancellationToken cancellationToken)
    {
        if (batchSize <= 0)
            return 0;

        var now = DateTime.UtcNow;
        var ids = await context.MobilePushDeliveries.AsNoTracking()
            .Where(x => x.Status == MobilePushDeliveryStatus.TicketPending &&
                x.NextAttemptAt <= now && (x.LockedUntil == null || x.LockedUntil <= now))
            .OrderBy(x => x.NextAttemptAt)
            .Take(batchSize).Select(x => x.Id).ToListAsync(cancellationToken);

        var handled = 0;
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await TryClaimAsync(id, MobilePushDeliveryStatus.TicketPending, cancellationToken))
                continue;

            var delivery = await context.MobilePushDeliveries.SingleAsync(x => x.Id == id, cancellationToken);
            if (delivery.TicketId is null || delivery.FirstSentAt is null ||
                delivery.FirstSentAt.Value.Add(ReceiptLifetime) <= DateTime.UtcNow)
            {
                Finish(delivery, MobilePushDeliveryStatus.Failed, "ReceiptExpired");
            }
            else
            {
                try
                {
                    var receipt = await transport.GetReceiptAsync(delivery.TicketId, cancellationToken);
                    if (receipt is null)
                    {
                        RescheduleReceipt(delivery, "ReceiptNotReady");
                    }
                    else if (receipt.Success)
                    {
                        Finish(delivery, MobilePushDeliveryStatus.ProviderAccepted, null);
                    }
                    else if (receipt.ErrorCode == "DeviceNotRegistered")
                    {
                        var installation = await context.MobilePushInstallations
                            .SingleOrDefaultAsync(x => x.Id == delivery.InstallationId, cancellationToken);
                        if (installation is not null)
                            await DeactivateIfStillSameTokenAsync(installation, delivery, cancellationToken);
                        Finish(delivery, MobilePushDeliveryStatus.InvalidToken, receipt.ErrorCode);
                    }
                    else
                    {
                        Finish(delivery, MobilePushDeliveryStatus.Failed, receipt.ErrorCode ?? "ProviderRejected");
                    }
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested &&
                    (ex is HttpRequestException or TaskCanceledException or JsonException))
                {
                    RescheduleReceipt(delivery, "ReceiptUnavailable");
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            handled++;
        }

        return handled;
    }

    // Bir push işini paralel worker-lər arasında yalnız birinə vermək üçün atomik tələb edir.
    private async Task<bool> TryClaimAsync(
        Guid id, MobilePushDeliveryStatus status, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var leaseUntil = now.Add(LeaseDuration);
        if (context.Database.IsRelational())
        {
            return await context.MobilePushDeliveries
                .Where(x => x.Id == id && x.Status == status && x.NextAttemptAt <= now &&
                    (x.LockedUntil == null || x.LockedUntil <= now))
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.LockedUntil, leaseUntil), cancellationToken) == 1;
        }

        var delivery = await context.MobilePushDeliveries.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (delivery is null || delivery.Status != status || delivery.NextAttemptAt > now ||
            delivery.LockedUntil > now)
            return false;
        delivery.LockedUntil = leaseUntil;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    // Aktiv cihaz və tokeni son çatdırılma üçün yenidən yoxlayır.
    private async Task<MobilePushInstallation?> GetEligibleInstallationAsync(
        MobilePushDelivery delivery, CancellationToken cancellationToken)
    {
        if (!IsReady() ||
            delivery.CreatedAt.Add(ReceiptLifetime) <= DateTime.UtcNow ||
            !Guid.TryParse(configuration["MobileApp:ExpoProjectId"], out var projectId) ||
            !await IsRestaurantEntitledAsync(delivery.RestaurantId, cancellationToken) ||
            !await IsRecipientEligibleAsync(delivery.UserId, delivery.RestaurantId, cancellationToken))
            return null;

        var installation = await context.MobilePushInstallations
            .SingleOrDefaultAsync(x => x.Id == delivery.InstallationId && x.IsActive &&
                x.UserId == delivery.UserId && x.ExpoProjectId == projectId, cancellationToken);
        if (installation is null)
            return null;

        var now = DateTime.UtcNow;
        var sessionActive = await context.UserRefreshTokens.AnyAsync(x =>
            x.UserId == delivery.UserId && x.SessionId == installation.SessionId &&
            x.RevokedAt == null && x.ExpiresAt > now && x.User.IsActive,
            cancellationToken);
        return sessionActive ? installation : null;
    }

    // Restoranın mobil push moduluna çıxışı hələ qüvvədədir deyə yoxlayır.
    private Task<bool> IsRestaurantEntitledAsync(int restaurantId, CancellationToken cancellationToken)
    {
        var activeContractStatusId = StatusIds.Contract(ContractStatus.Active);
        return context.Restaurants.AnyAsync(x => x.Id == restaurantId && x.IsActive &&
            x.MobilePushEnabled && x.ShowMobileDownloadLink &&
            x.Contracts.Any(c => c.StatusId == activeContractStatusId),
            cancellationToken);
    }

    // Bildiriş alan istifadəçinin restoranda aktiv səlahiyyətini təsdiqləyir.
    private Task<bool> IsRecipientEligibleAsync(int userId, int restaurantId, CancellationToken cancellationToken)
        => context.Users.AnyAsync(user => user.Id == userId && user.IsActive &&
            (user.RoleId == (int)RoleCode.Customer || user.UserRestaurants.Any(assignment =>
                assignment.RestaurantId == restaurantId && assignment.IsActive &&
                (assignment.RoleId == (int)RoleCode.Owner ||
                 assignment.RoleId == (int)RoleCode.Manager ||
                 assignment.RoleId == (int)RoleCode.Waiter ||
                 assignment.RoleId == (int)RoleCode.Kitchen))), cancellationToken);

    // Token etibarsızdırsa yalnız dəyişməyən cihaz qeydini deaktiv edir.
    private async Task DeactivateIfStillSameTokenAsync(
        MobilePushInstallation installation,
        MobilePushDelivery delivery,
        CancellationToken cancellationToken)
    {
        if (delivery.SentTokenHash is null)
            return;

        if (context.Database.IsRelational())
        {
            var now = DateTime.UtcNow;
            await context.MobilePushInstallations
                .Where(x => x.Id == installation.Id && x.IsActive &&
                    x.UserId == delivery.UserId && x.TokenHash == delivery.SentTokenHash)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.IsActive, false)
                    .SetProperty(x => x.DeactivatedAt, now), cancellationToken);
            return;
        }

        if (installation.IsActive && installation.UserId == delivery.UserId &&
            installation.TokenHash == delivery.SentTokenHash)
        {
            installation.IsActive = false;
            installation.DeactivatedAt = DateTime.UtcNow;
        }
    }

    // Çatdırılmanı son status və xəta kodu ilə bağlayır.
    private static void Finish(MobilePushDelivery delivery, MobilePushDeliveryStatus status, string? errorCode)
    {
        delivery.Status = status;
        delivery.LockedUntil = null;
        delivery.CompletedAt = DateTime.UtcNow;
        delivery.LastErrorCode = errorCode;
    }

    // Expo qəbzinin gecikmiş yoxlanışını növbəti vaxta keçirir.
    private static void RescheduleReceipt(MobilePushDelivery delivery, string errorCode)
    {
        delivery.NextAttemptAt = DateTime.UtcNow.Add(ReceiptRetryDelay);
        delivery.LockedUntil = null;
        delivery.LastErrorCode = errorCode;
    }

    // Müvəqqəti xətada çatdırılmanı artan gecikmə ilə yenidən növbəyə qoyur.
    private static void ScheduleRetry(MobilePushDelivery delivery, string errorCode)
    {
        if (delivery.AttemptCount >= MaxSendAttempts)
        {
            Finish(delivery, MobilePushDeliveryStatus.Failed, errorCode);
            return;
        }

        var delaySeconds = Math.Min(3600, 15 * Math.Pow(4, delivery.AttemptCount - 1));
        delivery.NextAttemptAt = DateTime.UtcNow.AddSeconds(delaySeconds);
        delivery.LockedUntil = null;
        delivery.LastErrorCode = errorCode;
    }

    // Canlı push göndərişi üçün tələb olunan ayarların hamısının hazır olduğunu yoxlayır.
    private bool IsReady() => configuration.GetValue<bool>("MobileApp:PushDeliveryReady") &&
        Guid.TryParse(configuration["MobileApp:ExpoProjectId"], out _);
}
