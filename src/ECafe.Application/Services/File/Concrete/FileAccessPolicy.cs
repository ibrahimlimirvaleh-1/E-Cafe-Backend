using System.Security.Claims;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Services.FileAccess.Abstract;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Http;

namespace ECafe.Application.Services.FileAccess.Concrete
{
    public sealed class FileAccessPolicy : IFileAccessPolicy
    {
        private const string ActiveRestaurantIdHeader = "X-Active-Restaurant-Id";

        private readonly IHttpContextAccessor _httpContextAccessor;

        public FileAccessPolicy(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public void EnsureCurrentUserCanAccess(Domain.Entities.File file)
        {
            if (file.FileTypeId == (int)FileTypeCode.TemporaryUpload)
            {
                if (IsCurrentUserSuperAdmin() || file.CreatedBy == GetCurrentUserId().ToString())
                    return;

                throw new ForbiddenException(ErrorCode.AccessDenied);
            }

            var contractRestaurantIds = file.RestaurantContracts
                .Select(contract => contract.RestaurantId)
                .Distinct()
                .ToList();

            if (contractRestaurantIds.Count > 0)
            {
                foreach (var restaurantId in contractRestaurantIds)
                    EnsureCurrentUserCanAccessRestaurant(restaurantId);

                return;
            }

            if (file.Restaurant is not null)
            {
                EnsureCurrentUserCanAccessRestaurant(file.Restaurant.Id);
                return;
            }

            var itemRestaurantIds = file.Items
                .Select(item => item.RestaurantId)
                .Distinct()
                .ToList();

            if (itemRestaurantIds.Count > 0)
            {
                foreach (var restaurantId in itemRestaurantIds)
                    EnsureCurrentUserCanAccessRestaurant(restaurantId);

                return;
            }

            if (file.User is not null)
            {
                if (!IsCurrentUserSuperAdmin() && file.User.Id != GetCurrentUserId())
                    throw new ForbiddenException(ErrorCode.AccessDenied);

                return;
            }

            throw new NotFoundException(ErrorCode.FileNotFound);
        }

        private ClaimsPrincipal CurrentUser
            => _httpContextAccessor.HttpContext?.User
               ?? throw new ForbiddenException("Authenticated user context is required.");

        private bool IsCurrentUserSuperAdmin()
            => GetCurrentRoleId() == (int)RoleCode.SuperAdmin;

        private int? GetCurrentRestaurantId()
        {
            var activeRestaurantId = GetActiveRestaurantIdFromHeader();
            if (activeRestaurantId.HasValue && GetCurrentRestaurantIds().Contains(activeRestaurantId.Value))
                return activeRestaurantId.Value;

            var legacyRestaurantIdClaim = CurrentUser.FindFirst("restaurantId")?.Value;
            if (int.TryParse(legacyRestaurantIdClaim, out var legacyRestaurantId) && legacyRestaurantId > 0)
                return legacyRestaurantId;

            var restaurantIds = GetCurrentRestaurantIds();
            return restaurantIds.Count > 0 ? restaurantIds.First() : null;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = CurrentUser.FindFirst("userId")?.Value;
            if (!int.TryParse(userIdClaim, out var userId) || userId <= 0)
                throw new ForbiddenException("User context is required.");

            return userId;
        }

        private int GetCurrentRoleId()
        {
            var roleClaim = CurrentUser.FindFirst(ClaimTypes.Role)?.Value;
            if (!int.TryParse(roleClaim, out var roleId))
                throw new ForbiddenException("Role context is required.");

            return roleId;
        }

        private void EnsureCurrentUserCanAccessRestaurant(int restaurantId)
        {
            if (restaurantId <= 0)
                throw new BusinessRuleException("Invalid restaurant ID!");

            if (IsCurrentUserSuperAdmin())
                return;

            var currentRestaurantId = GetCurrentRestaurantId();
            if (!currentRestaurantId.HasValue || currentRestaurantId.Value != restaurantId)
                throw new ForbiddenException(ErrorCode.AccessDenied);
        }

        private IReadOnlyCollection<int> GetCurrentRestaurantIds()
        {
            var restaurantIdsClaim = CurrentUser.FindFirst("restaurantIds")?.Value;
            if (!string.IsNullOrWhiteSpace(restaurantIdsClaim))
            {
                return restaurantIdsClaim
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(value => int.TryParse(value, out var restaurantId) ? restaurantId : 0)
                    .Where(restaurantId => restaurantId > 0)
                    .Distinct()
                    .ToList();
            }

            var restaurantIdClaim = CurrentUser.FindFirst("restaurantId")?.Value;
            return int.TryParse(restaurantIdClaim, out var legacyRestaurantId) && legacyRestaurantId > 0
                ? [legacyRestaurantId]
                : [];
        }

        private int? GetActiveRestaurantIdFromHeader()
        {
            var request = _httpContextAccessor.HttpContext?.Request;
            if (request is null ||
                !request.Headers.TryGetValue(ActiveRestaurantIdHeader, out var headerValue) ||
                !int.TryParse(headerValue.FirstOrDefault(), out var restaurantId) ||
                restaurantId <= 0)
            {
                return null;
            }

            return restaurantId;
        }
    }
}
