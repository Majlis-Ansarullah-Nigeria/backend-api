using ManagementApi.Domain.Enums;

namespace ManagementApi.Application.Members.DTOs;

public record MemberDto
{
    public Guid Id { get; init; }
    public string ChandaNo { get; init; } = default!;
    public string? WasiyatNo { get; init; }
    public string? Title { get; init; }
    public string Surname { get; init; } = default!;
    public string? FirstName { get; init; }
    public string? MiddleName { get; init; }
    public string FullName => $"{FirstName} {MiddleName} {Surname}".Trim();
    public DateTime? DateOfBirth { get; init; }
    public int? Age => DateOfBirth.HasValue ? DateTime.Now.Year - DateOfBirth.Value.Year : null;
    public string? Email { get; init; }
    public string? PhoneNo { get; init; }
    public string? MaritalStatus { get; init; }
    public string? Address { get; init; }
    public string? NextOfKinPhoneNo { get; init; }
    public string? NextOfKinName { get; init; }
    public bool? RecordStatus { get; init; }
    public string? MemberShipStatus { get; init; }
    public string? PhotoUrl { get; init; }
    public string? Signature { get; init; }
    public BloodGroup? BloodGroup { get; init; }
    public Genotype? Genotype { get; init; }
    public int? JamaatId { get; init; }
    public string? JamaatName { get; init; }
    public Guid? MuqamId { get; init; }
    public string? MuqamName { get; init; }
    public string? DilaName { get; init; }
    public string? ZoneName { get; init; }
    public List<MemberPositionDto>? Positions { get; init; }
}

public record SearchMembersRequest
{
    public string? SearchTerm { get; init; }
    public Guid? MuqamId { get; init; }
    public Guid? DilaId { get; init; }
    public Guid? ZoneId { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

public record UpdateMemberPhotoRequest
{
    public string ChandaNo { get; init; } = default!;
    public string PhotoUrl { get; init; } = default!;
}

public record UpdateMemberSignatureRequest
{
    public string ChandaNo { get; init; } = default!;
    public string SignatureUrl { get; init; } = default!;
}

public record UpdateMemberRequest
{
    public Guid Id { get; init; }
    public string Surname { get; init; } = default!;
    public string? FirstName { get; init; }
    public string? MiddleName { get; init; }
    public DateTime? DateOfBirth { get; init; }
    public string? Email { get; init; }
    public string? PhoneNo { get; init; }
    public string? MaritalStatus { get; init; }
    public string? Address { get; init; }
    public string? NextOfKinPhoneNo { get; init; }
    public string? NextOfKinName { get; init; }
    public BloodGroup? BloodGroup { get; init; }
    public Genotype? Genotype { get; init; }
    public int? JamaatId { get; init; }
}

public record MemberProfileExportDto
{
    public Guid Id { get; init; }
    public string ChandaNo { get; init; } = default!;
    public string? WasiyatNo { get; init; }
    public string? Title { get; init; }
    public string Surname { get; init; } = default!;
    public string? FirstName { get; init; }
    public string? MiddleName { get; init; }
    public DateTime? DateOfBirth { get; init; }
    public string? Email { get; init; }
    public string? PhoneNo { get; init; }
    public string? MaritalStatus { get; init; }
    public string? Address { get; init; }
    public string? NextOfKinPhoneNo { get; init; }
    public string? NextOfKinName { get; init; }
    public bool? RecordStatus { get; init; }
    public string? MemberShipStatus { get; init; }
    public string? PhotoUrl { get; init; }
    public string? Signature { get; init; }
    public BloodGroup? BloodGroup { get; init; }
    public Genotype? Genotype { get; init; }
    public string? JamaatName { get; init; }
    public string? MuqamName { get; init; }
    public string? DilaName { get; init; }
    public string? ZoneName { get; init; }
    public List<MemberPositionDto> Positions { get; init; } = new();
    public DateTime ExportedAt { get; init; }
}

public record MemberStatisticsDto
{
    public int TotalMembers { get; init; }
    public int ActiveMembers { get; init; }
    public int InactiveMembers { get; init; }
    public int MembersWithPositions { get; init; }
    public List<BloodGroupStatDto> BloodGroupDistribution { get; init; } = new();
    public List<GenotypeStatDto> GenotypeDistribution { get; init; } = new();
    public List<OrganizationStatDto> MembersByZone { get; init; } = new();
    public List<MaritalStatusStatDto> MaritalStatusDistribution { get; init; } = new();
    public List<AgeDistributionDto> AgeDistribution { get; init; } = new();
    public DateTime GeneratedAt { get; init; }
}

public record BloodGroupStatDto
{
    public string BloodGroup { get; init; } = default!;
    public int Count { get; init; }
}

public record GenotypeStatDto
{
    public string Genotype { get; init; } = default!;
    public int Count { get; init; }
}

public record OrganizationStatDto
{
    public string OrganizationId { get; init; } = default!;
    public string OrganizationName { get; init; } = default!;
    public int MemberCount { get; init; }
}

public record MaritalStatusStatDto
{
    public string MaritalStatus { get; init; } = default!;
    public int Count { get; init; }
}

public record AgeDistributionDto
{
    public string AgeRange { get; init; } = default!;
    public int Count { get; init; }
}
