using AutoMapper;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.Repositories.File;
using ECafe.Application.Repositories.User;
using ECafe.Application.Repositories.UserRefreshToken;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Shared.Services.Jwt.Abstract;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace ECafe.Application.Services.Auth.Concrete
{
    public class AuthManager : BaseManager, IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IUserRefreshTokenRepository _refreshTokenRepository;
        private readonly IJwtService _jwtService;
        private readonly IMinioService _minioService;
        private readonly IFileRepository _fileRepository;
        private readonly ILoginAttemptService _loginAttemptService;
        public AuthManager(IHttpContextAccessor httpContextAccessor,
                           IMapper mapper,
                           IConfiguration configuration,
                           IUserRepository userRepository,
                           IUserRefreshTokenRepository refreshTokenRepository,
                           IJwtService jwtService,
                           IMinioService minioService,
                           IFileRepository fileRepository,
                           ILoginAttemptService loginAttemptService)
                           : base(httpContextAccessor, mapper, configuration)
        {
            _userRepository = userRepository;
            _refreshTokenRepository = refreshTokenRepository;
            _jwtService = jwtService;
            _minioService = minioService;
            _fileRepository = fileRepository;
            _loginAttemptService = loginAttemptService;
        }

        public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
        {
            if (request is null)
                throw new BusinessRuleException("request is not null!");

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            await _loginAttemptService.EnsureNotLockedOutAsync(normalizedEmail);

            var user = await _userRepository.GetByEmailTrackedAsync(normalizedEmail);

            if (user is null)
            {
                await _loginAttemptService.RecordFailureAsync(null, normalizedEmail, "InvalidCredentials");
                throw new UnauthorizedException(ErrorCode.InvalidCredentials);
            }

            var isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.Password);

            if (!isPasswordValid)
            {
                await _loginAttemptService.RecordFailureAsync(user, normalizedEmail, "InvalidCredentials");
                throw new UnauthorizedException(ErrorCode.InvalidCredentials);
            }

            if (!user.IsActive)
            {
                await _loginAttemptService.RecordFailureAsync(user, normalizedEmail, "InactiveAccount");
                throw new ForbiddenException("User account is inactive.");
            }

            await _loginAttemptService.RecordSuccessAsync(user, normalizedEmail);
            return await CreateAndStoreTokenResponseAsync(user);
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
        {
            await EnsureUserDoesNotExistAsync(request.Email, request.Phone);
            var file = await GetAttachableFileAsync(request.FileId);

            var user = Mapper.Map<Domain.Entities.User>(request);
            user.File = file;

            await _userRepository.Add(user);
            await _userRepository.SaveChangesAsync();

            return await CreateAndStoreTokenResponseAsync(user);
        }

        public async Task<AuthResponseDto> RefreshAsync(RefreshTokenRequestDto request)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.RefreshToken))
                throw new BusinessRuleException("Refresh token is required.");

            var refreshTokenHash = HashRefreshToken(request.RefreshToken);
            var storedToken = await _refreshTokenRepository.GetByTokenHashTrackedAsync(refreshTokenHash);

            if (storedToken is null ||
                storedToken.RevokedAt is not null ||
                storedToken.ExpiresAt <= DateTime.UtcNow)
                throw new ForbiddenException("Refresh token is invalid or expired.");

            if (!storedToken.User.IsActive)
                throw new ForbiddenException("User account is inactive.");

            return await RotateRefreshTokenAsync(storedToken);
        }

        #region Helpers
        private async Task EnsureUserDoesNotExistAsync(string email, string phone)
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();
            var normalizedPhone = phone.Trim();

            var emailExists = await _userRepository.CheckExistAsync(x => x.Email == normalizedEmail);
            if (emailExists)
                throw new BusinessRuleException("Bu email ilə istifadəçi artıq mövcuddur");

            var phoneExists = await _userRepository.CheckExistAsync(x => x.Phone == normalizedPhone);
            if (phoneExists)
                throw new BusinessRuleException("Bu telefon nömrəsi ilə istifadəçi artıq mövcuddur");
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

        private async Task<AuthResponseDto> CreateAndStoreTokenResponseAsync(Domain.Entities.User user)
        {
            string? fileUrl = null;

            if (user.File != null)
                fileUrl = await _minioService.GenerateFileUrl(user.File.Token);

            var refreshToken = await AddRefreshTokenAsync(user);

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

        private async Task<AuthResponseDto> RotateRefreshTokenAsync(Domain.Entities.UserRefreshToken storedToken)
        {
            string? fileUrl = null;

            if (storedToken.User.File != null)
                fileUrl = await _minioService.GenerateFileUrl(storedToken.User.File.Token);

            var refreshToken = _jwtService.GenerateRefreshToken();
            var refreshTokenHash = HashRefreshToken(refreshToken);

            storedToken.RevokedAt = DateTime.UtcNow;
            storedToken.RevokedByIp = GetRequestIp();
            storedToken.ReplacedByTokenHash = refreshTokenHash;

            await AddRefreshTokenAsync(storedToken.User, refreshTokenHash);
            await _refreshTokenRepository.SaveChangesAsync();

            return Mapper.Map<AuthResponseDto>(new AuthTokenMapData
            {
                AccessToken = _jwtService.GenerateToken(storedToken.User, fileUrl),
                RefreshToken = refreshToken
            });
        }

        private async Task<string> AddRefreshTokenAsync(Domain.Entities.User user)
        {
            var refreshToken = _jwtService.GenerateRefreshToken();
            await AddRefreshTokenAsync(user, HashRefreshToken(refreshToken));
            return refreshToken;
        }

        private async Task AddRefreshTokenAsync(
            Domain.Entities.User user,
            string refreshTokenHash)
        {
            var refreshToken = Mapper.Map<Domain.Entities.UserRefreshToken>(new RefreshTokenMapData
            {
                UserId = user.Id,
                TokenHash = refreshTokenHash,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedByIp = GetRequestIp(),
                UserAgent = HttpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString()
            });

            await _refreshTokenRepository.Add(refreshToken);
        }

        private string? GetRequestIp()
            => HttpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        #endregion


    }
}
