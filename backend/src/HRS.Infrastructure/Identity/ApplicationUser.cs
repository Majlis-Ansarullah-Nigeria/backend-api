using Microsoft.AspNetCore.Identity;

namespace HRS.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public required string Name { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }
}
