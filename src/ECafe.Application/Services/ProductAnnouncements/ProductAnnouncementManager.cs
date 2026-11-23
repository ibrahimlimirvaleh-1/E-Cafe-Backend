using System.Globalization;
using System.Security.Claims;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.ProductAnnouncements;
using ECafe.Application.Repositories.ProductAnnouncements;
using ECafe.Application.Repositories.User;
using ECafe.Application.Repositories.UserRefreshToken;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Repository;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using AnnouncementAudit = ECafe.Domain.Entities.AuditLog;

namespace ECafe.Application.Services.ProductAnnouncements;

public sealed class ProductAnnouncementManager(IProductAnnouncementRepository repository, IUserRepository users,
    IUserRestaurantRepository assignments, IUserRefreshTokenRepository sessions, IBaseRepository<RolePermission> permissions,
    IBaseRepository<AnnouncementAudit> audit, IApplicationDbTransactionFactory transactions, IHttpContextAccessor http, TimeProvider clock)
    : IProductAnnouncementService
{
    public async Task<AnnouncementBatchResponse> PendingAsync(CancellationToken cancellationToken)
    {
        var audience = await AudienceAsync(cancellationToken);
        var rows = await repository.PendingAsync(audience.UserId, audience.RoleId, audience.Destinations,
            Now(), ProductAnnouncementPolicy.BatchSize + 1, cancellationToken);
        var language = TranslationPolicy.NormalizeLanguage(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        var cards = rows.Take(ProductAnnouncementPolicy.BatchSize).Select(row =>
        {
            var text = row.Texts.FirstOrDefault(value => value.Language == language)
                ?? row.Texts.First(value => value.Language == TranslationPolicy.DefaultLanguage);
            return new AnnouncementCard(row.Id, row.Version, text.Title, text.Body, row.Destination, Offset(row.PublishedAtUtc!.Value));
        }).ToArray();
        return new(audience.RoleId, audience.RestaurantId, cards, rows.Count > ProductAnnouncementPolicy.BatchSize);
    }

    public async Task AcknowledgeAsync(int id, AnnouncementAcknowledgeRequest request, CancellationToken cancellationToken)
    {
        if (id <= 0 || request is null || request.Version <= 0) throw Invalid();
        var userId = UserId();
        await using var transaction = await transactions.BeginTransactionAsync(cancellationToken);
        await sessions.AcquireUserLockAsync(userId);
        var audience = await AudienceAsync(cancellationToken);
        if (await repository.HasReceiptAsync(userId, audience.RoleId, id, request.Version, cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }
        await repository.AcquireWriteLockAsync(id, cancellationToken);
        var row = await repository.FindTrackedAsync(id, cancellationToken);
        if (row is null || row.PublishedAtUtc == null || row.PublishedAtUtc > Now() || row.ExpiresAtUtc <= Now() ||
            !row.Roles.Any(role => role.RoleId == audience.RoleId) || !audience.Destinations.Contains(row.Destination))
            throw new NotFoundException(ErrorCode.AnnouncementNotFound);
        EnsureVersion(row, request.Version);
        await repository.AddReceiptAsync(new() { UserId = userId, AnnouncementId = id, Version = row.Version,
            RoleId = audience.RoleId, AcknowledgedAtUtc = Now() }, cancellationToken);
        await repository.SaveChangesAsync();
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<AdminAnnouncementPage> AdminPageAsync(int? beforeId, CancellationToken cancellationToken)
    {
        await RequireAdminAsync(cancellationToken);
        if (beforeId is <= 0) throw Invalid();
        var rows = await repository.AdminPageAsync(beforeId, ProductAnnouncementPolicy.AdminPageSize + 1, cancellationToken);
        var items = rows.Take(ProductAnnouncementPolicy.AdminPageSize).Select(Map).ToArray();
        var more = rows.Count > ProductAnnouncementPolicy.AdminPageSize;
        return new(items, more, more ? items[^1].Id : null);
    }

    public async Task<IReadOnlyList<AnnouncementDestinationDefinition>> DestinationsAsync(CancellationToken cancellationToken)
    {
        await RequireAdminAsync(cancellationToken);
        return ProductAnnouncementPolicy.Destinations;
    }

    public async Task<AdminAnnouncement> CreateAsync(AnnouncementWriteRequest request, CancellationToken cancellationToken)
    {
        var userId = UserId();
        await using var transaction = await transactions.BeginTransactionAsync(cancellationToken);
        await sessions.AcquireUserLockAsync(userId);
        await RequireAdminAsync(cancellationToken);
        ValidateWrite(request);
        var row = new ProductAnnouncement { CreatedAt = Now(), CreatedBy = userId.ToString(CultureInfo.InvariantCulture) };
        Apply(row, request);
        await repository.Add(row);
        await repository.SaveChangesAsync();
        await AuditAsync(row, userId, AuditActions.ProductAnnouncementCreated);
        await transaction.CommitAsync(cancellationToken);
        return Map(row);
    }

    public async Task<AdminAnnouncement> UpdateAsync(int id, AnnouncementWriteRequest request, CancellationToken cancellationToken)
    {
        var userId = UserId();
        await using var transaction = await transactions.BeginTransactionAsync(cancellationToken);
        await sessions.AcquireUserLockAsync(userId);
        await RequireAdminAsync(cancellationToken);
        if (id <= 0) throw Invalid();
        await repository.AcquireWriteLockAsync(id, cancellationToken);
        var row = await repository.FindTrackedAsync(id, cancellationToken) ?? throw new NotFoundException(ErrorCode.AnnouncementNotFound);
        EnsureVersion(row, request?.ExpectedVersion);
        if (row.PublishedAtUtc != null) throw new BusinessRuleException(ErrorCode.AnnouncementMustBeDraft);
        ValidateWrite(request!);
        if (row.Version == int.MaxValue) throw Invalid();
        Apply(row, request!);
        row.Version++;
        Stamp(row, userId);
        await AuditAsync(row, userId, AuditActions.ProductAnnouncementUpdated);
        await transaction.CommitAsync(cancellationToken);
        return Map(row);
    }

    public async Task<AdminAnnouncement> SetPublishedAsync(int id, AnnouncementVersionRequest request, bool publish, CancellationToken cancellationToken)
    {
        var userId = UserId();
        await using var transaction = await transactions.BeginTransactionAsync(cancellationToken);
        await sessions.AcquireUserLockAsync(userId);
        await RequireAdminAsync(cancellationToken);
        if (id <= 0) throw Invalid();
        await repository.AcquireWriteLockAsync(id, cancellationToken);
        var row = await repository.FindTrackedAsync(id, cancellationToken) ?? throw new NotFoundException(ErrorCode.AnnouncementNotFound);
        EnsureVersion(row, request?.ExpectedVersion);
        if (publish) ValidateForPublish(row);
        if (row.PublishedAtUtc.HasValue != publish)
        {
            row.PublishedAtUtc = publish ? Now() : null;
            Stamp(row, userId);
            await AuditAsync(row, userId, publish ? AuditActions.ProductAnnouncementPublished : AuditActions.ProductAnnouncementUnpublished);
        }
        await transaction.CommitAsync(cancellationToken);
        return Map(row);
    }

    /// <summary>JWT-dən yalnız hesab ID-sini götürür; sorğu body-si başqa istifadəçi seçə bilmir.</summary>
    private int UserId()
        => http.HttpContext?.User.Identity?.IsAuthenticated == true &&
           int.TryParse(http.HttpContext.User.FindFirstValue("userId"), out var id) && id > 0
            ? id : throw new UnauthorizedException(ErrorCode.SessionInvalid);

    /// <summary>Həqiqi hesabı, versiyanı və cihazı bazadan yoxlayır; selected profil rolunu JWT-nin qlobal rolu ilə qarışdırmır.</summary>
    private async Task<Audience> AudienceAsync(CancellationToken cancellationToken)
    {
        var id = UserId();
        var principal = http.HttpContext!.User;
        var actor = await users.Query(value => value.Id == id).Select(value => new
        {
            value.IsActive, value.RoleId, value.SessionVersion,
            HasStaff = value.UserRestaurants.Any(role => role.IsActive && UserProfileAccessPolicy.RestaurantRoleIds.Contains(role.RoleId))
        }).SingleOrDefaultAsync(cancellationToken);
        var device = principal.FindFirstValue("sessionId");
        if (actor is null || !actor.IsActive || !int.TryParse(principal.FindFirstValue("sessionVersion"), out var version) ||
            actor.SessionVersion != version || string.IsNullOrEmpty(device) ||
            !await sessions.HasActiveUserSessionAsync(id, device, Now()) ||
            !int.TryParse(principal.FindFirstValue(ClaimTypes.Role), out var globalRole) || globalRole != actor.RoleId)
            throw new UnauthorizedException(ErrorCode.SessionInvalid);
        if (UserProfileAccessPolicy.IsRestaurantRole(actor.RoleId) && !actor.HasStaff)
            throw new UnauthorizedException(ErrorCode.StaffProfileInactive);
        var headers = http.HttpContext.Request.Headers;
        var roleText = headers["X-Active-Role-Id"].ToString();
        var restaurantText = headers["X-Active-Restaurant-Id"].ToString();
        var roleId = globalRole;
        int? restaurantId = null;
        if (roleText.Length > 0 && !int.TryParse(roleText, out roleId)) throw new ForbiddenException(ErrorCode.AccessDenied);
        if (roleId is (int)RoleCode.SuperAdmin or (int)RoleCode.Customer)
        {
            if (actor.RoleId != roleId || restaurantText.Length > 0 && roleId == (int)RoleCode.Customer)
                throw new ForbiddenException(ErrorCode.AccessDenied);
            if (roleId == (int)RoleCode.SuperAdmin && restaurantText.Length > 0)
            {
                if (!int.TryParse(restaurantText, out var selectedAdminRestaurant) || selectedAdminRestaurant <= 0)
                    throw new ForbiddenException(ErrorCode.AccessDenied);
                restaurantId = selectedAdminRestaurant;
            }
        }
        else
        {
            if (!UserProfileAccessPolicy.IsRestaurantRole(roleId) || !int.TryParse(restaurantText, out var selectedRestaurant) ||
                selectedRestaurant <= 0 || await assignments.GetActiveRoleIdAsync(id, selectedRestaurant) != roleId)
                throw new ForbiddenException(ErrorCode.AccessDenied);
            restaurantId = selectedRestaurant;
        }
        var granted = await permissions.Query(value => value.RoleId == roleId).Select(value => value.PermissionId).ToListAsync(cancellationToken);
        return new(id, roleId, restaurantId, ProductAnnouncementPolicy.AllowedDestinations(roleId, granted, actor.HasStaff));
    }

    /// <summary>Platforma elanlarının idarəsini header-də yazılmış rola deyil, canlı qlobal admin hesabına bağlayır.</summary>
    private async Task RequireAdminAsync(CancellationToken cancellationToken)
    {
        var audience = await AudienceAsync(cancellationToken);
        if (audience.RoleId != (int)RoleCode.SuperAdmin) throw new ForbiddenException(ErrorCode.AccessDenied);
    }

    /// <summary>Draft-da natamam mətnə icazə verir, amma dil/rol/keçid sxemini, ölçüləri və tarix sərhədini serverdə yoxlayır.</summary>
    private void ValidateWrite(AnnouncementWriteRequest? request)
    {
        var destination = ProductAnnouncementPolicy.FindDestination(request?.Destination);
        if (request == null || destination == null || request.RoleIds == null || request.RoleIds.Count is < 1 or > 6 ||
            request.RoleIds.Distinct().Count() != request.RoleIds.Count || request.RoleIds.Any(role => !destination.RoleIds.Contains(role)) ||
            request.ExpiresAtUtc?.UtcDateTime <= Now() || request.Texts == null || request.Texts.Count != TranslationPolicy.Languages.Count ||
            request.Texts.Any(value => value == null || !TranslationPolicy.Languages.Contains(value.Language) || value.Title == null || value.Body == null ||
                value.Title.Length > ProductAnnouncementPolicy.MaximumTitleLength || value.Body.Length > ProductAnnouncementPolicy.MaximumBodyLength) ||
            request.Texts.Select(value => value.Language).Distinct(StringComparer.Ordinal).Count() != TranslationPolicy.Languages.Count)
            throw Invalid();
    }

    /// <summary>Üç dildə tamamlanmış düz mətn olmadan və ya bitmiş tarixlə yeniliyi istifadəçilərə yayımlamır.</summary>
    private void ValidateForPublish(ProductAnnouncement row)
    {
        ValidateWrite(new(row.Destination, row.Roles.Select(value => value.RoleId).ToArray(),
            row.Texts.Select(value => new AnnouncementTextRequest(value.Language, value.Title, value.Body)).ToArray(),
            row.ExpiresAtUtc.HasValue ? Offset(row.ExpiresAtUtc.Value) : null));
        try
        {
            var texts = row.Texts.ToDictionary(value => value.Language, StringComparer.Ordinal);
            TranslationPolicy.ValidateText(new(texts["az"].Title, texts["en"].Title, texts["ru"].Title));
            TranslationPolicy.ValidateText(new(texts["az"].Body, texts["en"].Body, texts["ru"].Body));
        }
        catch (BusinessRuleException) { throw Invalid(); }
    }

    /// <summary>Sil-yenidən əlavə et əvəzinə sabit dil açarlarını yeniləyir; eyni EF tracking və kompozit PK qorunur.</summary>
    private static void Apply(ProductAnnouncement row, AnnouncementWriteRequest request)
    {
        row.Destination = request.Destination;
        row.ExpiresAtUtc = request.ExpiresAtUtc?.UtcDateTime;
        foreach (var value in row.Roles.Where(value => !request.RoleIds.Contains(value.RoleId)).ToArray()) row.Roles.Remove(value);
        foreach (var role in request.RoleIds.Where(role => !row.Roles.Any(value => value.RoleId == role))) row.Roles.Add(new() { RoleId = role });
        foreach (var text in request.Texts)
        {
            var stored = row.Texts.FirstOrDefault(value => value.Language == text.Language);
            if (stored == null) { stored = new() { Language = text.Language }; row.Texts.Add(stored); }
            stored.Title = text.Title.Trim();
            stored.Body = text.Body.Trim();
        }
    }

    /// <summary>Paralel adminin köhnə forması yeni versiyanı səssizcə əvəz etmir.</summary>
    private static void EnsureVersion(ProductAnnouncement row, int? expected)
    {
        if (expected is null or <= 0) throw Invalid();
        if (row.Version != expected) throw new BusinessRuleException(ErrorCode.AnnouncementChanged);
    }

    /// <summary>Yazının vaxtını və müəllifini sistem saatı ilə qeyd edir.</summary>
    private void Stamp(ProductAnnouncement row, int userId)
    { row.UpdatedAt = Now(); row.UpdatedBy = userId.ToString(CultureInfo.InvariantCulture); }

    /// <summary>Mətnin özünü və şəxsi məlumatları auditə kopyalamadan əməliyyat izini eyni transaction-da saxlayır.</summary>
    private async Task AuditAsync(ProductAnnouncement row, int userId, string action)
    {
        await audit.Add(new AnnouncementAudit { UserId = userId, EntityName = AuditEntityTypes.ProductAnnouncement, EntityId = row.Id,
            Action = action, OccurredAt = Now(), CreatedAt = Now() });
        await repository.SaveChangesAsync();
    }

    /// <summary>DB tarixlərini yalnız UTC kimi API-yə çıxarır.</summary>
    private static DateTimeOffset Offset(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    /// <summary>Test edilən sistem saatından UTC anını götürür.</summary>
    private DateTime Now() => clock.GetUtcNow().UtcDateTime;

    /// <summary>Admin cavabında dilləri və rolları sabit sıra ilə verir; istifadəçi feed-inə draft mətnləri daxil edilmir.</summary>
    private static AdminAnnouncement Map(ProductAnnouncement row) => new(row.Id, row.Version, row.Destination,
        row.Roles.Select(value => value.RoleId).Order().ToArray(), row.Texts.OrderBy(value => value.Language)
            .Select(value => new AnnouncementTextRequest(value.Language, value.Title, value.Body)).ToArray(),
        Offset(row.CreatedAt), row.UpdatedAt.HasValue ? Offset(row.UpdatedAt.Value) : null,
        row.PublishedAtUtc.HasValue ? Offset(row.PublishedAtUtc.Value) : null,
        row.ExpiresAtUtc.HasValue ? Offset(row.ExpiresAtUtc.Value) : null, row.PublishedAtUtc.HasValue);

    /// <summary>Yanlış admin sorğusunu məxfi məzmun qaytarmayan sabit xəta ilə rədd edir.</summary>
    private static BadRequestException Invalid() => new(ErrorCode.AnnouncementInvalid);

    private sealed record Audience(int UserId, int RoleId, int? RestaurantId, string[] Destinations);
}
