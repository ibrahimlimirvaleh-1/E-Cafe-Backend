namespace ECafe.Domain.Policies;

public sealed record ContentTranslationFields(string? Name = null, string? Description = null,
    string? UnavailableReason = null);
