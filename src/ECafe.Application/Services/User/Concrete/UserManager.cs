using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Common.Outbox;
using ECafe.Application.Common.Pagination;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.DTOs.User;
using ECafe.Application.DTOs.User.Staff;
using ECafe.Application.Repositories.File;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Repositories.Role;
using ECafe.Application.Repositories.User;
using ECafe.Application.Repositories.UserRefreshToken;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Application.Services.User.Abstract;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Shared.DTOs;
using ECafe.Shared.Extensions;
using ECafe.Shared.Services.Jwt.Abstract;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace ECafe.Application.Services.User.Concrete
{
    public class UserManager : BaseManager, IUserService
    {
        private readonly IRestaurantRepository _restaurantRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMinioService _minioService;
        private readonly IFileRepository _fileRepository;
        private readonly IEmailOutboxService _emailOutboxService;
        private readonly IJwtService _jwtService;
        private readonly IUserRefreshTokenRepository _refreshTokenRepository;
        private readonly IUserRestaurantRepository _userRestaurantRepository;
        private readonly IPasswordSetupService _passwordSetupService;
        private readonly IAuditLogService _auditLogService;

        public UserManager(
            IHttpContextAccessor httpContextAccessor,
            IMapper mapper,
            IConfiguration configuration,
            IRestaurantRepository restaurantRepository,
            IRoleRepository roleRepository,
            IMinioService minioService,
            IFileRepository fileRepository,
            IUserRepository userRepository,
            IEmailOutboxService emailOutboxService,
            IJwtService jwtService,
            IUserRefreshTokenRepository refreshTokenRepository,
            IUserRestaurantRepository userRestaurantRepository,
            IPasswordSetupService passwordSetupService,
            IAuditLogService auditLogService)
            : base(httpContextAccessor, mapper, configuration)
        {
            _restaurantRepository = restaurantRepository;
            _roleRepository = roleRepository;
            _minioService = minioService;
            _fileRepository = fileRepository;
            _userRepository = userRepository;
            _emailOutboxService = emailOutboxService;
            _jwtService = jwtService;
            _refreshTokenRepository = refreshTokenRepository;
            _userRestaurantRepository = userRestaurantRepository;
            _passwordSetupService = passwordSetupService;
            _auditLogService = auditLogService;
        }

        public async Task CreateUserAsync(CreateUserRequest request)
        {
            if (request is null)
                throw new BusinessRuleException("Request is required");

            await EnsureRestaurantExistsAsync(request.RestaurantId);
            EnsureCurrentUserCanAccessRestaurant(request.RestaurantId);
            await EnsureRoleExistsAsync(request.RoleId);
            EnsureOnlySuperAdminCanManageOwnerRole(request.RoleId);
            await EnsureRestaurantOwnerSlotAvailableAsync(request.RestaurantId, request.RoleId);
            await EnsureUserDoesNotExistAsync(request.Email);

            var file = await GetAttachableFileAsync(request.FileId);

            var user = Mapper.Map<Domain.Entities.User>(request);
            user.File = file;
            user.Password = CreateUnusablePasswordHash();

            await _userRepository.Add(user);
            await _userRepository.SaveChangesAsync();

            await _passwordSetupService.SendSetupLinkAsync(user);
        }

        public async Task DeleteAsync(int userId)
        {
            if (userId <= 0)
                throw new BusinessRuleException("Invalid user id");

            var userDetails = await _userRepository.GetProfileByIdAsync(userId);

            if (userDetails is null)
                throw new BusinessRuleException("User not found");

            EnsureCanManageTargetUser(userDetails);

            var user = await _userRepository.GetByIdAsync(userId);
            if (user is null)
                throw new BusinessRuleException("User not found");

            await _userRepository.Delete(user);
            await _userRepository.SaveChangesAsync();
        }

        public async Task ActivateStaffAsync(int restaurantId, int staffId)
        {
            if (restaurantId <= 0)
                throw new BusinessRuleException("Invalid restaurant id");

            if (staffId <= 0)
                throw new BusinessRuleException("Invalid staff id");

            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            var staffAssignment = await _userRestaurantRepository.GetStaffAssignmentAsync(restaurantId, staffId);
            if (staffAssignment is null)
                throw new BusinessRuleException("Staff assignment not found.");

            EnsureOnlySuperAdminCanManageOwnerRole(staffAssignment.User.RoleId);

            await EnsureRestaurantOwnerSlotAvailableAsync(
                restaurantId,
                staffAssignment.User.RoleId,
                staffAssignment.UserId);

            staffAssignment.IsActive = true;
            staffAssignment.User.IsActive = true;

            await _auditLogService.RecordRestaurantActionAsync(
                restaurantId,
                AuditActions.StaffActivated,
                new
                {
                    StaffId = staffAssignment.UserId,
                    StaffName = $"{staffAssignment.User.Name} {staffAssignment.User.Surname}",
                    staffAssignment.User.Email,
                    RoleName = GetRoleDescription(staffAssignment.User.RoleId),
                    RestaurantName = staffAssignment.Restaurant.Name
                },
                AuditEntityTypes.User,
                staffAssignment.UserId,
                $"{staffAssignment.User.Name} {staffAssignment.User.Surname}");

            await _userRestaurantRepository.SaveChangesAsync();
        }

        public async Task DeactivateStaffAsync(int restaurantId, int staffId)
        {
            if (restaurantId <= 0)
                throw new BusinessRuleException("Invalid restaurant id");

            if (staffId <= 0)
                throw new BusinessRuleException("Invalid staff id");

            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            if (staffId == GetCurrentUserId())
                throw new BusinessRuleException("You cannot deactivate your own account.");

            var staffAssignment = await _userRestaurantRepository.GetActiveStaffAssignmentAsync(restaurantId, staffId);
            if (staffAssignment is null)
                throw new BusinessRuleException("Active staff assignment not found.");

            EnsureOnlySuperAdminCanManageOwnerRole(staffAssignment.User.RoleId);

            staffAssignment.IsActive = false;

            if (!await _userRestaurantRepository.HasAnyOtherActiveAssignmentAsync(staffId, staffAssignment.Id))
                staffAssignment.User.IsActive = false;

            await RevokeActiveRefreshTokensAsync(staffId);

            await _auditLogService.RecordRestaurantActionAsync(
                restaurantId,
                AuditActions.StaffDeactivated,
                new
                {
                    StaffId = staffAssignment.UserId,
                    StaffName = $"{staffAssignment.User.Name} {staffAssignment.User.Surname}",
                    staffAssignment.User.Email,
                    RoleName = GetRoleDescription(staffAssignment.User.RoleId),
                    RestaurantName = staffAssignment.Restaurant.Name
                },
                AuditEntityTypes.User,
                staffAssignment.UserId,
                $"{staffAssignment.User.Name} {staffAssignment.User.Surname}");

            await _userRestaurantRepository.SaveChangesAsync();
        }

        public async Task<StaffDetailResponseDto> UpdateStaffAsync(int restaurantId, int staffId, UpdateStaffRequest request)
        {
            if (request is null)
                throw new BusinessRuleException("Request is required");

            if (restaurantId <= 0)
                throw new BusinessRuleException("Invalid restaurant id");

            if (staffId <= 0)
                throw new BusinessRuleException("Invalid staff id");

            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            var staffAssignment = await _userRestaurantRepository.GetActiveStaffAssignmentAsync(restaurantId, staffId);
            if (staffAssignment is null)
                throw new BusinessRuleException("Active staff assignment not found.");

            EnsureOnlySuperAdminCanManageOwnerRole(staffAssignment.User.RoleId);

            var email = request.Email.Trim().ToLowerInvariant();
            var phone = request.Phone.Trim();
            var conflict = await _userRepository.GetProfileConflictAsync(staffId, email, phone);
            if (conflict is not null)
                throw new BusinessRuleException("Email or phone already belongs to another user.");

            var file = await GetAttachableFileAsync(request.FileId);

            Mapper.Map(request, staffAssignment.User);
            if (file is not null)
                staffAssignment.User.File = file;

            staffAssignment.ServiceFeePercent = request.ServiceFeePercent;
            staffAssignment.MaxActiveTableCount = request.MaxActiveTableCount;

            if (!request.IsActive)
            {
                staffAssignment.IsActive = false;
                staffAssignment.User.IsActive = false;
                await RevokeActiveRefreshTokensAsync(staffId);
            }

            await _auditLogService.RecordRestaurantActionAsync(
                restaurantId,
                AuditActions.StaffUpdated,
                new
                {
                    StaffId = staffAssignment.UserId,
                    StaffName = $"{staffAssignment.User.Name} {staffAssignment.User.Surname}",
                    staffAssignment.User.Email,
                    RoleName = GetRoleDescription(staffAssignment.User.RoleId),
                    RestaurantName = staffAssignment.Restaurant.Name
                },
                AuditEntityTypes.User,
                staffAssignment.UserId,
                $"{staffAssignment.User.Name} {staffAssignment.User.Surname}");

            await _userRestaurantRepository.SaveChangesAsync();

            return await MapToStaffDetailResponseAsync(staffAssignment.User);
        }

        public async Task<AuthResponseDto> UpdateRoleAsync(int userId, int roleId)
        {
            if (userId <= 0)
                throw new BusinessRuleException("Invalid user id");

            if (roleId <= 0)
                throw new BusinessRuleException("Invalid role id");

            var userDetails = await _userRepository.GetProfileByIdAsync(userId);
            if (userDetails is null)
                throw new BusinessRuleException("User not found");

            EnsureCanManageTargetUser(userDetails);

            var role = await _roleRepository.GetByIdAsync(roleId);
            if (role is null)
                throw new BusinessRuleException("Role not found");

            EnsureOnlySuperAdminCanManageOwnerRole(roleId);
            await EnsureRestaurantScopedRoleHasRestaurantAsync(userId, roleId);
            await EnsureRestaurantOwnerSlotAvailableForRoleChangeAsync(userId, roleId);

            var user = await _userRepository.GetByIdAsync(userId);
            if (user is null)
                throw new BusinessRuleException("User not found");

            user.RoleId = roleId;

            var roleName = GetRoleDescription(roleId);

            await _userRepository.SaveChangesAsync();

            await _emailOutboxService.EnqueueEmailAsync(
                user.Email,
                $"{user.Name} {user.Surname}",
                "İstifadəçi qeydiyyatı tamamlandı",
                $"{user.Name} {user.Surname} rolunuz dəyişdirildi. Yeni rolunuz: {roleName}",
                OutboxAggregateTypes.User,
                user.Id,
                AuditEntityTypes.User,
                user.Id);

            if (userId != GetCurrentUserId())
            {
                return Mapper.Map<AuthResponseDto>(new AuthTokenMapData
                {
                    AccessToken = string.Empty,
                    RefreshToken = string.Empty
                });
            }

            var tokenUser = await _userRepository.GetByIdWithAuthDetailsTrackedAsync(userId);
            if (tokenUser is null)
                throw new BusinessRuleException("User not found");

            return await CreateAndStoreTokenResponseAsync(tokenUser);
        }

        public Task<PaginatedList<GetAllUserResponseDto>> GetAllAsync(int? restaurantId, PaginationFilter filter)
        {
            filter = PaginationFilterNormalizer.Normalize(filter);

            if (IsCurrentUserSuperAdmin())
            {
                if (restaurantId is <= 0)
                    restaurantId = null;
            }
            else if (restaurantId.HasValue && restaurantId.Value > 0)
            {
                EnsureCurrentUserCanAccessRestaurant(restaurantId.Value);
            }
            else
            {
                restaurantId = GetCurrentRestaurantId()
                    ?? throw new ForbiddenException("Restaurant context is required.");
            }

            var users = _userRepository.GetUsersForList(restaurantId)
                .OrderBy(x => x.Name)
                .ThenBy(x => x.Surname)
                .ProjectTo<GetAllUserResponseDto>(Mapper.ConfigurationProvider);

            return PaginatedList<GetAllUserResponseDto>.CreateAsync(users, filter.PageNumber, filter.PageSize);
        }

        public async Task<ProfileResponseDto> GetProfileAsync(int userId)
        {
            if (userId <= 0)
                throw new BusinessRuleException("Invalid user id");

            var user = await _userRepository.GetProfileByIdAsync(userId);

            if (user is null)
                throw new BusinessRuleException("User not found");

            return await MapToProfileResponseAsync(user);
        }

        public async Task UpdateProfileAsync(int userId, UpdateProfileRequest request)
        {
            if (userId <= 0)
                throw new BusinessRuleException("Invalid user id");

            if (request is null)
                throw new BusinessRuleException("Request is required");

            var user = await _userRepository.GetProfileByIdTrackedAsync(userId);

            if (user is null)
                throw new BusinessRuleException("User not found");

            var email = request.Email.Trim().ToLowerInvariant();
            var phone = request.Phone.Trim();

            var conflictingUser = await _userRepository.GetProfileConflictAsync(userId, email, phone);

            if (conflictingUser?.Email == email)
                throw new BusinessRuleException("User with this email already exists");

            if (conflictingUser?.Phone == phone)
                throw new BusinessRuleException("User with this phone already exists");

            Mapper.Map(request, user);

            var file = await GetAttachableFileAsync(request.FileId);
            if (file is not null)
                user.File = file;

            await _userRepository.SaveChangesAsync();
        }

        public async Task<StaffDetailResponseDto> GetStaffDetailAsync(int restaurantId, int staffId)
        {
            if (restaurantId <= 0)
                throw new BusinessRuleException("Invalid restaurant ID!");

            if (staffId <= 0)
                throw new BusinessRuleException("Invalid staff id");

            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            var staff = await _userRepository.GetStaffDetailAsync(restaurantId, staffId);

            if (staff is null)
                throw new BusinessRuleException("Staff not found");

            return await MapToStaffDetailResponseAsync(staff);
        }

        private async Task EnsureRestaurantExistsAsync(int restaurantId)
        {
            var restaurant = await _restaurantRepository.GetByIdAsync(restaurantId);

            if (restaurant is null)
                throw new BusinessRuleException("Restaurant not found");
        }

        private void EnsureCanManageTargetUser(Domain.Entities.User user)
        {
            if (IsCurrentUserSuperAdmin())
                return;

            EnsureOnlySuperAdminCanManageOwnerRole(user.RoleId);

            var restaurantId = user.UserRestaurant is { IsActive: true }
                ? (int?)user.UserRestaurant.RestaurantId
                : null;
            if (!restaurantId.HasValue)
                throw new ForbiddenException("Target user is not assigned to a restaurant.");

            EnsureCurrentUserCanAccessRestaurant(restaurantId.Value);
        }

        private async Task EnsureRoleExistsAsync(int roleId)
        {
            var role = await _roleRepository.GetByIdAsync(roleId);

            if (role is null)
                throw new BusinessRuleException("Role not found");
        }

        private async Task EnsureUserDoesNotExistAsync(string email)
        {
            var existingUser = await _userRepository.GetByEmailAsync(email.Trim().ToLowerInvariant());

            if (existingUser is not null)
                throw new BusinessRuleException("User with this email already exists");
        }

        private async Task<Domain.Entities.File?> GetAttachableFileAsync(int? fileId)
        {
            if (!fileId.HasValue)
                return null;

            var file = await _fileRepository.GetAttachableByIdAsync(fileId.Value);
            if (file is null)
                throw new BusinessRuleException("File not found or already attached.");

            file.FileTypeId = (int)FileTypeCode.UserProfileImage;

            return file;
        }

        private static string GetRoleDescription(int roleId)
        {
            if (!Enum.IsDefined(typeof(RoleCode), roleId))
                throw new BusinessRuleException("Invalid role id");

            return ((RoleCode)roleId).GetDescription();
        }

        private async Task EnsureRestaurantScopedRoleHasRestaurantAsync(int userId, int roleId)
        {
            if (!IsRestaurantScopedRole(roleId))
                return;

            var userRestaurant = await _userRestaurantRepository.GetActiveByUserIdAsync(userId);
            if (userRestaurant is null)
                throw new BusinessRuleException("Restaurant-scoped role requires an active restaurant assignment.");
        }

        private static bool IsRestaurantScopedRole(int roleId)
            => roleId is (int)RoleCode.Owner or
                (int)RoleCode.Manager or
                (int)RoleCode.Waiter or
                (int)RoleCode.Kitchen;

        private void EnsureOnlySuperAdminCanManageOwnerRole(int roleId)
        {
            if (roleId != (int)RoleCode.Owner || IsCurrentUserSuperAdmin())
                return;

            throw new ForbiddenException("Only platform admin can manage restaurant owner accounts.");
        }

        private async Task EnsureRestaurantOwnerSlotAvailableForRoleChangeAsync(int userId, int roleId)
        {
            if (roleId != (int)RoleCode.Owner)
                return;

            var assignment = await _userRestaurantRepository.GetActiveByUserIdAsync(userId);
            if (assignment is null)
                return;

            await EnsureRestaurantOwnerSlotAvailableAsync(assignment.RestaurantId, roleId, userId);
        }

        private async Task EnsureRestaurantOwnerSlotAvailableAsync(int restaurantId, int roleId, int? excludedUserId = null)
        {
            if (roleId != (int)RoleCode.Owner)
                return;

            var activeOwner = await _userRestaurantRepository.GetActiveOwnerByRestaurantAsync(restaurantId);
            if (activeOwner is null || activeOwner.UserId == excludedUserId)
                return;

            throw new BusinessRuleException("Restaurant already has an active owner.");
        }

        private async Task<ProfileResponseDto> MapToProfileResponseAsync(Domain.Entities.User user)
        {
            var response = Mapper.Map<ProfileResponseDto>(user);
            response.FileUrl = await GenerateFileUrlAsync(user.File);
            return response;
        }

        private async Task<StaffDetailResponseDto> MapToStaffDetailResponseAsync(Domain.Entities.User user)
        {
            var response = Mapper.Map<StaffDetailResponseDto>(user);
            response.FileUrl = await GenerateFileUrlAsync(user.File);
            return response;
        }

        private async Task<string?> GenerateFileUrlAsync(Domain.Entities.File? file)
        {
            if (file is null)
                return null;

            return await _minioService.GenerateFileUrl(file.Token);
        }

        private async Task<AuthResponseDto> CreateAndStoreTokenResponseAsync(Domain.Entities.User user)
        {
            var fileUrl = await GenerateFileUrlAsync(user.File);
            var refreshToken = _jwtService.GenerateRefreshToken();

            await _refreshTokenRepository.Add(Mapper.Map<Domain.Entities.UserRefreshToken>(new RefreshTokenMapData
            {
                UserId = user.Id,
                TokenHash = HashRefreshToken(refreshToken),
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedByIp = HttpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent = HttpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString()
            }));
            await _refreshTokenRepository.SaveChangesAsync();

            return Mapper.Map<AuthResponseDto>(new AuthTokenMapData
            {
                AccessToken = _jwtService.GenerateToken(user, fileUrl),
                RefreshToken = refreshToken
            });
        }

        private async Task RevokeActiveRefreshTokensAsync(int userId)
        {
            var nowUtc = DateTime.UtcNow;
            var refreshTokens = await _refreshTokenRepository.GetActiveByUserIdTrackedAsync(userId, nowUtc);
            var ipAddress = HttpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

            foreach (var refreshToken in refreshTokens)
            {
                refreshToken.RevokedAt = nowUtc;
                refreshToken.RevokedByIp = ipAddress;
            }
        }

        private static string HashRefreshToken(string refreshToken)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
            return Convert.ToHexString(bytes);
        }

        private static string CreateUnusablePasswordHash()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return BCrypt.Net.BCrypt.HashPassword(Convert.ToHexString(bytes));
        }
    }
}
