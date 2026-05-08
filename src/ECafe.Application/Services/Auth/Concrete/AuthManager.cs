using AutoMapper;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.DTOs.File;
using ECafe.Application.Repositories.User;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Shared.Services.Jwt.Abstract;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.Auth.Concrete
{
    public class AuthManager : BaseManager, IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IJwtService _jwtService;
        private readonly IMinioService _minioService;
        public AuthManager(IHttpContextAccessor httpContextAccessor,
                           IMapper mapper,
                           IConfiguration configuration,
                           IUserRepository userRepository,
                           IJwtService jwtService,
                           IMinioService minioService)
                           : base(httpContextAccessor, mapper, configuration)
        {
            _userRepository = userRepository;
            _jwtService = jwtService;
            _minioService = minioService;
        }

        public async Task<string> LoginAsync(LoginRequestDto request)
        {
            if (request is null)
                throw new BusinessRuleException("request is not null!");

            var user = await  _userRepository.GetByEmailAsync(request.Email);

            if (user is null)
                throw new BusinessRuleException("User not found!");
            var isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.Password);

            if (!isPasswordValid)
                throw new BusinessRuleException("Password is wrong!");

            var token = _jwtService.GenerateToken(user);

            return token;
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
        {
            await EnsureUserDoesNotExistAsync(request.Email);
            var file = await CreateFileIfExistsAsync(request.Image);

            var user = new Domain.Entities.User
            {
                Name = request.Name.Trim(),
                Surname = request.Surname.Trim(),
                Email = request.Email.Trim().ToLowerInvariant(),
                Phone = request.Phone.Trim(),
                Password = HashPassword(request.Password),
                IsActive = true,
                RoleId = (int)RoleCode.Customer,
                File = file
            };

            await _userRepository.Add(user);
            await _userRepository.SaveChangesAsync();

            return await _jwtService.CreateTokenResponseAsync(user);
        }

        #region Helpers
        private async Task EnsureUserDoesNotExistAsync(string email)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if (user is not null)
                throw new BusinessRuleException("Bu email ilə istifadəçi artıq mövcuddur");
        }

        private async Task<Domain.Entities.File?> CreateFileIfExistsAsync(IFormFile? image)
        {
            if (image is null || image.Length == 0) return null;

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

        private static string HashPassword(string password)
            => BCrypt.Net.BCrypt.HashPassword(password);
        #endregion


    }
}
