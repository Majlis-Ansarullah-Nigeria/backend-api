namespace ManagementApi.Application.Auth.DTOs;

public record LoginRequest
{
    public string ChandaNo { get; init; } = default!;
    public string Password { get; init; } = default!;
}
