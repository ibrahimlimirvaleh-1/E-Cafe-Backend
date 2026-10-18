namespace ECafe.Infrastructure.Services;

public sealed record ExpoPushResult(bool Success, string? TicketId, string? ErrorCode);

public sealed record ExpoReceiptResult(bool Success, string? ErrorCode);

public interface IExpoPushTransport
{
    Task<ExpoPushResult> SendAsync(string expoToken, Guid deliveryId, CancellationToken cancellationToken);
    /// <summary>Məxfi bildiriş mətnini paylaşmadan alıcının dilində ümumi push göndərir; köhnə adapterlər mövcud göndəriş yolunu saxlayır.</summary>
    Task<ExpoPushResult> SendLocalizedAsync(string expoToken, Guid deliveryId, string language, CancellationToken cancellationToken)
        => SendAsync(expoToken, deliveryId, cancellationToken);
    Task<ExpoReceiptResult?> GetReceiptAsync(string ticketId, CancellationToken cancellationToken);
}
