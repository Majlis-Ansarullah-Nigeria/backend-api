using System.Net;
using System.Net.Sockets;

namespace HRS.Application.Features.Audit;

/// <summary>How the audit viewer shows an IP: the network part only, like the mockup's
/// "102.89.x.x". The full address stays in the database and the CSV export.</summary>
public static class IpMask
{
    public static string? Mask(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress) || !IPAddress.TryParse(ipAddress, out var parsed))
        {
            return string.IsNullOrWhiteSpace(ipAddress) ? null : "x.x.x.x";
        }

        if (parsed.IsIPv4MappedToIPv6)
        {
            parsed = parsed.MapToIPv4();
        }

        if (parsed.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = parsed.GetAddressBytes();
            return $"{bytes[0]}.{bytes[1]}.x.x";
        }

        var groups = parsed.ToString().Split(':', StringSplitOptions.RemoveEmptyEntries);
        return groups.Length >= 2 ? $"{groups[0]}:{groups[1]}:x:x" : "x:x";
    }
}
