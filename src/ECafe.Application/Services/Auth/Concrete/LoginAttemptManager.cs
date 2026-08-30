using ECafe.Application.Repositories.LoginAttempt;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Services.Monitoring.Abstract;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.Auth.Concrete;

public class LoginAttemptManager : ILoginAttemptService
{
    private readonly ILoginAttemptRepository _loginAttemptRepository;
    private readonly IEmailOutboxService _emailOutboxService;
    private readonly ICriticalEventReporter _criticalEventReporter;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly int _maxFailedAttempts;
    private readonly TimeSpan _failedAttemptWindow;
    private readonly TimeSpan _lockoutDuration;
    private readonly bool _notifyOnLockout;

    public LoginAttemptManager(
        ILoginAttemptRepository loginAttemptRepository,
        IEmailOutboxService emailOutboxService,
        ICriticalEventReporter criticalEventReporter,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration)
    {
        _loginAttemptRepository = loginAttemptRepository;
        _emailOutboxService = emailOutboxService;
        _criticalEventReporter = criticalEventReporter;
        _httpContextAccessor = httpContextAccessor;
        _maxFailedAttempts = GetPositiveInt(configuration, "LoginSecurity:MaxFailedAttempts", 5);
        _failedAttemptWindow = TimeSpan.FromMinutes(GetPositiveInt(configuration, "LoginSecurity:FailedAttemptWindowMinutes", 15));
        _lockoutDuration = TimeSpan.FromMinutes(GetPositiveInt(configuration, "LoginSecurity:LockoutMinutes", 15));
        _notifyOnLockout = GetBool(configuration, "LoginSecurity:NotifyOnLockout", true);
    }

    public async Task EnsureNotLockedOutAsync(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        var now = DateTime.UtcNow;
        var lockoutUntil = await _loginAttemptRepository.GetActiveLockoutUntilAsync(normalizedEmail, now);

        if (lockoutUntil.HasValue)
            throw new ForbiddenException(ErrorCode.AccountTemporarilyLocked);
    }

    public async Task RecordFailureAsync(Domain.Entities.User? user, string email, string failureReason)
    {
        var normalizedEmail = NormalizeEmail(email);
        var now = DateTime.UtcNow;
        var failedSince = now.Subtract(_failedAttemptWindow);
        var failedCount = await _loginAttemptRepository.CountFailedAttemptsAsync(normalizedEmail, failedSince);
        var shouldLock = failedCount + 1 >= _maxFailedAttempts;
        var lockoutUntil = shouldLock ? now.Add(_lockoutDuration) : (DateTime?)null;

        await _loginAttemptRepository.Add(new Domain.Entities.LoginAttempt
        {
            UserId = user?.Id,
            Email = normalizedEmail,
            IsSuccessful = false,
            FailureReason = failureReason,
            IpAddress = GetClientIpAddress(),
            UserAgent = GetUserAgent(),
            AttemptedAt = now,
            LockoutUntil = lockoutUntil
        });

        await _loginAttemptRepository.SaveChangesAsync();

        if (shouldLock)
        {
            await _criticalEventReporter.CaptureAsync(new CriticalEvent(
                Category: "auth",
                Name: "login_lockout",
                Severity: CriticalEventSeverity.Warning,
                Properties: new Dictionary<string, string?>
                {
                    ["failureReason"] = failureReason,
                    ["failedAttemptCount"] = (failedCount + 1).ToString(),
                    ["maxFailedAttempts"] = _maxFailedAttempts.ToString(),
                    ["lockoutMinutes"] = _lockoutDuration.TotalMinutes.ToString("0")
                }));
        }

        if (shouldLock && user is not null && _notifyOnLockout)
            await EnqueueLockoutEmailAsync(user, lockoutUntil!.Value);
    }

    public async Task RecordSuccessAsync(Domain.Entities.User user, string email)
    {
        await _loginAttemptRepository.Add(new Domain.Entities.LoginAttempt
        {
            UserId = user.Id,
            Email = NormalizeEmail(email),
            IsSuccessful = true,
            IpAddress = GetClientIpAddress(),
            UserAgent = GetUserAgent(),
            AttemptedAt = DateTime.UtcNow
        });

        await _loginAttemptRepository.SaveChangesAsync();
    }

    private Task EnqueueLockoutEmailAsync(Domain.Entities.User user, DateTime lockoutUntil)
    {
        var ipAddress = GetClientIpAddress() ?? "unknown";
        var userAgent = GetUserAgent() ?? "unknown";
        var body = $"""
        Salam {user.Name},

        Hesabinizda ardicil ugursuz giris cehdleri qeyde alindi ve hesab muveqqeti bloklandi.

        IP address: {ipAddress}
        Cihaz/browser: {userAgent}
        Blok bitme vaxti (UTC): {lockoutUntil:yyyy-MM-dd HH:mm}

        Bu cehdleri siz etmemisinizse, sifrenizi yenileyin ve hesab tehlukesizliyini yoxlayin.
        """;

        return _emailOutboxService.EnqueueEmailAsync(
            user.Email,
            $"{user.Name} {user.Surname}",
            "ECafe hesabinizda ugursuz giris cehdleri",
            body,
            "LoginAttempt",
            user.Id,
            "User",
            user.Id);
    }

    private string? GetClientIpAddress()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null)
            return null;

        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
            return forwardedFor.Split(',')[0].Trim();

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(realIp))
            return realIp.Trim();

        return context.Connection.RemoteIpAddress?.ToString();
    }

    private string? GetUserAgent()
        => _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].FirstOrDefault();

    private static string NormalizeEmail(string email)
        => email.Trim().ToLowerInvariant();

    private static int GetPositiveInt(IConfiguration configuration, string key, int fallback)
    {
        var value = configuration[key];
        return int.TryParse(value, out var parsed) && parsed > 0
            ? parsed
            : fallback;
    }

    private static bool GetBool(IConfiguration configuration, string key, bool fallback)
    {
        var value = configuration[key];
        return bool.TryParse(value, out var parsed)
            ? parsed
            : fallback;
    }
}
