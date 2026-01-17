namespace ManagementApi.Application.Organizations.DTOs;

public record MuqamDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string? Code { get; init; }
    public string? Address { get; init; }
    public string? ContactPerson { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Email { get; init; }
    public Guid? DilaId { get; init; }
    public string? DilaName { get; init; }
    public int MemberCount { get; init; }
    public int JamaatCount { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record CreateMuqamRequest
{
    public string Name { get; init; } = default!;
    public string? Code { get; init; }
    public string? Address { get; init; }
    public string? ContactPerson { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Email { get; init; }
    public Guid? DilaId { get; init; }
}

public record UpdateMuqamRequest
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string? Code { get; init; }
    public string? Address { get; init; }
    public string? ContactPerson { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Email { get; init; }
    public Guid? DilaId { get; init; }
}
