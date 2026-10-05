using Microsoft.AspNetCore.Identity;

namespace HRS.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public required string Name { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>Which role assignment the unit switcher currently shows (FR-AC-002). Account-wide, not per-session.</summary>
    public Guid? ActiveRoleAssignmentId { get; set; }
}
