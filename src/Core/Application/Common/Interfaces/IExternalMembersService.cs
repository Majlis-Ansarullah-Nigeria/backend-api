using ManagementApi.Application.Members.DTOs;

namespace ManagementApi.Application.Common.Interfaces;

/// <summary>
/// Interface for fetching members from external gateway/API
/// </summary>
public interface IExternalMembersService
{
    /// <summary>
    /// Fetches all members from the external gateway
    /// </summary>
    Task<List<ExternalMemberDto>> FetchMembersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a single member by ChandaNo from the external gateway
    /// </summary>
    Task<ExternalMemberDto?> FetchMemberByChandaNoAsync(string chandaNo, CancellationToken cancellationToken = default);
}

/// <summary>
/// DTO for external member data
/// </summary>
public record ExternalMemberDto
{
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
    public string? PhotoUrl { get; init; }
    public string? Signature { get; init; }
    public string? BloodGroup { get; init; }
    public string? Genotype { get; init; }
    public int? JamaatId { get; init; }
}
