namespace ManagementApi.Application.Organizations.DTOs;

public record DilaDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string? Code { get; init; }
    public string? Address { get; init; }
    public string? ContactPerson { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Email { get; init; }
    public Guid? ZoneId { get; init; }
    public string? ZoneName { get; init; }
    public int MuqamCount { get; init; }
    public int TotalMembers { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record CreateDilaRequest
{
    public string Name { get; init; } = default!;
    public string? Code { get; init; }
    public string? Address { get; init; }
    public string? ContactPerson { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Email { get; init; }
    public Guid? ZoneId { get; init; }
}

public record UpdateDilaRequest
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string? Code { get; init; }
    public string? Address { get; init; }
    public string? ContactPerson { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Email { get; init; }
    public Guid? ZoneId { get; init; }
}
