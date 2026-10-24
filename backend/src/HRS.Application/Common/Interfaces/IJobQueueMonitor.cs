namespace HRS.Application.Common.Interfaces;

/// <summary>FR-ADM-005's job queue status. Servers is how many worker processes are alive.</summary>
public sealed record JobQueueStatus(int Servers, long Enqueued, long Processing, long Scheduled, long Failed);

public interface IJobQueueMonitor
{
    JobQueueStatus GetStatus();
}
