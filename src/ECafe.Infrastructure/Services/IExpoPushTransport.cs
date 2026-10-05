namespace ECafe.Infrastructure.Services;

public sealed record ExpoPushResult(bool Success, string? TicketId, string? ErrorCode);

public sealed record ExpoReceiptResult(bool Success, string? ErrorCode);

public interface IExpoPushTransport
{
    Task<ExpoPushResult> SendAsync(string expoToken, Guid deliveryId, CancellationToken cancellationToken);
    Task<ExpoReceiptResult?> GetReceiptAsync(string ticketId, CancellationToken cancellationToken);
}
