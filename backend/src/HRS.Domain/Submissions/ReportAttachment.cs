namespace HRS.Domain.Submissions;

/// <summary>
/// One uploaded file attached to a FileUpload-type field on a report (FR-SUB-005). Created
/// Pending at presign time (before the client has even uploaded the bytes) so the per-report
/// 100 MB budget counts in-flight uploads too, not just scanned-clean ones — otherwise two
/// concurrent presigns could both pass the budget check and together blow past it. A file that
/// fails its virus scan is removed outright (row and storage object) rather than kept in a
/// rejected state — there's nothing useful to retain about it.
/// </summary>
public sealed class ReportAttachment
{
    private ReportAttachment()
    {
    }

    private ReportAttachment(
        Guid id, Guid reportInstanceId, string fieldKey, string fileName, string contentType, long sizeBytes,
        string storageKey, Guid uploadedByUserId, DateTimeOffset now)
    {
        Id = id;
        ReportInstanceId = reportInstanceId;
        FieldKey = fieldKey;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        StorageKey = storageKey;
        Status = ReportAttachmentStatus.Pending;
        UploadedByUserId = uploadedByUserId;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid ReportInstanceId { get; private set; }

    public string FieldKey { get; private set; } = string.Empty;

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public string StorageKey { get; private set; } = string.Empty;

    public ReportAttachmentStatus Status { get; private set; }

    public Guid UploadedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static ReportAttachment Create(
        Guid id, Guid reportInstanceId, string fieldKey, string fileName, string contentType, long sizeBytes,
        string storageKey, Guid uploadedByUserId, DateTimeOffset now) =>
        new(id, reportInstanceId, fieldKey, fileName, contentType, sizeBytes, storageKey, uploadedByUserId, now);

    /// <summary>The scan passed — accepted. sizeBytes is re-passed here because the handler
    /// re-measures the real uploaded object rather than trusting the client's presign-time
    /// declaration (defense against a client that declared a smaller size than it actually
    /// uploaded).</summary>
    public void MarkClean(long sizeBytes)
    {
        if (Status != ReportAttachmentStatus.Pending)
        {
            throw new InvalidOperationException($"Can't mark a {Status} attachment clean.");
        }

        Status = ReportAttachmentStatus.Clean;
        SizeBytes = sizeBytes;
    }
}
