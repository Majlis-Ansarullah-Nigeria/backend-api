using Microsoft.AspNetCore.Identity;

namespace ManagementApi.Domain.Identity;

public class ApplicationRole : IdentityRole<Guid>
{
    public string? Description { get; set; }

    // Navigation
    public ICollection<ApplicationRolePermission> RolePermissions { get; set; } = new List<ApplicationRolePermission>();

    // Audit
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? LastModifiedOn { get; set; }
    public Guid? LastModifiedBy { get; set; }
}
