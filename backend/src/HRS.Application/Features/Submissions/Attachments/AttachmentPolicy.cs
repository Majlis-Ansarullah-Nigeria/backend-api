namespace HRS.Application.Features.Submissions.Attachments;

/// <summary>Fixed platform-wide limits (FR-SUB-005) — not per-template-configurable.</summary>
public static class AttachmentPolicy
{
    public const long MaxFileSizeBytes = 20L * 1024 * 1024;

    public const long MaxReportTotalBytes = 100L * 1024 * 1024;

    public static readonly IReadOnlySet<string> AllowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif",
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-powerpoint",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
    };
}
