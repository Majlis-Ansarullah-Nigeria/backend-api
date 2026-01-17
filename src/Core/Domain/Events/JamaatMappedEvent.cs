using ManagementApi.Domain.Common;

namespace ManagementApi.Domain.Events;

public class JamaatMappedEvent : DomainEvent
{
    public Guid JamaatId { get; }
    public Guid MuqamId { get; }

    public JamaatMappedEvent(Guid jamaatId, Guid muqamId)
    {
        JamaatId = jamaatId;
        MuqamId = muqamId;
    }
}
