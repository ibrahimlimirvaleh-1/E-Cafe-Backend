using ECafe.Domain.Entities;

namespace ECafe.Application.Features.MobileApp;

public interface IMobilePushOutboxWriter
{
    void Enqueue(Notification notification);
}

public sealed record MobilePushOutboxPayload(int UserId, int RestaurantId);
