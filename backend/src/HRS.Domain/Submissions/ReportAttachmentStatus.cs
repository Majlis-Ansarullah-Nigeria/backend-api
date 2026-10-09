namespace HRS.Domain.Submissions;

/// <summary>Pending exists only between a presigned upload being issued and its "complete" call
/// resolving — a file that fails its scan is deleted outright rather than reaching a third
/// state (see ReportAttachment's own doc comment).</summary>
public enum ReportAttachmentStatus
{
    Pending,
    Clean,
}
