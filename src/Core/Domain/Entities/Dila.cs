using ManagementApi.Domain.Common;

namespace ManagementApi.Domain.Entities;

public class Dila : AuditableEntity, IAggregateRoot
{
    public string Name { get; private set; } = default!;
    public string? Code { get; private set; }
    public string? Address { get; private set; }
    public string? ContactPerson { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? Email { get; private set; }
    public Guid? ZoneId { get; private set; }

    // Navigation properties
    public Zone? Zone { get; private set; }
    public ICollection<Muqam> Muqams { get; private set; } = new List<Muqam>();

    private Dila() { } // EF Core

    public Dila(string name, string? code, Guid? zoneId)
    {
        Name = name;
        Code = code;
        ZoneId = zoneId;
    }

    public void Update(string name, string? code, string? address, string? contactPerson, string? phoneNumber, string? email)
    {
        Name = name;
        Code = code;
        Address = address;
        ContactPerson = contactPerson;
        PhoneNumber = phoneNumber;
        Email = email;
    }

    public void AssignToZone(Guid zoneId)
    {
        ZoneId = zoneId;
    }
}
