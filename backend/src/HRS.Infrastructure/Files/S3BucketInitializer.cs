using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HRS.Infrastructure.Files;

/// <summary>Dev-only (gated by StorageOptions.AutoCreateBucket — see its own doc comment):
/// creates the RustFS bucket and a permissive CORS rule on startup so the browser can PUT
/// attachments straight to it without any manual `mc` setup. Best-effort — logs and moves on
/// rather than crashing the app if storage isn't reachable yet.</summary>
public sealed partial class S3BucketInitializer(IAmazonS3 client, IOptions<StorageOptions> options, ILogger<S3BucketInitializer> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.AutoCreateBucket)
        {
            return;
        }

        var bucket = options.Value.Bucket;

        try
        {
            if (!await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(client, bucket))
            {
                await client.PutBucketAsync(new PutBucketRequest { BucketName = bucket }, cancellationToken);
            }

            await client.PutCORSConfigurationAsync(new PutCORSConfigurationRequest
            {
                BucketName = bucket,
                Configuration = new CORSConfiguration
                {
                    Rules =
                    [
                        new CORSRule
                        {
                            AllowedMethods = ["PUT", "GET"],
                            AllowedOrigins = ["*"],
                            AllowedHeaders = ["*"],
                        },
                    ],
                },
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            LogBucketSetupFailed(ex, bucket);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Couldn't auto-create/configure the dev storage bucket {Bucket} — attachments won't work until it exists.")]
    private partial void LogBucketSetupFailed(Exception exception, string bucket);
}
