namespace MindUnlocking.Infrastructure.Identity;

public sealed class ApplicationUser
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public bool MfaEnabled { get; set; }
    public bool AdministrativeMfaRequired { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public IReadOnlyCollection<string> Permissions { get; set; } = [];
}

public sealed class RefreshSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public string? ReplacedByTokenHash { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresUtc { get; set; }
    public DateTimeOffset? RevokedUtc { get; set; }
    public string? RevocationReason { get; set; }
    public bool IsActive(DateTimeOffset nowUtc) => RevokedUtc is null && ExpiresUtc > nowUtc;
}
