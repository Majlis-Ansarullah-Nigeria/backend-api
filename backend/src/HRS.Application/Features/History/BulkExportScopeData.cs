namespace HRS.Application.Features.History;

/// <summary>The two ScopeJson shapes — only this module ever deserializes them (same reasoning
/// as OutboxMessage.PayloadJson/Announcement's own opaque-blob fields).</summary>
public sealed record InstanceIdsScope(IReadOnlyList<Guid> InstanceIds);

public sealed record OrganizationFilterScope(Guid? ReportTypeId, Guid? FromWindowId, Guid? ToWindowId, int? Status);
