namespace HRS.Application.Common.Interfaces;

public interface IAccessTokenGenerator
{
    AccessToken Generate(Guid userId, string email, string name, Guid sessionId);
}

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);
