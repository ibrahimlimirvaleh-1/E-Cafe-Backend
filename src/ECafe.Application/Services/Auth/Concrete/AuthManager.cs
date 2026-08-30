using AutoMapper;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.Common.Validation;
using ECafe.Application.Repositories.File;
using ECafe.Application.Repositories.User;
using ECafe.Application.Repositories.UserRefreshToken;
using ECafe.Application.Services;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Application.Services.Monitoring.Abstract;
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
        private readonly IEmailOutboxService _emailOutboxService;
        private readonly ICriticalEventReporter _criticalEventReporter;
        public AuthManager(IHttpContextAccessor httpContextAccessor,
                           IMapper mapper,
                           IConfiguration configuration,
                           IUserRepository userRepository,
                           IUserRefreshTokenRepository refreshTokenRepository,
                           IJwtService jwtService,
                           IMinioService minioService,
                           IFileRepository fileRepository,
                           ILoginAttemptService loginAttemptService,
                           IEmailOutboxService emailOutboxService,
                           ICriticalEventReporter criticalEventReporter)
                           : base(httpContextAccessor, mapper, configuration)
        {
            _userRepository = userRepository;
            _refreshTokenRepository = refreshTokenRepository;
            _jwtService = jwtService;
            _minioService = minioService;
            _fileRepository = fileRepository;
            _loginAttemptService = loginAttemptService;
            _emailOutboxService = emailOutboxService;
            _criticalEventReporter = criticalEventReporter;
        }

        public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
        {
            if (request is null)
                throw new BusinessRuleException(ErrorCode.RequestCannotBeNull);

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            await _loginAttemptService.EnsureNotLockedOutAsync(normalizedEmail);

            var user = await _userRepository.GetByEmailTrackedAsync(normalizedEmail);

            if (user is null)
            {
                await _loginAttemptService.RecordFailureAsync(null, normalizedEmail, "InvalidCredentials");
                throw new UnauthorizedException(ErrorCode.InvalidCredentials);
            }

            if (user.PasswordSetAt is null || string.IsNullOrWhiteSpace(user.Password))
            {
                await _loginAttemptService.RecordFailureAsync(user, normalizedEmail, "PasswordNotSet");
                throw new ForbiddenException("Password has not been set yet.");
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
            user.PasswordSetAt = DateTime.UtcNow;

            await _userRepository.Add(user);
            await _userRepository.SaveChangesAsync();

            return await CreateAndStoreTokenResponseAsync(user);
        }

        public async Task<AuthResponseDto> RefreshAsync(RefreshTokenRequestDto request)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.RefreshToken))
                throw new BusinessRuleException(ErrorCode.RefreshTokenInvalid);

            var refreshTokenHash = HashRefreshToken(request.RefreshToken);
            var storedToken = await _refreshTokenRepository.GetByTokenHashTrackedAsync(refreshTokenHash);

            if (storedToken is null)
                throw new ForbiddenException(ErrorCode.RefreshTokenInvalid);

            if (storedToken.RevokedAt is not null)
                await HandleRefreshTokenReuseAsync(storedToken);

            if (storedToken.ExpiresAt <= DateTime.UtcNow)
                throw new ForbiddenException(ErrorCode.RefreshTokenInvalid);

            if (!storedToken.User.IsActive)
                throw new ForbiddenException("User account is inactive.");

            return await RotateRefreshTokenAsync(storedToken);
        }

        public async Task LogoutAsync(LogoutRequestDto request)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.RefreshToken))
                throw new BusinessRuleException(ErrorCode.RefreshTokenInvalid);

            var refreshTokenHash = HashRefreshToken(request.RefreshToken);
            var storedToken = await _refreshTokenRepository.GetByTokenHashTrackedAsync(refreshTokenHash);

            if (storedToken is null)
                return;

            if (storedToken.RevokedAt is null)
            {
                RevokeRefreshToken(storedToken);
                await _refreshTokenRepository.SaveChangesAsync();
            }
        }

        public async Task LogoutAllAsync()
        {
            var currentUserId = GetCurrentUserId();
            var currentUser = await _userRepository.GetByIdAsync(currentUserId);
            if (currentUser is null)
                throw new UnauthorizedException(ErrorCode.SessionInvalid);

            currentUser.SessionVersion++;
            var activeTokens = await _refreshTokenRepository.GetActiveByUserIdTrackedAsync(currentUserId, DateTime.UtcNow);

            foreach (var token in activeTokens)
            {
                RevokeRefreshToken(token);
            }

            await _userRepository.SaveChangesAsync();
        }

        #region Helpers
        private async Task EnsureUserDoesNotExistAsync(string email, string phone)
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();
            var normalizedPhone = PhoneNumberValidationExtensions.NormalizeAzerbaijanPhoneNumber(phone);

            var emailExists = await _userRepository.CheckExistAsync(x => x.Email == normalizedEmail);
            if (emailExists)
                throw new BusinessRuleException(ErrorCode.UserEmailAlreadyExists);

            var phoneExists = await _userRepository.CheckExistAsync(x => x.Phone == normalizedPhone);
            if (phoneExists)
                throw new BusinessRuleException(ErrorCode.UserPhoneAlreadyExists);
        }

        private async Task<Domain.Entities.File?> GetAttachableFileAsync(int? fileId)
        {
            if (!fileId.HasValue)
                return null;

            var file = await _fileRepository.GetAttachableByIdAsync(fileId.Value);
            if (file is null)
                throw new BusinessRuleException(ErrorCode.FileNotFoundOrAlreadyAttached);

            file.FileTypeId = (int)FileTypeCode.UserProfileImage;

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

        private async Task HandleRefreshTokenReuseAsync(Domain.Entities.UserRefreshToken reusedToken)
        {
            await _criticalEventReporter.CaptureAsync(new CriticalEvent(
                Category: "auth",
                Name: "refresh_token_reuse",
                Severity: CriticalEventSeverity.Critical,
                Properties: new Dictionary<string, string?>
                {
                    ["errorCode"] = ErrorCode.RefreshTokenReuseDetected.ToString(),
                    ["hasReplacementToken"] = (!string.IsNullOrWhiteSpace(reusedToken.ReplacedByTokenHash)).ToString(),
                    ["wasRevoked"] = (reusedToken.RevokedAt is not null).ToString()
                }));

            reusedToken.User.SessionVersion++;
            await RevokeAllActiveRefreshTokensAsync(reusedToken.UserId);
            await _refreshTokenRepository.SaveChangesAsync();
            await EnqueueRefreshTokenReuseEmailAsync(reusedToken.User);

            throw new ForbiddenException(ErrorCode.RefreshTokenReuseDetected);
        }

        private async Task RevokeAllActiveRefreshTokensAsync(int userId)
        {
            var activeTokens = await _refreshTokenRepository.GetActiveByUserIdTrackedAsync(userId, DateTime.UtcNow);
            foreach (var token in activeTokens)
            {
                RevokeRefreshToken(token);
            }
        }

        private void RevokeRefreshToken(Domain.Entities.UserRefreshToken refreshToken)
        {
            refreshToken.RevokedAt ??= DateTime.UtcNow;
            refreshToken.RevokedByIp ??= GetRequestIp();
        }

        private Task EnqueueRefreshTokenReuseEmailAsync(Domain.Entities.User user)
        {
            var ipAddress = GetRequestIp() ?? "unknown";
            var userAgent = HttpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString();
            var body = $"""
            Salam {user.Name},

            Hesabinizda refresh token tekrar istifadəsi askarlandi. Tehlukesizlik ucun butun aktiv sessiyalar baglandi.

            IP address: {ipAddress}
            Cihaz/browser: {userAgent}

            Bu emeliyyati siz etmemisinizse, sifrenizi yenileyin.
            """;

            return _emailOutboxService.EnqueueEmailAsync(
                user.Email,
                $"{user.Name} {user.Surname}",
                "ECafe hesabinizda sessiya tehlukesizliyi xeberdarligi",
                body,
                "RefreshToken",
                user.Id,
                "User",
                user.Id);
        }

        private string? GetRequestIp()
            => HttpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        #endregion


    }
}
