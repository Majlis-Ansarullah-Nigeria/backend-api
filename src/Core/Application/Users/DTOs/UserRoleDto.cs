namespace ManagementApi.Application.Users.DTOs;

public record AssignRolesRequest
{
    public Guid UserId { get; init; }
    public List<string> Roles { get; init; } = new();
}

public record UserWithRolesDto
{
    public Guid Id { get; init; }
    public string? ChandaNo { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Email { get; init; }
    public bool IsActive { get; init; }
    public List<string> Roles { get; init; } = new();
    public List<string> Permissions { get; init; } = new();
}
