namespace HRS.Application.Common.Interfaces;

/// <summary>Where the current request came from, for the audit log (FR-AUD-001). Both are null
/// outside an HTTP request (background jobs).</summary>
public interface IRequestContext
{
    string? IpAddress { get; }

    string? UserAgent { get; }
}
