namespace ECafe.Application.DTOs.Auth;

public sealed class CustomerAccountDeletionRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public bool Confirm { get; set; }
}

public sealed record CustomerAccountDeletionEligibility(bool CanDelete, IReadOnlyList<string> Blockers);
