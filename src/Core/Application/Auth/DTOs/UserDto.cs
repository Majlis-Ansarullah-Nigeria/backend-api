using ManagementApi.Domain.Enums;

namespace ManagementApi.Application.Auth.DTOs;

public record UserDto
{
    public Guid Id { get; init; }
    public string? ChandaNo { get; init; }
    public Guid? MemberId { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Email { get; init; }
    public string? ImageUrl { get; init; }
    public bool IsActive { get; init; }

    // Organization context
    public Guid? MuqamId { get; init; }
    public Guid? DilaId { get; init; }
    public Guid? ZoneId { get; init; }
    public OrganizationLevel? OrganizationLevel { get; init; }

    // Roles and Permissions
    public List<string> Roles { get; init; } = new();
    public List<string> Permissions { get; init; } = new();
}
