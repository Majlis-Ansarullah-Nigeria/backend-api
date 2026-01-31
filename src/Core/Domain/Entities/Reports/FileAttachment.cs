using ManagementApi.Domain.Common;

namespace ManagementApi.Domain.Entities.Reports;

/// <summary>
/// Represents a file attachment for a report submission
/// Files are stored as binary data in the database
/// </summary>
public class FileAttachment : AuditableEntity
{
    public Guid ReportSubmissionId { get; private set; }
    public Guid QuestionId { get; private set; }
    public string FileName { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public long FileSize { get; private set; } // Size in bytes
    public byte[] FileData { get; private set; } = default!; // Binary file content
    public string? Description { get; private set; }

    // Navigation property
    public ReportSubmission ReportSubmission { get; private set; } = default!;

    private FileAttachment() { } // EF Core

    public FileAttachment(
        Guid reportSubmissionId,
        Guid questionId,
        string fileName,
        string contentType,
        byte[] fileData,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be empty", nameof(fileName));

        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Content type cannot be empty", nameof(contentType));

        if (fileData == null || fileData.Length == 0)
            throw new ArgumentException("File data cannot be empty", nameof(fileData));

        // Validate file size (max 10MB)
        const long maxFileSize = 10 * 1024 * 1024; // 10MB
        if (fileData.Length > maxFileSize)
            throw new ArgumentException($"File size cannot exceed {maxFileSize / (1024 * 1024)}MB", nameof(fileData));

        ReportSubmissionId = reportSubmissionId;
        QuestionId = questionId;
        FileName = fileName;
        ContentType = contentType;
        FileSize = fileData.Length;
        FileData = fileData;
        Description = description;
    }

    public void UpdateDescription(string? description)
    {
        Description = description;
    }
}
