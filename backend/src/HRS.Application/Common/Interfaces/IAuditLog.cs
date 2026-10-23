namespace HRS.Application.Common.Interfaces;

/// <summary>What happened, for FR-AUD-001's audit log. Before/After are serialized to JSON as
/// they are; pass only the fields that matter, never secrets or confidential answers.</summary>
public sealed record AuditEntry(
    string Action,
    Guid? ActorUserId,
    string EntityType,
    string? EntityId,
    string Summary,
    Guid? UnitId = null,
    object? Before = null,
    object? After = null,
    string? ActorEmail = null);

/// <summary>
/// Writes FR-AUD-001's append-only audit log. Add queues the entry in the caller's own unit of
/// work, so it commits — or rolls back — with the change it describes. RecordAsync writes it
/// straight away, for actions with no unit of work of their own (sign-ins, downloads).
/// </summary>
public interface IAuditLog
{
    void Add(AuditEntry entry);

    Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken);
}
