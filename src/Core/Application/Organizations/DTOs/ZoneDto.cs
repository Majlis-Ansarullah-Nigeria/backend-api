namespace ManagementApi.Application.Organizations.DTOs;

public record ZoneDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string? Code { get; init; }
    public string? Address { get; init; }
    public string? ContactPerson { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Email { get; init; }
    public int DilaCount { get; init; }
    public int TotalMuqams { get; init; }
    public int TotalMembers { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record CreateZoneRequest
{
    public string Name { get; init; } = default!;
    public string? Code { get; init; }
    public string? Address { get; init; }
    public string? ContactPerson { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Email { get; init; }
}

public record UpdateZoneRequest
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string? Code { get; init; }
    public string? Address { get; init; }
    public string? ContactPerson { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Email { get; init; }
}
