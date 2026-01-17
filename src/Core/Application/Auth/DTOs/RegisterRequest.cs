namespace ManagementApi.Application.Auth.DTOs;

public record RegisterRequest
{
    public string ChandaNo { get; init; } = default!;
    public string Password { get; init; } = default!;
    public string ConfirmPassword { get; init; } = default!;
}
