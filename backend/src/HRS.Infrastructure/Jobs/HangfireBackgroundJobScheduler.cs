using Hangfire;
using HRS.Application.Common.Interfaces;

namespace HRS.Infrastructure.Jobs;

public sealed class HangfireBackgroundJobScheduler(IBackgroundJobClient client) : IBackgroundJobScheduler
{
    public void EnqueueBulkExport(Guid bulkExportId) =>
        client.Enqueue<BulkExportJob>(job => job.RunAsync(bulkExportId, CancellationToken.None));
}
