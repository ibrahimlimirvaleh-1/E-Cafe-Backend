using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public sealed class ProductAnnouncement : AuditableSoftDeletableEntity<int>
{
    public int Version { get; set; } = 1;
    public string Destination { get; set; } = string.Empty;
    public DateTime? PublishedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public ICollection<ProductAnnouncementText> Texts { get; set; } = [];
    public ICollection<ProductAnnouncementRole> Roles { get; set; } = [];
}

public sealed class ProductAnnouncementText
{
    public int AnnouncementId { get; set; }
    public string Language { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public sealed class ProductAnnouncementRole
{
    public int AnnouncementId { get; set; }
    public int RoleId { get; set; }
}

public sealed class ProductAnnouncementReceipt
{
    public int AnnouncementId { get; set; }
    public int Version { get; set; }
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public DateTime AcknowledgedAtUtc { get; set; }
}
