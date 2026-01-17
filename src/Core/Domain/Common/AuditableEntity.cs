namespace ManagementApi.Domain.Common;

public abstract class AuditableEntity : BaseEntity
{
    public DateTime CreatedOn { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? LastModifiedOn { get; set; }
    public Guid? LastModifiedBy { get; set; }
}
