namespace HRS.Application.Common.Interfaces;

/// <summary>
/// S3-compatible object storage for attachments (FR-SUB-005). The browser uploads bytes directly
/// to storage using a presigned URL — the request body never passes through the API — so this
/// interface only ever deals with metadata and streams the API itself needs to read back
/// (verifying the upload, feeding the virus scanner).
/// </summary>
public interface IFileStorage
{
    /// <summary>A time-limited URL the browser can PUT the file's bytes to directly. Pure HMAC
    /// computation — no network call — so this isn't async.</summary>
    PresignedUpload CreatePresignedUpload(string key, TimeSpan expiresIn);

    /// <summary>Null if nothing has been uploaded to that key yet (the client hasn't PUT the
    /// bytes, or never will).</summary>
    Task<StoredFile?> GetAsync(string key, CancellationToken cancellationToken);

    /// <summary>Best-effort — deleting a key that doesn't exist is not an error.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);

    /// <summary>FR-HIS-004's background bulk export — the Worker already holds the finished
    /// ZIP/workbook bytes in memory (it built them), so unlike attachments it writes directly
    /// rather than handing the browser a presigned PUT.</summary>
    Task UploadAsync(string key, byte[] content, string contentType, CancellationToken cancellationToken);

    /// <summary>A time-limited URL the browser can GET the file's bytes from directly — symmetric
    /// with CreatePresignedUpload, so a large export downloads straight from storage rather than
    /// streaming through the API process.</summary>
    PresignedDownload CreatePresignedDownload(string key, TimeSpan expiresIn);
}

public sealed record PresignedUpload(string Url, DateTimeOffset ExpiresAt);

public sealed record PresignedDownload(string Url, DateTimeOffset ExpiresAt);

public sealed record StoredFile(Stream Content, long SizeBytes) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
