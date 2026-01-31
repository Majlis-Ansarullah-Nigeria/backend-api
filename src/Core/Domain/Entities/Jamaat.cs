using ManagementApi.Domain.Common;
using ManagementApi.Domain.Events;

namespace ManagementApi.Domain.Entities;

public class Jamaat : AuditableEntity, IAggregateRoot
{
    public int JamaatId { get; private set; } // External ID from API
    public string Name { get; private set; } = default!;
    public string? Code { get; private set; }
    public string? CircuitName { get; private set; }
    public Guid? MuqamId { get; private set; } // null = unmapped

    // Navigation properties
    public Muqam? Muqam { get; private set; }

    private Jamaat() { } // EF Core

    public Jamaat(int jamaatId, string name, string? code, string? circuitName = null)
    {
        JamaatId = jamaatId;
        Name = name;
        Code = code;
        CircuitName = circuitName;
    }

    public void MapToMuqam(Guid muqamId)
    {
        if (MuqamId == muqamId) return; // Already mapped

        var previousMuqamId = MuqamId;
        MuqamId = muqamId;

        if (previousMuqamId.HasValue)
        {
            AddDomainEvent(new JamaatUnmappedEvent(Id, previousMuqamId.Value));
        }

        AddDomainEvent(new JamaatMappedEvent(Id, muqamId));
    }

    public void Unmap()
    {
        if (!MuqamId.HasValue) return;

        var previousMuqamId = MuqamId.Value;
        MuqamId = null;

        AddDomainEvent(new JamaatUnmappedEvent(Id, previousMuqamId));
    }

    public void UpdateInfo(string name, string? code, string? circuitName = null)
    {
        Name = name;
        Code = code;
        CircuitName = circuitName;
    }
}
