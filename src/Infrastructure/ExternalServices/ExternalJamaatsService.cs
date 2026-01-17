using ManagementApi.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ManagementApi.Infrastructure.ExternalServices;

/// <summary>
/// Implementation of external jamaats API service
/// Fetches jamaats from https://tajneedapi.ahmadiyyanigeria.net/jamaats
/// </summary>
public class ExternalJamaatsService : IExternalJamaatsService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExternalJamaatsService> _logger;
    private readonly string? _baseUrl;

    public ExternalJamaatsService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ExternalJamaatsService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _baseUrl = _configuration["ExternalServices:JamaatsApi:BaseUrl"] ?? "https://tajneedapi.ahmadiyyanigeria.net";
    }

    public async Task<List<ExternalJamaatDto>> FetchJamaatsAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_baseUrl))
        {
            _logger.LogWarning("External jamaats API URL is not configured. Returning empty list.");
            return new List<ExternalJamaatDto>();
        }

        try
        {
            var client = _httpClientFactory.CreateClient("JamaatsApi");

            // The API endpoint is /jamaats directly (not /api/jamaats)
            var response = await client.GetAsync($"{_baseUrl}/jamaats", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var jamaats = JsonSerializer.Deserialize<List<ExternalJamaatDto>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                _logger.LogInformation("Successfully fetched {Count} jamaats from external API", jamaats?.Count ?? 0);
                return jamaats ?? new List<ExternalJamaatDto>();
            }

            _logger.LogWarning("Failed to fetch jamaats from API. Status code: {StatusCode}, Reason: {Reason}",
                response.StatusCode, response.ReasonPhrase);
            return new List<ExternalJamaatDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching jamaats from external API at {BaseUrl}/jamaats", _baseUrl);
            return new List<ExternalJamaatDto>();
        }
    }

    public async Task<ExternalJamaatDto?> FetchJamaatByIdAsync(int jamaatId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_baseUrl))
        {
            _logger.LogWarning("External jamaats API URL is not configured.");
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("JamaatsApi");

            // Fetch all jamaats and filter by ID (API may not support single jamaat endpoint)
            var response = await client.GetAsync($"{_baseUrl}/jamaats", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var jamaats = JsonSerializer.Deserialize<List<ExternalJamaatDto>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var jamaat = jamaats?.FirstOrDefault(j => j.JamaatId == jamaatId);

                if (jamaat != null)
                {
                    _logger.LogInformation("Successfully fetched jamaat {JamaatId} from external API", jamaatId);
                }
                else
                {
                    _logger.LogWarning("Jamaat {JamaatId} not found in external API", jamaatId);
                }

                return jamaat;
            }

            _logger.LogWarning("Failed to fetch jamaat {JamaatId} from API. Status code: {StatusCode}", jamaatId, response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching jamaat {JamaatId} from external API", jamaatId);
            return null;
        }
    }
}
