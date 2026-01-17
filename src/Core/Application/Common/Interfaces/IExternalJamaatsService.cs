using ManagementApi.Application.Jamaats.DTOs;

namespace ManagementApi.Application.Common.Interfaces;

/// <summary>
/// Interface for fetching jamaats from external API
/// </summary>
public interface IExternalJamaatsService
{
    /// <summary>
    /// Fetches all jamaats from the external API
    /// </summary>
    Task<List<ExternalJamaatDto>> FetchJamaatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a single jamaat by ID from the external API
    /// </summary>
    Task<ExternalJamaatDto?> FetchJamaatByIdAsync(int jamaatId, CancellationToken cancellationToken = default);
}

/// <summary>
/// DTO for external jamaat data from tajneedapi.ahmadiyyanigeria.net
/// </summary>
public record ExternalJamaatDto
{
    public int JamaatId { get; init; }
    public string JamaatName { get; init; } = default!;
    public string? JamaatCode { get; init; }
    public int? CircuitId { get; init; }
    public string? CircuitCode { get; init; }
    public string? CircuitName { get; init; }

    // Computed properties for compatibility with domain model
    public string Name => JamaatName;
    public string? Description => CircuitName != null ? $"Circuit: {CircuitName}" : null;
    public string? Location => CircuitName;
    public bool IsActive => true; // Assume all jamaats from API are active
}
