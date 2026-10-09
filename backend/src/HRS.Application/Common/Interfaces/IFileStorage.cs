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
}

public sealed record PresignedUpload(string Url, DateTimeOffset ExpiresAt);

public sealed record StoredFile(Stream Content, long SizeBytes) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
