using ManagementApi.Domain.Common;

namespace ManagementApi.Domain.Events;

public class JamaatUnmappedEvent : DomainEvent
{
    public Guid JamaatId { get; }
    public Guid PreviousMuqamId { get; }

    public JamaatUnmappedEvent(Guid jamaatId, Guid previousMuqamId)
    {
        JamaatId = jamaatId;
        PreviousMuqamId = previousMuqamId;
    }
}
