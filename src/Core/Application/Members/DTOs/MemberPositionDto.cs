using ManagementApi.Domain.Enums;

namespace ManagementApi.Application.Members.DTOs;

public record MemberPositionDto
{
    public Guid Id { get; init; }
    public Guid MemberId { get; init; }
    public string PositionTitle { get; init; } = default!;
    public OrganizationLevel OrganizationLevel { get; init; }
    public Guid? OrganizationEntityId { get; init; }
    public string? OrganizationEntityName { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public bool IsActive { get; init; }
    public string? Responsibilities { get; init; }
    public DateTime CreatedOn { get; init; }
    public Guid? CreatedBy { get; init; }
}

public record CreateMemberPositionRequest
{
    public Guid MemberId { get; init; }
    public string PositionTitle { get; init; } = default!;
    public OrganizationLevel OrganizationLevel { get; init; }
    public Guid? OrganizationEntityId { get; init; }
    public DateTime StartDate { get; init; }
    public string? Responsibilities { get; init; }
}

public record UpdateMemberPositionRequest
{
    public Guid Id { get; init; }
    public string PositionTitle { get; init; } = default!;
    public string? Responsibilities { get; init; }
}

public record EndMemberPositionRequest
{
    public Guid Id { get; init; }
    public DateTime EndDate { get; init; }
}
