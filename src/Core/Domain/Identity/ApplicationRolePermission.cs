namespace ManagementApi.Domain.Identity;

public class ApplicationRolePermission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoleId { get; set; }
    public string Permission { get; set; } = default!;

    // Navigation
    public ApplicationRole Role { get; set; } = default!;

    // Audit
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
}
