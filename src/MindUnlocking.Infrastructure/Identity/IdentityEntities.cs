using Microsoft.AspNetCore.Identity;

namespace MindUnlocking.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public bool AdministrativeMfaRequired { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ApplicationRole : IdentityRole<Guid>
{
    public string Description { get; set; } = string.Empty;
}

public sealed class RefreshSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string? ReplacedByTokenHash { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresUtc { get; set; }
    public DateTimeOffset? RevokedUtc { get; set; }
    public string? RevocationReason { get; set; }
    public bool IsActive(DateTimeOffset nowUtc) => RevokedUtc is null && ExpiresUtc > nowUtc;
}
