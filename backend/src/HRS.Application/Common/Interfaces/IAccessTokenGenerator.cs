namespace HRS.Application.Common.Interfaces;

public interface IAccessTokenGenerator
{
    AccessToken Generate(Guid userId, string email, string name);
}

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);
