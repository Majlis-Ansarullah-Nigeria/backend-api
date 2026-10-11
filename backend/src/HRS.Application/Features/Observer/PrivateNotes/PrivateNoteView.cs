namespace HRS.Application.Features.Observer.PrivateNotes;

/// <summary>FR-OBS-004: one entry in the private-notes log, resolved for display. AuthorName is
/// looked up once per query rather than stored, so a note always shows the author's current
/// display name (consistent with every other "resolve the name at read time" pattern in this
/// codebase, e.g. GetComplianceGridQuery's head names).</summary>
public sealed record PrivateNoteView(Guid Id, string AuthorName, string Body, DateTimeOffset CreatedAt);
