using HRS.Application.Common.Interfaces;

namespace HRS.Application.UnitTests.Common;

/// <summary>Collects audit entries so handler tests can check what was recorded (FR-AUD-001).</summary>
public sealed class RecordingAuditLog : IAuditLog
{
    public List<AuditEntry> Entries { get; } = [];

    public void Add(AuditEntry entry) => Entries.Add(entry);

    public Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }
}
