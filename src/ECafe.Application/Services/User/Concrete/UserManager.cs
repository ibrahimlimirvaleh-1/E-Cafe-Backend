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
            IUserRestaurantRepository userRestaurantRepository)
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
        }

        public async Task CreateUserAsync(CreateUserRequest request)
        {
            if (request is null)
                throw new BusinessRuleException("Request is required");

            await EnsureRestaurantExistsAsync(request.RestaurantId);
            EnsureCurrentUserCanAccessRestaurant(request.RestaurantId);
            await EnsureRoleExistsAsync(request.RoleId);
            await EnsureUserDoesNotExistAsync(request.Email);

            var file = await GetAttachableFileAsync(request.FileId);

            var user = Mapper.Map<Domain.Entities.User>(request);
            user.File = file;

            await _userRepository.Add(user);
            await _userRepository.SaveChangesAsync();

            var roleName = GetRoleDescription(request.RoleId);

            await _emailOutboxService.EnqueueEmailAsync(
                user.Email,
                $"{user.Name} {user.Surname}",
                "İstifadəçi qeydiyyatı tamamlandı",
                $"{user.Name} {user.Surname} {roleName} rolu ilə uğurla qeydiyyatdan keçdi. Şifrəniz: {request.Password}",
                OutboxAggregateTypes.User,
                user.Id,
                AuditEntityTypes.User,
                user.Id);
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

            await EnsureRestaurantScopedRoleHasRestaurantAsync(userId, roleId);

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

        private static string HashRefreshToken(string refreshToken)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
            return Convert.ToHexString(bytes);
        }
    }
}
