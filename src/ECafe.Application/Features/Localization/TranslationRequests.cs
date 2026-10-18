using ECafe.Application.DTOs.Localization;
using ECafe.Application.Services.Localization;
using ECafe.Application.Services.Localization.Abstract;
using MediatR;

namespace ECafe.Application.Features.Localization;

public sealed record TranslationDictionaryQuery : IRequest<TranslationSnapshot>;
public sealed record TranslationDraftQuery : IRequest<TranslationDraftResponse>;
public sealed record SaveTranslationDraftCommand(SaveTranslationDraftRequest Request) : IRequest<TranslationDraftResponse>;
public sealed record PublishTranslationCommand(long ExpectedRevision, int? SourceReleaseId) : IRequest<TranslationReleaseResponse>;
public sealed record TranslationReleasesQuery : IRequest<IReadOnlyList<TranslationReleaseResponse>>;
public sealed record LanguagePreferenceQuery : IRequest<LanguagePreferenceResponse>;
public sealed record SaveLanguagePreferenceCommand(string Language) : IRequest<LanguagePreferenceResponse>;

public sealed class TranslationRequestHandlers(ITranslationService service) :
    IRequestHandler<TranslationDictionaryQuery, TranslationSnapshot>,
    IRequestHandler<TranslationDraftQuery, TranslationDraftResponse>,
    IRequestHandler<SaveTranslationDraftCommand, TranslationDraftResponse>,
    IRequestHandler<PublishTranslationCommand, TranslationReleaseResponse>,
    IRequestHandler<TranslationReleasesQuery, IReadOnlyList<TranslationReleaseResponse>>,
    IRequestHandler<LanguagePreferenceQuery, LanguagePreferenceResponse>,
    IRequestHandler<SaveLanguagePreferenceCommand, LanguagePreferenceResponse>
{
    public Task<TranslationSnapshot> Handle(TranslationDictionaryQuery request, CancellationToken cancellationToken) => service.GetPublishedAsync(cancellationToken);
    public Task<TranslationDraftResponse> Handle(TranslationDraftQuery request, CancellationToken cancellationToken) => service.GetDraftAsync(cancellationToken);
    public Task<TranslationDraftResponse> Handle(SaveTranslationDraftCommand request, CancellationToken cancellationToken) => service.SaveDraftAsync(request.Request, cancellationToken);
    public Task<TranslationReleaseResponse> Handle(PublishTranslationCommand request, CancellationToken cancellationToken) => service.PublishAsync(request.ExpectedRevision, request.SourceReleaseId, cancellationToken);
    public Task<IReadOnlyList<TranslationReleaseResponse>> Handle(TranslationReleasesQuery request, CancellationToken cancellationToken) => service.GetReleasesAsync(cancellationToken);
    public Task<LanguagePreferenceResponse> Handle(LanguagePreferenceQuery request, CancellationToken cancellationToken) => service.GetPreferenceAsync(cancellationToken);
    public Task<LanguagePreferenceResponse> Handle(SaveLanguagePreferenceCommand request, CancellationToken cancellationToken) => service.SavePreferenceAsync(request.Language, cancellationToken);
}
