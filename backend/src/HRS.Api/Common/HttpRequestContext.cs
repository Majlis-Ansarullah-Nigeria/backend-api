using HRS.Application.Common.Interfaces;

namespace HRS.Api.Common;

/// <summary>The current HTTP request's client, for the audit log (FR-AUD-001). RemoteIpAddress
/// is the real client once UseForwardedHeaders has read the proxy's X-Forwarded-For.</summary>
public sealed class HttpRequestContext(IHttpContextAccessor accessor) : IRequestContext
{
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => accessor.HttpContext?.Request.Headers.UserAgent.ToString() is { Length: > 0 } agent ? agent : null;
}
