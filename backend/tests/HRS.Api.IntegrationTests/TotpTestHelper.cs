using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;

namespace HRS.Api.IntegrationTests;

/// <summary>
/// A minimal RFC 6238 TOTP generator so tests can compute a valid code from the shared key
/// returned by /me/mfa/totp/setup, the same way an authenticator app would. ASP.NET Core
/// Identity's built-in provider (HMAC-SHA1, 30s step, 6 digits) follows the same RFC.
/// </summary>
internal static class TotpTestHelper
{
    [SuppressMessage(
        "Security",
        "CA5350:Do Not Use Weak Cryptographic Algorithms",
        Justification = "RFC 6238 TOTP mandates HMAC-SHA1 for compatibility with authenticator apps — " +
            "this is the same algorithm ASP.NET Core Identity's own authenticator provider uses.")]
    public static string GenerateCode(string formattedOrRawBase32Secret, DateTimeOffset? at = null)
    {
        var secretBytes = Base32Decode(formattedOrRawBase32Secret);
        var counter = (at ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() / 30;

        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counterBytes);
        }

        var hash = new HMACSHA1(secretBytes).ComputeHash(counterBytes);
        var offset = hash[^1] & 0x0F;

        var binaryCode = ((hash[offset] & 0x7F) << 24)
            | ((hash[offset + 1] & 0xFF) << 16)
            | ((hash[offset + 2] & 0xFF) << 8)
            | (hash[offset + 3] & 0xFF);

        return (binaryCode % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        var cleaned = input.Replace(" ", string.Empty, StringComparison.Ordinal).TrimEnd('=').ToUpperInvariant();
        var bits = 0;
        var value = 0;
        var output = new List<byte>();

        foreach (var c in cleaned)
        {
            var index = alphabet.IndexOf(c, StringComparison.Ordinal);

            if (index < 0)
            {
                continue;
            }

            value = (value << 5) | index;
            bits += 5;

            if (bits >= 8)
            {
                output.Add((byte)((value >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }

        return [.. output];
    }
}
