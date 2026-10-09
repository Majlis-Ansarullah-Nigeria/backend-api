using Amazon.S3;
using Amazon.S3.Model;
using HRS.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace HRS.Infrastructure.Files;

public sealed class S3FileStorage(IAmazonS3 client, IOptions<StorageOptions> options) : IFileStorage
{
    private readonly StorageOptions _options = options.Value;

    public PresignedUpload CreatePresignedUpload(string key, TimeSpan expiresIn)
    {
        var expiresAt = DateTime.UtcNow.Add(expiresIn);
        var url = client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = expiresAt,
        });

        return new PresignedUpload(url, new DateTimeOffset(expiresAt, TimeSpan.Zero));
    }

    public async Task<StoredFile?> GetAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.GetObjectAsync(new GetObjectRequest { BucketName = _options.Bucket, Key = key }, cancellationToken);
            return new StoredFile(response.ResponseStream, response.ContentLength);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = _options.Bucket, Key = key }, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Already gone — deleting is idempotent.
        }
    }
}
