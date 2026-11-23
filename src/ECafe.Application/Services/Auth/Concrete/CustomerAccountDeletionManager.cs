using System.Security.Claims;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.Repositories.User;
using ECafe.Application.Repositories.UserRefreshToken;
using ECafe.Application.Repository;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Application.Services.Realtime.Abstract;
using ECafe.Application.Services.Localization;
using ECafe.Domain.Policies;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using AccountUser = ECafe.Domain.Entities.User;
using AccountAudit = ECafe.Domain.Entities.AuditLog;

namespace ECafe.Application.Services.Auth.Concrete;

public sealed class CustomerAccountDeletionManager(IUserRepository users, IUserRefreshTokenRepository refreshTokens,
    ICustomerAccountClosureRepository closure, IApplicationDbTransactionFactory transactions,
    ILoginAttemptService loginAttempts, IUserSessionStateCache sessions, IUserRealtimeNotifier realtime,
    IBaseRepository<AccountAudit> audit, IHttpContextAccessor http, TimeProvider clock,
    TranslationBootstrap translations, ILogger<CustomerAccountDeletionManager> logger) : ICustomerAccountDeletionService
{
    public async Task<CustomerAccountDeletionEligibility> GetEligibilityAsync(CancellationToken cancellationToken)
    {
        var user = await CurrentCustomerAsync(cancellationToken);
        var blockers = await closure.GetBlockersAsync(user.Id, cancellationToken);
        return new(blockers.Count == 0, blockers.Select(value => value.ToString()).ToArray());
    }

    public async Task DeleteAsync(CustomerAccountDeletionRequest request, CancellationToken cancellationToken)
    {
        if (request is null) throw new BadRequestException(ErrorCode.RequestCannotBeNull);
        new CustomerAccountDeletionValidator().ValidateAndThrow(request);
        var userId = CurrentUserId();
        await using var transaction = await transactions.BeginTransactionAsync(cancellationToken);
        await refreshTokens.AcquireUserLockAsync(userId);
        var user = await CurrentCustomerAsync(cancellationToken);
        await loginAttempts.EnsureNotLockedOutAsync(user.Email);
        if (string.IsNullOrEmpty(user.Password) || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.Password))
        {
            await loginAttempts.RecordFailureAsync(user, user.Email, "CustomerAccountDeletionReauthentication");
            await transaction.CommitAsync(cancellationToken);
            throw new BusinessRuleException(ErrorCode.InvalidCredentials);
        }
        if ((await closure.GetBlockersAsync(userId, cancellationToken)).Count != 0)
            throw new BusinessRuleException(ErrorCode.AccountDeletionBlocked);

        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var token in await refreshTokens.GetActiveByUserIdTrackedAsync(userId, now)) token.RevokedAt = now;
        await closure.RevokeRecoveryAndPushAsync(userId, now, cancellationToken);
        AnonymizeAndClose(user);
        await audit.Add(new AccountAudit
        {
            UserId = userId, EntityName = AuditEntityTypes.User, EntityId = userId,
            Action = AuditActions.CustomerAccountDeleted, OccurredAt = now, CreatedAt = now
        });
        await users.SaveChangesAsync();
        await transaction.CommitAsync(cancellationToken);
        await sessions.InvalidateAsync(userId);
        await NotifyClosedAccountAsync(user);
    }

    /// <summary>Başqa hesab seçilməsin deyə identifikatoru yalnız təsdiqlənmiş giriş principal-ından alır.</summary>
    private int CurrentUserId()
    {
        var principal = http.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true || !int.TryParse(principal.FindFirst("userId")?.Value, out var id) || id <= 0)
            throw new UnauthorizedException(ErrorCode.SessionInvalid);
        return id;
    }

    /// <summary>Köhnə/müştəri kimi görünən işçi tokenini rədd edir; həqiqi rol, versiya və cihaz sessiyasını bazadan yoxlayır.</summary>
    private async Task<AccountUser> CurrentCustomerAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var userId = CurrentUserId();
        var principal = http.HttpContext!.User;
        var user = await users.GetProfileByIdTrackedAsync(userId);
        var sessionId = principal.FindFirst("sessionId")?.Value;
        if (user == null || !user.IsActive || string.IsNullOrEmpty(sessionId) ||
            !int.TryParse(principal.FindFirst("sessionVersion")?.Value, out var version) || version != user.SessionVersion ||
            !await refreshTokens.HasActiveUserSessionAsync(userId, sessionId, clock.GetUtcNow().UtcDateTime))
            throw new UnauthorizedException(ErrorCode.SessionInvalid);
        if (user.RoleId != (int)RoleCode.Customer || principal.FindFirst(ClaimTypes.Role)?.Value != ((int)RoleCode.Customer).ToString())
            throw new ForbiddenException(ErrorCode.AccountDeletionCustomerOnly);
        return user;
    }

    /// <summary>Hesabatların required-user əlaqələrini silmədən girişi bağlayır; profil şəklini gələcək anonim oxunuşdan çıxarır.</summary>
    private static void AnonymizeAndClose(AccountUser user)
    {
        var identity = Guid.NewGuid().ToString("N");
        user.IsActive = false;
        user.SessionVersion++;
        user.Name = "Deleted";
        user.Surname = "Customer";
        user.Email = $"closed-{identity}@account.invalid";
        user.Phone = $"closed-{identity}";
        user.Password = string.Empty;
        user.PasswordSetAt = null;
        user.Rating = null;
        if (user.File is { FileTypeId: (int)FileTypeCode.UserProfileImage } file)
            file.FileTypeId = (int)FileTypeCode.TemporaryUpload;
        user.File = null;
        user.FileId = null;
    }

    /// <summary>Commit-dən sonra digər cihazları xəbərdar edir; realtime dayanması bağlanmış hesabı uğursuz əməliyyat kimi göstərmir.</summary>
    private async Task NotifyClosedAccountAsync(AccountUser user)
    {
        var texts = translations.Snapshot.ForLanguage(TranslationPolicy.NormalizeLanguage(user.PreferredLanguage)).Messages;
        try { await realtime.NotifyUserDeactivatedAsync(user.Id, texts.GetValueOrDefault("account.customerDeletion.completed") ?? string.Empty); }
        catch (Exception error) { logger.LogWarning("Account closure realtime delivery unavailable ({ErrorType}).", error.GetType().Name); }
    }
}

public sealed class CustomerAccountDeletionValidator : AbstractValidator<CustomerAccountDeletionRequest>
{
    public CustomerAccountDeletionValidator()
    {
        RuleFor(value => value.CurrentPassword).NotEmpty().MaximumLength(128);
        RuleFor(value => value.Confirm).Equal(true);
    }
}
