namespace ManagementApi.Application.Jamaats.DTOs;

public record JamaatDto
{
    public Guid Id { get; init; }
    public int JamaatId { get; init; }
    public string Name { get; init; } = default!;
    public string? Code { get; init; }
    public Guid? MuqamId { get; init; }
    public string? MuqamName { get; init; }
    public bool IsMapped => MuqamId.HasValue;
    public DateTime CreatedOn { get; init; }
}

public record MapJamaatToMuqamRequest
{
    public Guid JamaatId { get; init; }
    public Guid MuqamId { get; init; }
}

public record UnmapJamaatRequest
{
    public Guid JamaatId { get; init; }
}

public record JamaatMappingStatsDto
{
    public int TotalJamaats { get; init; }
    public int MappedJamaats { get; init; }
    public int UnmappedJamaats { get; init; }
    public double MappingPercentage { get; init; }
}
