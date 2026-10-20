using System.Data;
using ECafe.Application.Common.Audit;
using ECafe.Application.DTOs.ContentLocalization;
using ECafe.Application.Mappings;
using ECafe.Application.Repositories.ContentLocalization;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.ContentLocalization.Abstract;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Application.Services.ContentLocalization.Concrete;

public sealed class ContentTranslationManager(IContentTranslationRepository repository,
    IContentLanguageRepository languages, IContentTranslationAccessService access,
    IApplicationDbTransactionFactory transactions, IAuditLogService? audit = null) : IContentTranslationService
{
    public async Task<IReadOnlyList<ContentLanguageResponse>> GetLanguagesAsync(CancellationToken cancellationToken)
        => (await languages.GetLanguagesAsync(cancellationToken)).Where(language => language.IsActive && TranslationPolicy.Languages.Contains(language.Code))
            .Select(language => new ContentLanguageResponse(language.Id, language.Code, language.Name ?? language.Code, language.IsDefault)).ToList();

    public async Task<ContentTranslationsResponse> GetAsync(string entityType, int id, CancellationToken cancellationToken)
    {
        var type = ContentTranslationEditorMapper.ParseType(entityType);
        var source = await access.RequireAsync(type, id, cancellationToken);
        return await ReadAsync(entityType, type, source, cancellationToken);
    }

    public async Task<ContentTranslationsResponse> SaveAsync(string entityType, int id, string language,
        SaveContentTranslationRequest request, CancellationToken cancellationToken)
    {
        var type = ContentTranslationEditorMapper.ParseType(entityType);
        ContentTranslationPolicy.RequireRevision(request?.ExpectedRevision);
        var value = ContentTranslationEditorMapper.Value(ContentTranslationPolicy.Normalize(type,
            new(request!.Name, request.Description, request.UnavailableReason)));
        await access.RequireAsync(type, id, cancellationToken);
        await using var transaction = await transactions.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await repository.AcquireLockAsync(type, id, cancellationToken);
        var source = await access.RequireAsync(type, id, cancellationToken);
        var targetLanguage = await RequireEditableLanguageAsync(language, cancellationToken);
        var current = (await repository.GetRowsAsync(type, [id], language, cancellationToken)).SingleOrDefault();
        ContentTranslationPolicy.RequireCurrentRevision(current?.Revision ?? 0, request.ExpectedRevision!.Value);
        try
        {
            var revision = checked((current?.Revision ?? 0) + 1);
            await repository.SaveAsync(type, id, targetLanguage.Id, value, revision, cancellationToken);
            await RecordAuditAsync(type, source, language, revision, false);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { throw new BusinessRuleException(ErrorCode.LocalizationDraftConflict); }
        catch (DbUpdateException exception) when (exception.InnerException is System.Data.Common.DbException { SqlState: "23505" })
        { throw new BusinessRuleException(ErrorCode.LocalizationDraftConflict); }
        return await ReadAsync(entityType, type, source, cancellationToken);
    }

    public async Task DeleteAsync(string entityType, int id, string language, long? expectedRevision, CancellationToken cancellationToken)
    {
        var type = ContentTranslationEditorMapper.ParseType(entityType);
        ContentTranslationPolicy.RequireRevision(expectedRevision);
        await access.RequireAsync(type, id, cancellationToken);
        await using var transaction = await transactions.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await repository.AcquireLockAsync(type, id, cancellationToken);
        var source = await access.RequireAsync(type, id, cancellationToken);
        var targetLanguage = await RequireEditableLanguageAsync(language, cancellationToken);
        var current = (await repository.GetRowsAsync(type, [id], language, cancellationToken)).SingleOrDefault();
        ContentTranslationPolicy.RequireCurrentRevision(current?.Revision ?? 0, expectedRevision!.Value);
        try
        {
            if (current != null)
            {
                await repository.DeleteAsync(type, id, targetLanguage.Id, expectedRevision.Value, cancellationToken);
                await RecordAuditAsync(type, source, language, expectedRevision.Value, true);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { throw new BusinessRuleException(ErrorCode.LocalizationDraftConflict); }
    }

    /// <summary>Eyni transaction-dakı auditə yalnız dil və reviziya metadata-sını əlavə edir; tərcümə mətnlərini loglamır.</summary>
    private Task RecordAuditAsync(ContentEntityType type, ContentTranslationSource source, string language, long revision, bool deleted)
        => audit != null && source.RestaurantId is int restaurantId
            ? audit.RecordRestaurantActionAsync(restaurantId,
                deleted ? ContentTranslationAuditActions.Deleted : ContentTranslationAuditActions.Updated,
                new { LanguageCode = language, Revision = revision }, type.ToString(), source.Id)
            : Task.CompletedTask;

    /// <summary>AZ mənbəni ayrıca redaktədən qoruyur və seçilmiş əlavə dilin bazada aktiv olduğunu yoxlayır.</summary>
    private async Task<ContentLanguage> RequireEditableLanguageAsync(string language, CancellationToken cancellationToken)
    {
        TranslationPolicy.ValidateLanguage(language);
        if (language == TranslationPolicy.DefaultLanguage) throw new BusinessRuleException(ErrorCode.LocalizationInvalidEntry);
        return (await languages.GetLanguagesAsync(cancellationToken)).SingleOrDefault(row => row.Code == language && row.IsActive)
            ?? throw new BusinessRuleException(ErrorCode.LocalizationInvalidLanguage);
    }

    /// <summary>Xam mənbə və dil sətirlərini oxuyur; cavabın hazırlanmasını ayrıca mapper-ə ötürür.</summary>
    private async Task<ContentTranslationsResponse> ReadAsync(string entityType, ContentEntityType type,
        ContentTranslationSource source, CancellationToken cancellationToken)
        => ContentTranslationEditorMapper.Response(entityType, source,
            await repository.GetRowsAsync(type, [source.Id], null, cancellationToken),
            await languages.GetLanguagesAsync(cancellationToken));
}
