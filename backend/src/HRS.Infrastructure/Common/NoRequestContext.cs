using HRS.Application.Common.Interfaces;

namespace HRS.Infrastructure.Common;

/// <summary>Background jobs have no client — audit entries they write carry no IP or user agent.
/// The API replaces this with an HTTP-backed context.</summary>
public sealed class NoRequestContext : IRequestContext
{
    public string? IpAddress => null;

    public string? UserAgent => null;
}
