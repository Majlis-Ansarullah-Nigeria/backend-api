using ManagementApi.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ManagementApi.Infrastructure.ExternalServices;

/// <summary>
/// Implementation of external members gateway service
/// Fetches members from external API (currently configured for tajneedapi.ahmadiyyanigeria.net)
/// Note: Adjust the endpoint path if the members API is available at a different location
/// </summary>
public class ExternalMembersService : IExternalMembersService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExternalMembersService> _logger;
    private readonly string? _baseUrl;

    public ExternalMembersService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ExternalMembersService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _baseUrl = _configuration["ExternalServices:MembersGateway:BaseUrl"] ?? "https://tajneedapi.ahmadiyyanigeria.net";
    }

    public async Task<List<ExternalMemberDto>> FetchMembersAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_baseUrl))
        {
            _logger.LogWarning("External members gateway URL is not configured. Returning empty list.");
            return new List<ExternalMemberDto>();
        }

        try
        {
            var client = _httpClientFactory.CreateClient("MembersGateway");

            // Fetch Ansarullah members from the external API
            var response = await client.GetAsync($"{_baseUrl}/members/auxilliarybody/ansarullah", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var members = JsonSerializer.Deserialize<List<ExternalMemberDto>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                _logger.LogInformation("Successfully fetched {Count} Ansarullah members from external gateway", members?.Count ?? 0);
                return members ?? new List<ExternalMemberDto>();
            }

            _logger.LogWarning("Failed to fetch Ansarullah members from gateway. Status code: {StatusCode}, Reason: {Reason}. " +
                              "Please verify the members endpoint is available at {BaseUrl}/members/auxilliarybody/ansarullah",
                response.StatusCode, response.ReasonPhrase, _baseUrl);
            return new List<ExternalMemberDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Ansarullah members from external gateway at {BaseUrl}/members/auxilliarybody/ansarullah. " +
                               "Please verify the endpoint exists and the API is accessible.", _baseUrl);
            return new List<ExternalMemberDto>();
        }
    }

    public async Task<ExternalMemberDto?> FetchMemberByChandaNoAsync(string chandaNo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_baseUrl))
        {
            _logger.LogWarning("External members gateway URL is not configured.");
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("MembersGateway");

            // Try fetching by ChandaNo (adjust based on actual API)
            var response = await client.GetAsync($"{_baseUrl}/members/{chandaNo}", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var member = JsonSerializer.Deserialize<ExternalMemberDto>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (member != null)
                {
                    _logger.LogInformation("Successfully fetched member {ChandaNo} from external gateway", chandaNo);
                }

                return member;
            }

            _logger.LogWarning("Failed to fetch member {ChandaNo} from gateway. Status code: {StatusCode}, Reason: {Reason}",
                chandaNo, response.StatusCode, response.ReasonPhrase);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching member {ChandaNo} from external gateway", chandaNo);
            return null;
        }
    }
}
