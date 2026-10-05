namespace HRS.Infrastructure.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public required string SigningKey { get; set; }

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(30);
}
