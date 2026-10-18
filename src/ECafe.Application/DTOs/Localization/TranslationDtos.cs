using System.Text.Json.Serialization;
using ECafe.Domain.Policies;

namespace ECafe.Application.DTOs.Localization;

public sealed record TranslationDictionaryResponse(string Language, string Version, string FallbackLanguage, IReadOnlyDictionary<string, string> Messages);
public sealed record TranslationEntryResponse(string Key, string Az, string En, string Ru);
public sealed record TranslationDraftResponse(long Revision, string? PublishedVersion, IReadOnlyList<TranslationEntryResponse> Entries);
public sealed record TranslationReleaseResponse(int Id, long Revision, DateTimeOffset CreatedAt, int CreatedByUserId, int? SourceReleaseId);
public sealed record LanguagePreferenceResponse(string Language);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SaveTranslationDraftRequest
{
    [JsonRequired] public long ExpectedRevision { get; init; }
    [JsonRequired] public List<TranslationEntryResponse> Entries { get; init; } = [];
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PublishTranslationRequest
{
    [JsonRequired] public long ExpectedRevision { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class LanguagePreferenceRequest
{
    [JsonRequired] public string Language { get; init; } = TranslationPolicy.DefaultLanguage;
}
