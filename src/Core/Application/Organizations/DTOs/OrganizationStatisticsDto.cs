namespace ManagementApi.Application.Organizations.DTOs;

public record OrganizationStatisticsDto
{
    public int TotalZones { get; init; }
    public int TotalDilas { get; init; }
    public int TotalMuqams { get; init; }
    public int TotalMembers { get; init; }
    public int TotalJamaats { get; init; }
    public int UnassignedMuqams { get; init; }
    public int UnassignedDilas { get; init; }
    public ZoneStatsDto? LargestZone { get; init; }
    public DilaStatsDto? LargestDila { get; init; }
    public MuqamStatsDto? LargestMuqam { get; init; }
}

public record ZoneStatsDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public int DilaCount { get; init; }
    public int MemberCount { get; init; }
}

public record DilaStatsDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string? ZoneName { get; init; }
    public int MuqamCount { get; init; }
    public int MemberCount { get; init; }
}

public record MuqamStatsDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string? DilaName { get; init; }
    public int MemberCount { get; init; }
    public int JamaatCount { get; init; }
}
