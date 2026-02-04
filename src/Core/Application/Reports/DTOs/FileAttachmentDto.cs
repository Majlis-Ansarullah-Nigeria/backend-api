namespace ManagementApi.Application.Reports.DTOs;

public record FileAttachmentDto
{
    public Guid Id { get; init; }
    public Guid ReportSubmissionId { get; init; }
    public Guid QuestionId { get; init; }
    public string FileName { get; init; } = default!;
    public string ContentType { get; init; } = default!;
    public long FileSize { get; init; }
    public string? Description { get; init; }
    public DateTime CreatedOn { get; init; }
    public string FileSizeFormatted => FormatFileSize(FileSize);

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

public record UploadFileRequest
{
    public Guid SubmissionId { get; init; }
    public Guid QuestionId { get; init; }
    public string FileName { get; init; } = default!;
    public string ContentType { get; init; } = default!;
    public byte[] FileData { get; init; } = default!;
    public string? Description { get; init; }
}

public record FileUploadRequest
{
    public Microsoft.AspNetCore.Http.IFormFile File { get; init; } = default!;
    public Guid QuestionId { get; init; }
    public string? Description { get; init; }
}

public record FileDownloadDto
{
    public string FileName { get; init; } = default!;
    public string ContentType { get; init; } = default!;
    public byte[] FileData { get; init; } = default!;
}
