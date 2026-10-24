using Hangfire;
using HRS.Application.Common.Interfaces;

namespace HRS.Infrastructure.Jobs;

public sealed class HangfireJobQueueMonitor(JobStorage storage) : IJobQueueMonitor
{
    public JobQueueStatus GetStatus()
    {
        var stats = storage.GetMonitoringApi().GetStatistics();
        return new JobQueueStatus((int)stats.Servers, stats.Enqueued, stats.Processing, stats.Scheduled, stats.Failed);
    }
}
