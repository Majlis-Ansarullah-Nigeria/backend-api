using ManagementApi.Domain.Common;

namespace ManagementApi.Domain.Entities;

public class Zone : AuditableEntity, IAggregateRoot
{
    public string Name { get; private set; } = default!;
    public string? Code { get; private set; }
    public string? Address { get; private set; }
    public string? ContactPerson { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? Email { get; private set; }

    // Navigation properties
    public ICollection<Dila> Dilas { get; private set; } = new List<Dila>();

    private Zone() { } // EF Core

    public Zone(string name, string? code)
    {
        Name = name;
        Code = code;
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
}
