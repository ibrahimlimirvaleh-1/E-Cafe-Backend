using ECafe.Application.DTOs.ProductAnnouncements;
using ECafe.Domain.Policies;

namespace ECafe.Application.Services.ProductAnnouncements;

public interface IProductAnnouncementService
{
    Task<AnnouncementBatchResponse> PendingAsync(CancellationToken cancellationToken);
    Task AcknowledgeAsync(int id, AnnouncementAcknowledgeRequest request, CancellationToken cancellationToken);
    Task<AdminAnnouncementPage> AdminPageAsync(int? beforeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AnnouncementDestinationDefinition>> DestinationsAsync(CancellationToken cancellationToken);
    Task<AdminAnnouncement> CreateAsync(AnnouncementWriteRequest request, CancellationToken cancellationToken);
    Task<AdminAnnouncement> UpdateAsync(int id, AnnouncementWriteRequest request, CancellationToken cancellationToken);
    Task<AdminAnnouncement> SetPublishedAsync(int id, AnnouncementVersionRequest request, bool publish, CancellationToken cancellationToken);
}
