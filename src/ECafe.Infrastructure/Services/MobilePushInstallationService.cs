using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ECafe.Application.Features.MobileApp;
using ECafe.Domain.Entities;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Context;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ECafe.Infrastructure.Services;

public sealed class MobilePushInstallationService(
    ECafeDbContext context,
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration,
    IDataProtectionProvider dataProtectionProvider) : IMobilePushInstallationService
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("ECafe.MobilePush.InstallationToken.v1");

    public async Task<MobilePushInstallationResponse> RegisterAsync(
        Guid installationId,
        RegisterMobilePushInstallationRequest request,
        CancellationToken cancellationToken)
    {
        var (userId, sessionId) = GetCurrentSession();
        ValidateRegistration(installationId, request);
        var now = DateTime.UtcNow;
        if (!await IsSessionActiveAsync(userId, sessionId, now, cancellationToken))
            throw new UnauthorizedException(ErrorCode.SessionInvalid);

        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.ExpoPushToken)));
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var existing = await context.MobilePushInstallations
            .SingleOrDefaultAsync(x => x.Id == installationId, cancellationToken);
        if (existing is { IsActive: true } && existing.UserId != userId &&
            await IsSessionActiveAsync(existing.UserId, existing.SessionId, now, cancellationToken))
            throw new BusinessRuleException("Installation belongs to another active account.");

        var tokenOwner = await context.MobilePushInstallations
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash && x.IsActive, cancellationToken);
        if (tokenOwner is not null && tokenOwner.Id != installationId)
        {
            if (tokenOwner.UserId != userId &&
                await IsSessionActiveAsync(tokenOwner.UserId, tokenOwner.SessionId, now, cancellationToken))
                throw new BusinessRuleException("Push token belongs to another active account.");

            tokenOwner.IsActive = false;
            tokenOwner.DeactivatedAt = now;
            await context.SaveChangesAsync(cancellationToken);
        }

        if (existing is null)
        {
            existing = new MobilePushInstallation { Id = installationId };
            context.MobilePushInstallations.Add(existing);
        }

        existing.UserId = userId;
        existing.SessionId = sessionId;
        existing.ExpoProjectId = request.ExpoProjectId;
        existing.TokenHash = tokenHash;
        existing.ProtectedToken = _protector.Protect(request.ExpoPushToken);
        existing.IsActive = true;
        existing.RegisteredAt = now;
        existing.DeactivatedAt = null;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new BusinessRuleException("Installation registration changed concurrently. Retry the request.");
        }

        return new(installationId, true);
    }

    public async Task DeactivateAsync(Guid installationId, CancellationToken cancellationToken)
    {
        var (userId, sessionId) = GetCurrentSession();
        var installation = await context.MobilePushInstallations
            .SingleOrDefaultAsync(x => x.Id == installationId &&
                x.UserId == userId && x.SessionId == sessionId && x.IsActive,
                cancellationToken);
        if (installation is null)
            return;

        installation.IsActive = false;
        installation.DeactivatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    private void ValidateRegistration(Guid installationId, RegisterMobilePushInstallationRequest request)
    {
        if (installationId == Guid.Empty || request is null ||
            string.IsNullOrWhiteSpace(request.ExpoPushToken) ||
            request.ExpoPushToken.Length > 256 ||
            !IsExpoToken(request.ExpoPushToken))
            throw new BusinessRuleException("Invalid mobile push installation.");

        if (!configuration.GetValue<bool>("MobileApp:PushDeliveryReady") ||
            !Guid.TryParse(configuration["MobileApp:ExpoProjectId"], out var configuredProjectId) ||
            configuredProjectId != request.ExpoProjectId)
            throw new BusinessRuleException("Mobile push registration is not ready for this app release.");
    }

    private static bool IsExpoToken(string value)
    {
        var prefix = value.StartsWith("ExpoPushToken[", StringComparison.Ordinal)
            ? "ExpoPushToken[" : value.StartsWith("ExponentPushToken[", StringComparison.Ordinal)
                ? "ExponentPushToken[" : null;
        return prefix is not null && value.EndsWith(']') &&
            value.Length - prefix.Length - 1 is >= 10 and <= 200 &&
            value.AsSpan(prefix.Length, value.Length - prefix.Length - 1)
                .ToArray().All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    }

    private (int UserId, string SessionId) GetCurrentSession()
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (!int.TryParse(user?.FindFirstValue("userId"), out var userId) || userId <= 0 ||
            user?.FindFirstValue("sessionId") is not { Length: 32 } sessionId)
            throw new UnauthorizedException(ErrorCode.SessionInvalid);

        return (userId, sessionId);
    }

    private Task<bool> IsSessionActiveAsync(int userId, string sessionId, DateTime now, CancellationToken cancellationToken)
        => context.UserRefreshTokens.AnyAsync(x =>
            x.UserId == userId && x.SessionId == sessionId &&
            x.RevokedAt == null && x.ExpiresAt > now && x.User.IsActive,
            cancellationToken);
}
