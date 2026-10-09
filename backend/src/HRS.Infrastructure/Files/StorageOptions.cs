namespace HRS.Infrastructure.Files;

public sealed class StorageOptions
{
    public required string Endpoint { get; set; }

    public required string Bucket { get; set; }

    public required string AccessKey { get; set; }

    public required string SecretKey { get; set; }

    public string Region { get; set; } = "auto";

    /// <summary>Dev-only (RustFS): create the bucket and a permissive CORS rule on startup if
    /// they don't exist, so the browser can PUT straight to it without manual setup. Never set
    /// in production — a real S3/R2/B2 bucket is ops-managed, and silently rewriting its CORS
    /// policy on every deploy is not this app's call to make.</summary>
    public bool AutoCreateBucket { get; set; }
}
