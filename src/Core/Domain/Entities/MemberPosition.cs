using ManagementApi.Domain.Common;
using ManagementApi.Domain.Enums;

namespace ManagementApi.Domain.Entities;

/// <summary>
/// Represents a position/role held by a member in the organization
/// </summary>
public class MemberPosition : AuditableEntity, IAggregateRoot
{
    public Guid MemberId { get; private set; }
    public string PositionTitle { get; private set; } = default!;
    public OrganizationLevel OrganizationLevel { get; private set; }
    public Guid? OrganizationEntityId { get; private set; } // ID of Zone, Dila, Muqam, or Jamaat
    public DateTime StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public bool IsActive { get; private set; }
    public string? Responsibilities { get; private set; }

    // Navigation properties
    public Member? Member { get; private set; }

    private MemberPosition() { } // EF Core

    public MemberPosition(
        Guid memberId,
        string positionTitle,
        OrganizationLevel organizationLevel,
        Guid? organizationEntityId,
        DateTime startDate,
        string? responsibilities = null)
    {
        MemberId = memberId;
        PositionTitle = positionTitle;
        OrganizationLevel = organizationLevel;
        OrganizationEntityId = organizationEntityId;
        StartDate = startDate;
        IsActive = true;
        Responsibilities = responsibilities;
    }

    public void UpdatePosition(string positionTitle, string? responsibilities)
    {
        PositionTitle = positionTitle;
        Responsibilities = responsibilities;
    }

    public void EndPosition(DateTime endDate)
    {
        EndDate = endDate;
        IsActive = false;
    }

    public void ReactivatePosition()
    {
        EndDate = null;
        IsActive = true;
    }
}
