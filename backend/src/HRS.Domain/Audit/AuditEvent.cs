namespace HRS.Domain.Audit;

/// <summary>
/// One entry in FR-AUD-001's append-only audit log: who did what, when, from where, and the
/// before/after values where something changed. There are no mutators — once written, an entry
/// never changes, and the database refuses UPDATE and DELETE on the table as well. ActorUserId is
/// null for a failed sign-in with an unknown email (ActorEmail holds what was typed) and for
/// system actions. No foreign keys: the log must outlive the users and units it mentions.
/// </summary>
public sealed class AuditEvent
{
    private AuditEvent()
    {
    }

    private AuditEvent(
        Guid id,
        DateTimeOffset occurredAt,
        string action,
        Guid? actorUserId,
        string? actorEmail,
        string entityType,
        string? entityId,
        Guid? unitId,
        string summary,
        string? beforeJson,
        string? afterJson,
        string? ipAddress,
        string? userAgent)
    {
        Id = id;
        OccurredAt = occurredAt;
        Action = action;
        ActorUserId = actorUserId;
        ActorEmail = actorEmail;
        EntityType = entityType;
        EntityId = entityId;
        UnitId = unitId;
        Summary = summary;
        BeforeJson = beforeJson;
        AfterJson = afterJson;
        IpAddress = ipAddress;
        UserAgent = userAgent;
    }

    public Guid Id { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>A stable dotted name such as <c>report.submitted</c> — see AuditActions.</summary>
    public string Action { get; private set; } = string.Empty;

    public Guid? ActorUserId { get; private set; }

    public string? ActorEmail { get; private set; }

    public string EntityType { get; private set; } = string.Empty;

    public string? EntityId { get; private set; }

    /// <summary>The unit the action concerns, if any — what the viewer's Unit filter matches.</summary>
    public Guid? UnitId { get; private set; }

    /// <summary>A short human-readable description, written once at the time of the action.</summary>
    public string Summary { get; private set; } = string.Empty;

    public string? BeforeJson { get; private set; }

    public string? AfterJson { get; private set; }

    /// <summary>The full client IP. The viewer masks it; the CSV export keeps it for investigations.</summary>
    public string? IpAddress { get; private set; }

    public string? UserAgent { get; private set; }

    public static AuditEvent Create(
        Guid id,
        DateTimeOffset occurredAt,
        string action,
        Guid? actorUserId,
        string? actorEmail,
        string entityType,
        string? entityId,
        Guid? unitId,
        string summary,
        string? beforeJson,
        string? afterJson,
        string? ipAddress,
        string? userAgent) =>
        new(id, occurredAt, action, actorUserId, actorEmail, entityType, entityId, unitId, summary, beforeJson, afterJson, ipAddress, userAgent);
}
