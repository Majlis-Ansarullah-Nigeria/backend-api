using System.Security.Claims;
using System.Text;
using HRS.Application.Common.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace HRS.Infrastructure.Auth;

public sealed class JwtAccessTokenGenerator(IOptions<AuthOptions> options, TimeProvider timeProvider) : IAccessTokenGenerator
{
    private readonly AuthOptions _options = options.Value;

    public AccessToken Generate(Guid userId, string email, string name, Guid sessionId)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.Add(_options.AccessTokenLifetime);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Name, name),
                new Claim(JwtRegisteredClaimNames.Sid, sessionId.ToString()),
            ]),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = credentials,
        };

        var handler = new JsonWebTokenHandler();
        var token = handler.CreateToken(descriptor);

        return new AccessToken(token, expiresAt);
    }
}
