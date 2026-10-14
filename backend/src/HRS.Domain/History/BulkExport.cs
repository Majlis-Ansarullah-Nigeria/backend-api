namespace HRS.Domain.History;

/// <summary>FR-HIS-004's background bulk export job record — the "Recent exports" row and the
/// Hangfire job's own state machine in one. ScopeJson is an opaque blob (same reasoning as
/// OutboxMessage.PayloadJson) whose shape depends on ScopeKind; only the Application-layer job
/// handler ever deserializes it.</summary>
public sealed class BulkExport
{
    private BulkExport()
    {
    }

    private BulkExport(
        Guid id, Guid requestedByUserId, BulkExportFormat format, BulkExportScopeKind scopeKind, string scopeJson, DateTimeOffset now)
    {
        Id = id;
        RequestedByUserId = requestedByUserId;
        Format = format;
        ScopeKind = scopeKind;
        ScopeJson = scopeJson;
        Status = BulkExportStatus.Pending;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public BulkExportFormat Format { get; private set; }

    public BulkExportScopeKind ScopeKind { get; private set; }

    public string ScopeJson { get; private set; } = "{}";

    public BulkExportStatus Status { get; private set; }

    public string? StorageKey { get; private set; }

    public int ReportCount { get; private set; }

    public string? ErrorMessage { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public static BulkExport Create(
        Guid id, Guid requestedByUserId, BulkExportFormat format, BulkExportScopeKind scopeKind, string scopeJson, DateTimeOffset now) =>
        new(id, requestedByUserId, format, scopeKind, scopeJson, now);

    public void MarkProcessing()
    {
        if (Status != BulkExportStatus.Pending)
        {
            throw new InvalidOperationException($"Can't start processing an export in {Status} status.");
        }

        Status = BulkExportStatus.Processing;
    }

    public void MarkReady(string storageKey, int reportCount, DateTimeOffset now)
    {
        Status = BulkExportStatus.Ready;
        StorageKey = storageKey;
        ReportCount = reportCount;
        CompletedAt = now;
    }

    public void MarkFailed(string errorMessage, DateTimeOffset now)
    {
        Status = BulkExportStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = now;
    }
}
