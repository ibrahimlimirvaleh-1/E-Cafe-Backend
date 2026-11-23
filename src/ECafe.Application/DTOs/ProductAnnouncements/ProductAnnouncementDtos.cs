namespace ECafe.Application.DTOs.ProductAnnouncements;

public sealed record AnnouncementTextRequest(string Language, string Title, string Body);
public sealed record AnnouncementWriteRequest(string Destination, IReadOnlyList<int> RoleIds,
    IReadOnlyList<AnnouncementTextRequest> Texts, DateTimeOffset? ExpiresAtUtc, int? ExpectedVersion = null);
public sealed record AnnouncementVersionRequest(int ExpectedVersion);
public sealed record AnnouncementAcknowledgeRequest(int Version);
public sealed record AnnouncementCard(int Id, int Version, string Title, string Body, string Destination, DateTimeOffset PublishedAtUtc);
public sealed record AnnouncementBatchResponse(int RoleId, int? RestaurantId, IReadOnlyList<AnnouncementCard> Items, bool HasMore);
public sealed record AdminAnnouncement(int Id, int Version, string Destination, IReadOnlyList<int> RoleIds,
    IReadOnlyList<AnnouncementTextRequest> Texts, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc,
    DateTimeOffset? PublishedAtUtc, DateTimeOffset? ExpiresAtUtc, bool IsPublished);
public sealed record AdminAnnouncementPage(IReadOnlyList<AdminAnnouncement> Items, bool HasMore, int? NextBeforeId);
