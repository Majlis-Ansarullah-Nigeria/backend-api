using ManagementApi.Domain.Common;
using ManagementApi.Domain.Enums;

namespace ManagementApi.Domain.Entities;

public class Member : AuditableEntity, IAggregateRoot
{
    public string ChandaNo { get; private set; } = default!;
    public string? WasiyatNo { get; private set; }
    public string? Title { get; private set; }
    public string Surname { get; private set; } = default!;
    public string? FirstName { get; private set; }
    public string? MiddleName { get; private set; }
    public DateTime? DateOfBirth { get; private set; }
    public string? Email { get; private set; }
    public string? PhoneNo { get; private set; }
    public string? MaritalStatus { get; private set; }
    public string? Address { get; private set; }
    public string? NextOfKinPhoneNo { get; private set; }
    public string? NextOfKinName { get; private set; }
    public bool? RecordStatus { get; private set; }
    public string? MemberShipStatus { get; private set; }
    public string? PhotoUrl { get; private set; }
    public string? Signature { get; private set; }
    public BloodGroup? BloodGroup { get; private set; }
    public Genotype? Genotype { get; private set; }

    public int? JamaatId { get; private set; } // External Jamaat ID
    public Guid? MuqamId { get; private set; }

    // Navigation properties
    public Muqam? Muqam { get; private set; }

    private Member() { } // EF Core

    public Member(string chandaNo, string surname, string? firstName)
    {
        ChandaNo = chandaNo;
        Surname = surname;
        FirstName = firstName;
    }

    public void UpdateBasicInfo(
        string surname,
        string? firstName,
        string? middleName,
        DateTime? dateOfBirth,
        string? email,
        string? phoneNo,
        string? maritalStatus,
        string? address)
    {
        Surname = surname;
        FirstName = firstName;
        MiddleName = middleName;
        DateOfBirth = dateOfBirth;
        Email = email;
        PhoneNo = phoneNo;
        MaritalStatus = maritalStatus;
        Address = address;
    }

    public void UpdateMedicalInfo(BloodGroup? bloodGroup, Genotype? genotype)
    {
        BloodGroup = bloodGroup;
        Genotype = genotype;
    }

    public void UpdatePhotoUrl(string photoUrl)
    {
        PhotoUrl = photoUrl;
    }

    public void UpdateSignature(string signature)
    {
        Signature = signature;
    }

    public void UpdateJamaatId(int jamaatId)
    {
        JamaatId = jamaatId;
    }

    public void AssignToMuqam(Guid muqamId)
    {
        MuqamId = muqamId;
    }
}
