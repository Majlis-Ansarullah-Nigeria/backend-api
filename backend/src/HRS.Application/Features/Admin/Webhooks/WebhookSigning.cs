using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace HRS.Application.Features.Admin.Webhooks;

/// <summary>
/// FR-ADM-003's signature, sent as <c>X-HRS-Signature: t={unix seconds},v1={hex}</c> where hex is
/// HMAC-SHA256 over <c>"{t}.{body}"</c> with the endpoint's secret. Receivers recompute it and
/// reject old timestamps to stop replays.
/// </summary>
public static class WebhookSigning
{
    public static string NewSecret() => "whsec_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Sign(string secret, long timestamp, string body)
    {
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp.ToString(CultureInfo.InvariantCulture)}.{body}"));
        return $"t={timestamp.ToString(CultureInfo.InvariantCulture)},v1={Convert.ToHexStringLower(mac)}";
    }

    /// <summary>"…a1b2" — enough to recognise a secret without revealing it.</summary>
    public static string Hint(string secret) => secret.Length <= 4 ? "…" : "…" + secret[^4..];
}
