namespace ManagementApi.Domain.Common;

public interface ISoftDelete
{
    DateTime? DeletedOn { get; set; }
    Guid? DeletedBy { get; set; }
    bool IsDeleted { get; set; }
}
