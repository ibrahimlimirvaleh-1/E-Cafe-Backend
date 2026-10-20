using System.Security.Claims;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Repositories.ContentLocalization;
using ECafe.Application.Services.ContentLocalization.Abstract;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using Microsoft.AspNetCore.Http;

namespace ECafe.Application.Services.ContentLocalization.Concrete;

public sealed class ContentTranslationAccessManager(IContentTranslationQuery content,
    IContentTranslationAccessRepository access, IHttpContextAccessor accessor) : IContentTranslationAccessService
{
    /// <summary>Aktiv hesabı, cihaz sessiyasını, canlı rolu, restoran təyinatını və mənbə yazı icazəsini hər çağırışda bazadan yoxlayır.</summary>
    public async Task<ContentTranslationSource> RequireAsync(ContentEntityType type, int id, CancellationToken cancellationToken)
    {
        var principal = accessor.HttpContext?.User;
        var sessionId = principal?.FindFirst("sessionId")?.Value;
        if (principal?.Identity?.IsAuthenticated != true || !int.TryParse(principal.FindFirst("userId")?.Value, out var userId) ||
            !int.TryParse(principal.FindFirst(ClaimTypes.Role)?.Value, out var tokenRole) ||
            sessionId is not { Length: 32 } || !Guid.TryParseExact(sessionId, "N", out _))
            throw new ForbiddenException(ErrorCode.Forbidden);
        var actor = await access.GetActorAsync(userId, cancellationToken);
        if (actor == null || actor.RoleId != tokenRole ||
            principal.FindFirst("sessionVersion") is { } version &&
            (!int.TryParse(version.Value, out var sessionVersion) || sessionVersion != actor.SessionVersion))
            throw new ForbiddenException(ErrorCode.Forbidden);
        if (!await access.HasActiveSessionAsync(userId, sessionId, cancellationToken))
            throw new ForbiddenException(ErrorCode.Forbidden);
        var source = id > 0 ? await content.GetSourceAsync(type, id, cancellationToken) : null;
        if (source == null) throw new NotFoundException(ErrorCode.NotFound);
        var effectiveRole = actor.RoleId;
        if (actor.RoleId != (int)RoleCode.SuperAdmin)
        {
            if (source.RestaurantId is not int restaurantId) throw new ForbiddenException(ErrorCode.Forbidden);
            effectiveRole = TokenRestaurantRole(principal, restaurantId, tokenRole);
            if (!actor.Assignments.Any(assignment => assignment.RestaurantId == restaurantId && assignment.RoleId == effectiveRole &&
                assignment.RoleId is (int)RoleCode.Owner or (int)RoleCode.Manager))
                throw new ForbiddenException(ErrorCode.Forbidden);
        }
        if (!await access.HasPermissionAsync(effectiveRole, ContentTranslationPolicy.For(type).Permission, cancellationToken))
            throw new ForbiddenException(ErrorCode.Forbidden);
        return source;
    }

    /// <summary>Seçilmiş restoran rolunu tokenin restoran təyinatından götürür; digər restoranın rolunu qəbul etmir.</summary>
    private static int TokenRestaurantRole(ClaimsPrincipal principal, int restaurantId, int fallback)
    {
        var assignments = principal.FindFirst("restaurantRoles")?.Value;
        if (string.IsNullOrWhiteSpace(assignments)) return fallback;
        foreach (var assignment in assignments.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = assignment.Split(':', 2);
            if (parts.Length == 2 && int.TryParse(parts[0], out var tenant) && tenant == restaurantId && int.TryParse(parts[1], out var role)) return role;
        }
        return 0;
    }
}
