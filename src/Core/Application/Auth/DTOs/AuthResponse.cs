namespace ManagementApi.Application.Auth.DTOs;

public record AuthResponse
{
    public string Token { get; init; } = default!;
    public UserDto User { get; init; } = default!;
}
