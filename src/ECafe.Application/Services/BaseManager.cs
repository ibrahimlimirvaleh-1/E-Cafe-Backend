using AutoMapper;
using ECafe.Shared.Security;
using ECafe.Shared.Http;
using ECafe.Application.Common.Exceptions;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;


namespace ECafe.Application.Services;

public abstract class BaseManager
{

    protected readonly IHttpContextAccessor HttpContextAccessor;
    protected readonly IMapper Mapper;
    protected readonly IConfiguration _configuration;

    protected BaseManager(IHttpContextAccessor httpContextAccessor, IMapper mapper, IConfiguration configuration)
    {
        HttpContextAccessor = httpContextAccessor;
        Mapper = mapper;
        _configuration = configuration;
    }

    protected bool IsCurrentUserSuperAdmin()
        => GetCurrentRoleId() == (int)RoleCode.SuperAdmin;

    /// <summary>Fayl bağlamada anonim sorğunu, başqa yükləyicinin faylını və sənədin şəkil kimi ictimailəşdirilməsini rədd edir.</summary>
    protected void EnsureCurrentUserCanAttachImage(Domain.Entities.File file)
    {
        var principal = HttpContextAccessor.HttpContext?.User;
        var authenticated = principal?.Identity?.IsAuthenticated == true;
        var actorId = authenticated && int.TryParse(principal?.FindFirst(AuthenticationClaimTypes.UserId)?.Value, out var parsedId) ? parsedId : 0;
        var isAdmin = authenticated && principal?.FindFirst(ClaimTypes.Role)?.Value == ((int)RoleCode.SuperAdmin).ToString();
        if (!ECafe.Domain.Policies.FileAttachmentPolicy.CanAttachImage(file, actorId, isAdmin))
            throw new BusinessRuleException(ErrorCode.FileNotFoundOrAlreadyAttached);
    }

    /// <summary>Seçilmiş header, legacy claim və restoran siyahısının əvvəlki prioriteti ilə cari restoranı tapır.</summary>
    protected int? GetCurrentRestaurantId()
    {
        var activeRestaurantId = GetActiveRestaurantIdFromHeader();
        if (activeRestaurantId.HasValue && GetCurrentRestaurantIds().Contains(activeRestaurantId.Value))
            return activeRestaurantId.Value;

        var legacyRestaurantIdClaim = CurrentUser.FindFirst(AuthenticationClaimTypes.RestaurantId)?.Value;
        if (int.TryParse(legacyRestaurantIdClaim, out var legacyRestaurantId) && legacyRestaurantId > 0)
            return legacyRestaurantId;

        var restaurantIds = GetCurrentRestaurantIds();
        return restaurantIds.Count > 0 ? restaurantIds.First() : null;
    }

    protected int GetRequiredCurrentRestaurantId()
        => GetCurrentRestaurantId()
           ?? throw new ForbiddenException("Restaurant context is required.");

    /// <summary>Mövcud userId claim-indən müsbət hesab ID-si tələb edir; əvvəlki giriş xətasını saxlayır.</summary>
    protected int GetCurrentUserId()
    {
        var userIdClaim = CurrentUser.FindFirst(AuthenticationClaimTypes.UserId)?.Value;
        if (!int.TryParse(userIdClaim, out var userId) || userId <= 0)
            throw new ForbiddenException("User context is required.");

        return userId;
    }

    /// <summary>Cari cihaz sessiyasını claim-dən oxuyur; boş dəyər null kimi qalır.</summary>
    protected string? GetCurrentSessionId()
    {
        var sessionId = CurrentUser.FindFirst(AuthenticationClaimTypes.SessionId)?.Value;
        return string.IsNullOrWhiteSpace(sessionId) ? null : sessionId;
    }

    protected void EnsureCurrentUserCanAccessRestaurant(int restaurantId)
    {
        if (restaurantId <= 0)
            throw new BusinessRuleException("Invalid restaurant ID!");

        if (IsCurrentUserSuperAdmin())
            return;

        var currentRestaurantId = GetCurrentRestaurantId();
        if (!currentRestaurantId.HasValue || currentRestaurantId.Value != restaurantId)
            throw new ForbiddenException("You do not have access to this restaurant.");
    }

    /// <summary>Tokenin çox-restoran siyahısını və əvvəlki tək-restoran fallback-ini eyni qaydada oxuyur.</summary>
    protected IReadOnlyCollection<int> GetCurrentRestaurantIds()
    {
        var restaurantIdsClaim = CurrentUser.FindFirst(AuthenticationClaimTypes.RestaurantIds)?.Value;
        if (!string.IsNullOrWhiteSpace(restaurantIdsClaim))
        {
            return restaurantIdsClaim
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => int.TryParse(value, out var restaurantId) ? restaurantId : 0)
                .Where(restaurantId => restaurantId > 0)
                .Distinct()
                .ToList();
        }

        var restaurantIdClaim = CurrentUser.FindFirst(AuthenticationClaimTypes.RestaurantId)?.Value;
        return int.TryParse(restaurantIdClaim, out var legacyRestaurantId) && legacyRestaurantId > 0
            ? [legacyRestaurantId]
            : [];
    }

    /// <summary>Seçilmiş restoran header-indən yalnız müsbət ID oxuyur; həmin restorana giriş ayrıca yoxlanılır.</summary>
    private int? GetActiveRestaurantIdFromHeader()
    {
        var request = HttpContextAccessor.HttpContext?.Request;
        if (request is null ||
            !request.Headers.TryGetValue(RequestHeaderNames.ActiveRestaurantId, out var headerValue) ||
            !int.TryParse(headerValue.FirstOrDefault(), out var restaurantId) ||
            restaurantId <= 0)
        {
            return null;
        }

        return restaurantId;
    }

    private ClaimsPrincipal CurrentUser
        => HttpContextAccessor.HttpContext?.User
           ?? throw new ForbiddenException("Authenticated user context is required.");

    protected int GetCurrentRoleId()
    {
        var roleClaim = CurrentUser.FindFirst(ClaimTypes.Role)?.Value;
        if (!int.TryParse(roleClaim, out var roleId))
            throw new ForbiddenException("Role context is required.");

        return roleId;
    }

    /// <summary>Verilmiş restoran üçün token təyinatını oxuyur; uyğun təyinat yoxdursa əvvəlki qlobal rol fallback-i qalır.</summary>
    protected int GetCurrentRoleId(int restaurantId)
    {
        if (restaurantId <= 0)
            throw new BusinessRuleException("Invalid restaurant ID!");

        var restaurantRolesClaim = CurrentUser.FindFirst(AuthenticationClaimTypes.RestaurantRoles)?.Value;
        if (!string.IsNullOrWhiteSpace(restaurantRolesClaim))
        {
            foreach (var assignment in restaurantRolesClaim.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = assignment.Split(':', 2, StringSplitOptions.TrimEntries);
                if (parts.Length == 2 &&
                    int.TryParse(parts[0], out var assignedRestaurantId) &&
                    int.TryParse(parts[1], out var roleId) &&
                    assignedRestaurantId == restaurantId)
                {
                    return roleId;
                }
            }
        }

        return GetCurrentRoleId();
    }
}

