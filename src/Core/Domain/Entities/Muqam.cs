using ManagementApi.Domain.Common;

namespace ManagementApi.Domain.Entities;

public class Muqam : AuditableEntity, IAggregateRoot
{
    public string Name { get; private set; } = default!;
    public string? Code { get; private set; }
    public string? Address { get; private set; }
    public string? ContactPerson { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? Email { get; private set; }
    public Guid? DilaId { get; private set; }

    // Navigation properties
    public Dila? Dila { get; private set; }
    public ICollection<Member> Members { get; private set; } = new List<Member>();
    public ICollection<Jamaat> Jamaats { get; private set; } = new List<Jamaat>();

    private Muqam() { } // EF Core

    public Muqam(string name, string? code, Guid? dilaId)
    {
        Name = name;
        Code = code;
        DilaId = dilaId;
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

    public void AssignToDila(Guid dilaId)
    {
        DilaId = dilaId;
    }
}
