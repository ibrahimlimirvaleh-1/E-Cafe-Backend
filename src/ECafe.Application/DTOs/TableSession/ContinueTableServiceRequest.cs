namespace ECafe.Application.DTOs.TableSession;

public sealed record ContinueTableServiceRequest(DateTimeOffset ReviewAgainAt, string Reason);
