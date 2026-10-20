namespace ECafe.Domain.Policies;

public sealed record ContentTranslationFields(string? Name = null, string? Description = null,
    string? Location = null, string? BranchName = null, string? UnavailableReason = null);
