namespace ManagementApi.Application.Roles.DTOs;

public record RoleDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public List<string> Permissions { get; init; } = new();
}

public record RoleWithPermissionsDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public List<string> Permissions { get; init; } = new();
    public int UserCount { get; init; }
}
