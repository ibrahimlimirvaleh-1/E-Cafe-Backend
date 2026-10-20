using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using AutoMapper;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Localization;
using ECafe.Application.Mappings;
using ECafe.Application.Repositories.Localization;
using ECafe.Application.Repositories.User;
using ECafe.Application.Repository;
using ECafe.Application.Services.Localization.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ECafe.Application.Services.Localization.Concrete;

public sealed class TranslationManager(IHttpContextAccessor accessor, IMapper mapper, IConfiguration configuration,
    ITranslationRepository repository, IUserRepository users, IApplicationDbTransactionFactory transactions,
    ITranslationCache cache, TranslationBootstrap bootstrap, TimeProvider clock, ILogger<TranslationManager> logger)
    : BaseManager(accessor, mapper, configuration), ITranslationService
{
    public Task<TranslationSnapshot> GetPublishedAsync(CancellationToken cancellationToken)
        => cache.GetAsync(LoadPublishedAsync, cancellationToken);

    /// <summary>Yalnız yayımlanmış versiyanı oxuyur; DB nasazlığında şəxsi məlumat olmayan build lüğətinə qayıdır, admin yazıları isə uğurlu sayılmır.</summary>
    private async Task<TranslationSnapshot> LoadPublishedAsync(CancellationToken cancellationToken)
    {
        try
        {
            var catalog = await repository.GetCatalogAsync(false, cancellationToken);
            if (catalog?.PublishedReleaseId is not int releaseId) return bootstrap.Snapshot;
            var release = await repository.GetReleaseAsync(releaseId, cancellationToken);
            if (release == null) return bootstrap.Snapshot;
            var entries = Merge(await repository.GetReleaseEntriesAsync(release.Id, cancellationToken));
            return new TranslationSnapshot($"{release.Id}-{bootstrap.Snapshot.Version}", entries);
        }
        catch (Exception exception) when (exception is DbException or TimeoutException or JsonException)
        {
            logger.LogWarning("Translation catalog unavailable; bundled fallback is used. FailureType: {FailureType}", exception.GetType().Name);
            return bootstrap.Snapshot;
        }
    }

    public async Task<TranslationDraftResponse> GetDraftAsync(CancellationToken cancellationToken)
    {
        await RequireAdministratorAsync(cancellationToken);
        var catalog = await repository.GetCatalogAsync(false, cancellationToken) ?? throw new BusinessRuleException(ErrorCode.LocalizationCatalogUnavailable);
        return TranslationMapper.Draft(catalog, Merge(await repository.GetDraftEntriesAsync(cancellationToken)));
    }

    public async Task<TranslationDraftResponse> SaveDraftAsync(SaveTranslationDraftRequest request, CancellationToken cancellationToken)
    {
        await RequireAdministratorAsync(cancellationToken);
        if (request?.Entries is null || request.Entries.Count is < 1 or > TranslationPolicy.MaxBatchSize ||
            request.Entries.Any(entry => entry == null || string.IsNullOrWhiteSpace(entry.Key)) ||
            request.Entries.Select(entry => entry.Key).Distinct(StringComparer.Ordinal).Count() != request.Entries.Count)
            throw new BusinessRuleException(ErrorCode.LocalizationInvalidEntry);
        await using var transaction = await transactions.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var catalog = await LockDraftAsync(request.ExpectedRevision, cancellationToken);
        var entries = Merge(await repository.GetDraftEntriesAsync(cancellationToken));
        foreach (var entry in request.Entries)
        {
            if (!bootstrap.Snapshot.Entries.ContainsKey(entry.Key)) throw new BusinessRuleException(ErrorCode.LocalizationInvalidEntry);
            var text = new TranslationText(entry.Az, entry.En, entry.Ru);
            TranslationPolicy.ValidateText(text);
            if (!TranslationPolicy.Placeholders(bootstrap.Snapshot.Entries[entry.Key].Az).SetEquals(TranslationPolicy.Placeholders(text.Az)))
                throw new BusinessRuleException(ErrorCode.LocalizationPlaceholderMismatch);
            entries[entry.Key] = text;
        }
        ValidateCatalogSize(entries);
        await repository.SaveDraftEntriesAsync(entries, cancellationToken);
        catalog.Revision++;
        await repository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TranslationMapper.Draft(catalog, entries);
    }

    public async Task<TranslationReleaseResponse> PublishAsync(long expectedRevision, int? rollbackReleaseId, CancellationToken cancellationToken)
    {
        await RequireAdministratorAsync(cancellationToken);
        await using var transaction = await transactions.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var catalog = await LockDraftAsync(expectedRevision, cancellationToken);
        var draft = await repository.GetDraftEntriesAsync(cancellationToken);
        if (rollbackReleaseId.HasValue)
        {
            var source = await repository.GetReleaseAsync(rollbackReleaseId.Value, cancellationToken)
                ?? throw new NotFoundException(ErrorCode.LocalizationReleaseNotFound);
            draft = await repository.GetReleaseEntriesAsync(source.Id, cancellationToken);
        }
        var entries = Merge(draft);
        foreach (var entry in entries.Values) TranslationPolicy.ValidateText(entry);
        ValidateCatalogSize(entries);
        await repository.SaveDraftEntriesAsync(entries, cancellationToken);
        var release = new TranslationRelease { CreatedAt = clock.GetUtcNow().UtcDateTime,
            CreatedByUserId = GetCurrentUserId(), SourceReleaseId = rollbackReleaseId };
        await repository.AddReleaseAsync(release, entries, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        catalog.PublishedReleaseId = release.Id;
        catalog.Revision++;
        await repository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        cache.Invalidate();
        return TranslationMapper.Release(release, catalog.Revision);
    }

    public async Task<IReadOnlyList<TranslationReleaseResponse>> GetReleasesAsync(CancellationToken cancellationToken)
    {
        await RequireAdministratorAsync(cancellationToken);
        var catalog = await repository.GetCatalogAsync(false, cancellationToken);
        return (await repository.GetReleasesAsync(cancellationToken)).Select(release => TranslationMapper.Release(release, catalog?.Revision ?? 0)).ToArray();
    }

    public async Task<LanguagePreferenceResponse> GetPreferenceAsync(CancellationToken cancellationToken)
    {
        var user = await FindCurrentUserAsync(false, cancellationToken);
        return new(TranslationPolicy.NormalizeLanguage(user.PreferredLanguage));
    }

    public async Task<LanguagePreferenceResponse> SavePreferenceAsync(string language, CancellationToken cancellationToken)
    {
        TranslationPolicy.ValidateLanguage(language);
        var user = await FindCurrentUserAsync(true, cancellationToken);
        user.PreferredLanguage = language;
        await users.SaveChangesAsync();
        return new(language);
    }

    /// <summary>Admin qərarını DB-nin cari aktiv platforma rolu ilə yoxlayır; köhnə token və restoran meneceri kataloqu dəyişə bilməz.</summary>
    private async Task RequireAdministratorAsync(CancellationToken cancellationToken)
    {
        var user = await FindCurrentUserAsync(false, cancellationToken);
        if (GetCurrentRoleId() != (int)RoleCode.SuperAdmin || user.RoleId != (int)RoleCode.SuperAdmin)
            throw new ForbiddenException(ErrorCode.AccessDenied);
    }
    /// <summary>İstifadəçi seçimini yalnız token sahibinin aktiv hesabına bağlayır.</summary>
    private async Task<Domain.Entities.User> FindCurrentUserAsync(bool tracked, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        return await (tracked ? users.QueryTracked() : users.Query()).SingleOrDefaultAsync(user => user.Id == userId && user.IsActive, cancellationToken)
            ?? throw new ForbiddenException(ErrorCode.AccessDenied);
    }
    /// <summary>Kataloq kilidindən sonra administratorun canlı rolunu və ekranın qaralama reviziyasını təkrar yoxlayır.</summary>
    private async Task<TranslationCatalog> LockDraftAsync(long expectedRevision, CancellationToken cancellationToken)
    {
        await repository.AcquireLockAsync(cancellationToken);
        await RequireAdministratorAsync(cancellationToken);
        var catalog = await repository.GetCatalogAsync(true, cancellationToken) ?? throw new BusinessRuleException(ErrorCode.LocalizationCatalogUnavailable);
        if (catalog.Revision != expectedRevision) throw new BusinessRuleException(ErrorCode.LocalizationDraftConflict);
        return catalog;
    }
    /// <summary>Yeni build açarlarını saxlayaraq bazadakı məlum tərcümələri əlavə edir; naməlum açarla sayt davranışını genişləndirmir.</summary>
    private Dictionary<string, TranslationText> Merge(IReadOnlyDictionary<string, TranslationText> overrides)
    {
        var entries = bootstrap.Snapshot.Entries.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var pair in overrides)
            if (entries.TryGetValue(pair.Key, out var original) && pair.Value != null &&
                TranslationPolicy.Placeholders(original.Az).SetEquals(TranslationPolicy.Placeholders(pair.Value.Az)))
                entries[pair.Key] = pair.Value with { Source = original.Source };
        return entries;
    }
    /// <summary>Kataloqun API/keş yükü üçün ümumi ölçüsünü yoxlayır; JSON nəticəsi bazada saxlanılmır.</summary>
    private static void ValidateCatalogSize(IReadOnlyDictionary<string, TranslationText> entries)
    {
        var json = JsonSerializer.Serialize(entries.OrderBy(entry => entry.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value), TranslationJson.Options);
        if (Encoding.UTF8.GetByteCount(json) > TranslationPolicy.MaxCatalogBytes) throw new BusinessRuleException(ErrorCode.LocalizationInvalidEntry);
    }
}
