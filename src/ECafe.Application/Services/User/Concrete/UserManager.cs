using AutoMapper;
using ECafe.Application.DTOs.File;
using ECafe.Application.DTOs.User;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Repositories.Role;
using ECafe.Application.Repositories.User;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Application.Services.User.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
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

            user.UserRoles.Add(new UserRole
            {
                RoleId = request.RoleId
            });

            await _userRepository.Add(user);
            await _userRepository.SaveChangesAsync();

            var roleName = GetRoleDescription(request.RoleId);
            await _emailService.SendMailAsync(user.Email, user.Name,user.Surname, request.Password, roleName);
        }

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
    }
}