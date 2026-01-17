using ManagementApi.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace ManagementApi.Domain.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string? ChandaNo { get; set; } // Link to Member
    public Guid? MemberId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;

    // Organization context - populated from Member/Jamaat
    public Guid? MuqamId { get; set; }
    public Guid? DilaId { get; set; }
    public Guid? ZoneId { get; set; }
    public OrganizationLevel? OrganizationLevel { get; set; }

    // Audit
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? LastModifiedOn { get; set; }
    public Guid? LastModifiedBy { get; set; }
}
