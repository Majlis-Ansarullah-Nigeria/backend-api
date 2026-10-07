using Microsoft.AspNetCore.Identity;

namespace HRS.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public required string Name { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>Which role assignment the unit switcher currently shows (FR-AC-002). Account-wide, not per-session.</summary>
    public Guid? ActiveRoleAssignmentId { get; set; }

    /// <summary>Admin-deactivated account (FR-ADM-002) — distinct from Identity's own lockout, which is transient and self-clearing.</summary>
    public DateTimeOffset? DeactivatedAt { get; set; }
}
