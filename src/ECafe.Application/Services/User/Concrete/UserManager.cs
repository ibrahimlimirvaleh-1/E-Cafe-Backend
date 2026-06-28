using AutoMapper;
using ECafe.Application.DTOs.File;
using ECafe.Application.DTOs.User;
using ECafe.Application.DTOs.User.Staff;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Repositories.Role;
using ECafe.Application.Repositories.User;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Application.Services.User.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Shared.DTOs;
using ECafe.Shared.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.User.Concrete
{
    public class UserManager : BaseManager, IUserService
    {
        private readonly IRestaurantRepository _restaurantRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMinioService _minioService;
        private readonly IEmailService _emailService;

        public UserManager(
            IHttpContextAccessor httpContextAccessor,
            IMapper mapper,
            IConfiguration configuration,
            IRestaurantRepository restaurantRepository,
            IRoleRepository roleRepository,
            IMinioService minioService,
            IUserRepository userRepository,
            IEmailService emailService)
            : base(httpContextAccessor, mapper, configuration)
        {
            _restaurantRepository = restaurantRepository;
            _roleRepository = roleRepository;
            _minioService = minioService;
            _userRepository = userRepository;
            _emailService = emailService;
        }

        public async Task CreateUserAsync(CreateUserRequest request)
        {
            await EnsureRestaurantExistsAsync(request.RestaurantId);

            await EnsureRoleExistsAsync(request.RoleId);

            await EnsureUserDoesNotExistAsync(request.Email);

            var file = await CreateFileIfExistsAsync(request.Image);

            var user = CreateUserEntity(request, file);

            await _userRepository.Add(user);

            await _userRepository.SaveChangesAsync();

            var roleName = GetRoleDescription(request.RoleId);

            await _emailService.SendMailAsync(user.Email, user.Name, user.Surname, request.Password, roleName);
        }


        public async Task DeleteAsync(int userId)
        {
            if (userId <= 0)
                throw new BusinessRuleException("Invalid user id");

            var user = await _userRepository.GetByIdAsync(userId);

            if (user is null)
                throw new BusinessRuleException("User not found");

            await _userRepository.Delete(user);
            await _userRepository.SaveChangesAsync();
        }

        public async Task UpdateRoleAsync(int userId, int roleId)
        {
            if (userId <= 0)
                throw new BusinessRuleException("Invalid user id");

            if (roleId <= 0)
                throw new BusinessRuleException("Invalid role id");

            var user = await _userRepository.GetByIdAsync(userId);
            if (user is null)
                throw new BusinessRuleException("User not found");

            var role = await _roleRepository.GetByIdAsync(roleId);
            if (role is null)
                throw new BusinessRuleException("Role not found");

            user.RoleId = roleId;

            var roleName = GetRoleDescription(roleId);

            await _userRepository.SaveChangesAsync();

            await _emailService.SendMailAsync(user.Email, user.Name, user.Surname, roleName);
        }

        public Task<PaginatedList<GetAllUserResponseDto>> GetAllAsync(int? restaurantId, PaginationFilter filter)
        {
            filter ??= new PaginationFilter();

            if (filter.PageNumber <= 0)
                filter.PageNumber = 1;

            if (filter.PageSize <= 0)
                filter.PageSize = 5;

            var query = _userRepository.GetUsersForList(restaurantId);

            var users = query
                .OrderBy(x => x.Name)
                .ThenBy(x => x.Surname)
                .Select(x => new GetAllUserResponseDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Surname = x.Surname,
                    IsActive = x.IsActive,
                    Rating = x.Rating,
                    Role = new RoleDto
                    {
                        Id = x.Role.Id,
                        Name = x.Role.Name
                    }
                });

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

            user.Name = request.Name.Trim();
            user.Surname = request.Surname.Trim();
            user.Email = email;
            user.Phone = phone;

            var file = await CreateFileIfExistsAsync(request.Image);
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

            var staff = await _userRepository.GetStaffDetailAsync(restaurantId, staffId);

            if (staff is null)
                throw new BusinessRuleException("Staff not found");

            return await MapToStaffDetailResponseAsync(staff);
        }

        #region Helpers
        private async Task EnsureRestaurantExistsAsync(int restaurantId)
        {
            var restaurant = await _restaurantRepository.GetByIdAsync(restaurantId);

            if (restaurant is null)
                throw new BusinessRuleException("Restaurant not found");
        }

        private async Task EnsureRoleExistsAsync(int roleId)
        {
            var role = await _roleRepository.GetByIdAsync(roleId);

            if (role is null)
                throw new BusinessRuleException("Role not found");
        }

        private async Task EnsureUserDoesNotExistAsync(string email)
        {
            var existingUser = await _userRepository.GetByEmailAsync(email);

            if (existingUser is not null)
                throw new BusinessRuleException("User with this email already exists");
        }

        private async Task<Domain.Entities.File?> CreateFileIfExistsAsync(IFormFile? image)
        {
            if (image is null || image.Length == 0)
                return null;

            var token = await _minioService.UploadFileAsync(new UploadFileDto(image));

            return new Domain.Entities.File
            {
                Token = token,
                Name = Path.GetFileNameWithoutExtension(image.FileName),
                Extension = Path.GetExtension(image.FileName),
                Size = image.Length,
                Url = string.Empty
            };
        }

        private Domain.Entities.User CreateUserEntity(CreateUserRequest request, Domain.Entities.File? file)
        {
            return new Domain.Entities.User
            {
                Name = request.Name.Trim(),
                Surname = request.Surname.Trim(),
                Email = request.Email.Trim().ToLowerInvariant(),
                Phone = request.Phone.Trim(),
                Password = HashPassword(request.Password),
                IsActive = request.IsActive,
                Rating = request.Rating,
                File = file,
                RoleId = request.RoleId,
                UserRestaurant = new UserRestaurant
                {
                    RestaurantId = request.RestaurantId
                }
            };
        }

        private static string GetRoleDescription(int roleId)
        {
            if (!Enum.IsDefined(typeof(RoleCode), roleId))
                throw new BusinessRuleException("Invalid role id");

            return ((RoleCode)roleId).GetDescription();
        }

        private static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        private async Task<ProfileResponseDto> MapToProfileResponseAsync(Domain.Entities.User user)
        {
            return new ProfileResponseDto
            {
                Id = user.Id,
                Name = user.Name,
                Surname = user.Surname,
                Email = user.Email,
                Phone = user.Phone,
                IsActive = user.IsActive,
                Rating = user.Rating,
                Role = user.Role.Name,
                RestaurantId = user.UserRestaurant?.RestaurantId,
                FileUrl = await GenerateFileUrlAsync(user.File)
            };
        }

        private async Task<StaffDetailResponseDto> MapToStaffDetailResponseAsync(Domain.Entities.User user)
        {
            return new StaffDetailResponseDto
            {
                Id = user.Id,
                Name = user.Name,
                Surname = user.Surname,
                Rating = user.Rating,
                Role = user.Role.Name,
                FileUrl = await GenerateFileUrlAsync(user.File),
                Email = user.Email,
                Phone = user.Phone,
                IsActive = user.IsActive
            };
        }

        private async Task<string?> GenerateFileUrlAsync(Domain.Entities.File? file)
        {
            if (file is null)
                return null;

            return await _minioService.GenerateFileUrl(file.Token);
        }




        #endregion
    }
}
