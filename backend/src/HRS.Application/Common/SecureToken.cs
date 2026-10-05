using System.Security.Cryptography;
using System.Text;

namespace HRS.Application.Common;

/// <summary>
/// The "random bytes to the client, SHA-256 hash at rest" pattern used by every bearer
/// secret in this codebase (refresh tokens, OTP codes, invite tokens). Hex-encoded rather
/// than Base64 like the refresh token: this one rides in a URL path segment
/// (/invite/{token}), where Base64's '+', '/' and '=' would break route matching or need
/// escaping — hex has neither problem.
/// </summary>
internal static class SecureToken
{
    public static string Generate() => Convert.ToHexString(RandomNumberGenerator.GetBytes(48));

    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
