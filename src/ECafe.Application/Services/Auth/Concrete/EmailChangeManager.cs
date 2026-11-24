using System.Security.Cryptography;
using ECafe.Shared.Security;
using ECafe.Application.Common.Configuration;
using ECafe.Application.Common.Security;
using System.Net.Mail;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Outbox;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.Repositories.User;
using ECafe.Application.Repositories.UserRefreshToken;
using ECafe.Application.Repository;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Application.Services.Localization;
using ECafe.Application.Services.Localization.Concrete;
using ECafe.Domain.Entities;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.Auth.Concrete;

public sealed class EmailChangeManager(
    IUserRepository users, IUserRefreshTokenRepository refreshTokens,
    IBaseRepository<EmailChangeRequest> requests, IBaseRepository<PasswordResetToken> resetTokens,
    IBaseRepository<UserPasswordSetupToken> setupTokens, IBaseRepository<Domain.Entities.AuditLog> audit,
    IApplicationDbTransactionFactory transactions, IEmailOutboxService emails,
    ILoginAttemptService loginAttempts, IUserSessionStateCache sessions,
    IHttpContextAccessor http, IConfiguration configuration, TimeProvider clock,
    TranslationBootstrap translations) : IEmailChangeService
{
    private const int LifetimeMinutes = 30;
    private const int RequestLimit = 3;
    private static readonly TimeSpan RequestWindow = TimeSpan.FromMinutes(15);

    public async Task RequestAsync(RequestEmailChangeRequest request, CancellationToken cancellationToken = default)
    {
        new RequestEmailChangeValidator().ValidateAndThrow(request);
        var userId = CurrentUserId();
        await using var transaction = await transactions.BeginTransactionAsync(cancellationToken);
        await refreshTokens.AcquireUserLockAsync(userId);
        var user = await CurrentUserAsync(userId, cancellationToken);
        await loginAttempts.EnsureNotLockedOutAsync(user.Email);
        if (string.IsNullOrEmpty(user.Password) || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.Password))
        {
            await loginAttempts.RecordFailureAsync(user, user.Email, "EmailChangeReauthentication");
            await transaction.CommitAsync(cancellationToken);
            throw new BusinessRuleException(ErrorCode.InvalidCredentials);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (await requests.Query(x => x.UserId == userId && x.CreatedAt > now - RequestWindow)
            .CountAsync(cancellationToken) >= RequestLimit)
            throw new TooManyRequestsException();

        var pending = await requests.QueryTracked(x => x.UserId == userId && x.UsedAt == null && x.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var item in pending) item.RevokedAt = now;
        var newEmail = request.NewEmail.Trim().ToLowerInvariant();
        var unavailable = string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase)
            || await users.CheckExistAsync(x => x.Id != userId && x.Email == newEmail);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await requests.Add(new EmailChangeRequest
        {
            UserId = userId, OriginalEmail = user.Email, NewEmail = newEmail, TokenHash = AuthenticationTokenHelper.HashToken(token),
            SessionVersion = user.SessionVersion, CreatedAt = now, ExpiresAt = now.AddMinutes(LifetimeMinutes),
            RevokedAt = unavailable ? now : null
        });
        await RecordAuditAsync(userId, AuditActions.EmailChangeRequested, now);
        if (!unavailable)
        {
            var url = BuildConfirmationUrl(token);
            await EnqueueAsync(user, newEmail, "email.emailChange.confirmSubject", "email.emailChange.confirmBody",
                new Dictionary<string, object?> { ["url"] = url, ["minutes"] = LifetimeMinutes });
            await EnqueueAsync(user, user.Email, "email.emailChange.requestSubject", "email.emailChange.requestBody");
        }
        await requests.SaveChangesAsync();
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ConfirmAsync(ConfirmEmailChangeRequest request, CancellationToken cancellationToken = default)
    {
        new ConfirmEmailChangeValidator().ValidateAndThrow(request);
        var userId = CurrentUserId();
        await using var transaction = await transactions.BeginTransactionAsync(cancellationToken);
        await refreshTokens.AcquireUserLockAsync(userId);
        var user = await CurrentUserAsync(userId, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        var hash = AuthenticationTokenHelper.HashToken(request.Token);
        var pending = await requests.QueryTracked(x => x.UserId == userId && x.TokenHash == hash
            && x.UsedAt == null && x.RevokedAt == null && x.ExpiresAt > now).SingleOrDefaultAsync(cancellationToken);
        if (pending == null || pending.SessionVersion != user.SessionVersion
            || !string.Equals(pending.OriginalEmail, user.Email, StringComparison.OrdinalIgnoreCase)
            || await users.CheckExistAsync(x => x.Id != userId && x.Email == pending.NewEmail))
            throw new BusinessRuleException(ErrorCode.EmailChangeInvalidOrExpired);

        var originalEmail = user.Email;
        user.Email = pending.NewEmail;
        user.SessionVersion++;
        pending.UsedAt = now;
        var otherRequests = await requests.QueryTracked(x => x.UserId == userId && x.Id != pending.Id
            && x.UsedAt == null && x.RevokedAt == null).ToListAsync(cancellationToken);
        foreach (var item in otherRequests) item.RevokedAt = now;
        foreach (var item in await refreshTokens.GetActiveByUserIdTrackedAsync(userId, now)) item.RevokedAt = now;
        foreach (var item in await resetTokens.QueryTracked(x => x.UserId == userId && x.UsedAt == null
            && x.RevokedAt == null).ToListAsync(cancellationToken)) item.RevokedAt = now;
        foreach (var item in await setupTokens.QueryTracked(x => x.UserId == userId && x.UsedAt == null)
            .ToListAsync(cancellationToken)) item.UsedAt = now;

        await RecordAuditAsync(userId, AuditActions.EmailChangeConfirmed, now);
        // DB-nin unikal email indeksi başqa hesabın paralel təsdiqi/qeydiyyatı ilə yarışı da bağlayır.
        try
        {
            await EnqueueAsync(user, originalEmail, "email.emailChange.changedSubject", "email.emailChange.changedBody");
            await EnqueueAsync(user, user.Email, "email.emailChange.changedSubject", "email.emailChange.changedBody");
            await requests.SaveChangesAsync();
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is System.Data.Common.DbException { SqlState: "23505" })
        {
            throw new BusinessRuleException(ErrorCode.EmailChangeInvalidOrExpired);
        }
        await sessions.InvalidateAsync(userId);
    }

    /// <summary>Hesab identifikatorunu yalnız serverin təsdiqlədiyi principal-dan götürür; payload ilə başqa istifadəçi seçilə bilmir.</summary>
    private int CurrentUserId()
    {
        var principal = http.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true
            || !int.TryParse(principal.FindFirst(AuthenticationClaimTypes.UserId)?.Value, out var userId) || userId <= 0)
            throw new UnauthorizedException(ErrorCode.SessionInvalid);
        return userId;
    }

    /// <summary>Kiliddən sonra cari hesabı, sessiya versiyasını və cihazın ləğvini DB-dən yenidən yoxlayır.</summary>
    private async Task<Domain.Entities.User> CurrentUserAsync(int userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.GetProfileByIdTrackedAsync(userId);
        var principal = http.HttpContext!.User;
        var sessionId = principal.FindFirst(AuthenticationClaimTypes.SessionId)?.Value;
        if (user == null || !user.IsActive
            || !int.TryParse(principal.FindFirst(AuthenticationClaimTypes.SessionVersion)?.Value, out var version) || version != user.SessionVersion
            || (!string.IsNullOrEmpty(sessionId) && !await refreshTokens.HasActiveUserSessionAsync(userId, sessionId, clock.GetUtcNow().UtcDateTime)))
            throw new UnauthorizedException(ErrorCode.SessionInvalid);
        return user;
    }

    /// <summary>Parol/token/email məzmunu olmadan təhlükəsizlik hadisəsini əsas dəyişikliklə eyni transaction-da auditə yazır.</summary>
    private Task<Domain.Entities.AuditLog> RecordAuditAsync(int userId, string action, DateTime now)
        => audit.Add(new Domain.Entities.AuditLog
        {
            UserId = userId, EntityName = AuditEntityTypes.User, EntityId = userId,
            Action = action, OccurredAt = now, CreatedAt = now
        });

    /// <summary>Tokeni query deyil, fragment-də saxlayan sabit frontend linki yaradır; request Host və redirect parametrinə etibar etmir.</summary>
    private string BuildConfirmationUrl(string token)
    {
        var baseUrl = configuration[AuthenticationConfigurationKeys.FrontendBaseUrl];
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
            || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException("A trusted HTTPS Frontend:BaseUrl is required for email changes.");
        return uri.AbsoluteUri.TrimEnd('/') + "/confirm-email-change#token=" + Uri.EscapeDataString(token);
    }

    /// <summary>Məktubu alıcının dilində hazırlayıb mövcud transactional email outbox-a verir; token log/auditə yazılmır.</summary>
    private Task EnqueueAsync(Domain.Entities.User user, string recipient, string subjectKey, string bodyKey,
        IReadOnlyDictionary<string, object?>? arguments = null)
    {
        var messages = translations.Snapshot.ForLanguage(TranslationPolicy.NormalizeLanguage(user.PreferredLanguage)).Messages;
        return emails.EnqueueEmailAsync(recipient, $"{user.Name} {user.Surname}",
            TextLocalizer.Render(messages, subjectKey, subjectKey, null),
            TextLocalizer.Render(messages, bodyKey, bodyKey, arguments),
            OutboxAggregateTypes.User, user.Id, AuditEntityTypes.User, user.Id);
    }

}

public sealed class RequestEmailChangeValidator : AbstractValidator<RequestEmailChangeRequest>
{
    public RequestEmailChangeValidator()
    {
        RuleFor(x => x.NewEmail).NotEmpty().EmailAddress().MaximumLength(100);
        RuleFor(x => x.NewEmail).Must(IsSafeAddress).WithMessage("Email format is invalid.");
        RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(128);
    }

    /// <summary>Məktub başlığına sətir əlavə etməyi və display-name ünvanlarını bloklayır; yalnız tək email ünvanı qəbul edir.</summary>
    private static bool IsSafeAddress(string? value)
        => value != null && !value.Any(char.IsControl) && MailAddress.TryCreate(value.Trim(), out var address)
            && string.Equals(address.Address, value.Trim(), StringComparison.OrdinalIgnoreCase);
}

public sealed class ConfirmEmailChangeValidator : AbstractValidator<ConfirmEmailChangeRequest>
{
    public ConfirmEmailChangeValidator()
        => RuleFor(x => x.Token).NotEmpty().Matches("^[A-F0-9]{64}$");
}
