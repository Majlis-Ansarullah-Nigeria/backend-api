using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Windows.Jobs;

namespace HRS.Worker;

/// <summary>Hangfire's resolvable unit of work for BR-WIN-08's window jobs — a thin adapter that
/// hands off to the actual (and independently testable) <see cref="WindowJobRunner"/>.</summary>
public sealed partial class WindowJobsRecurringJob(IAppDbContext db, IClock clock, ILogger<WindowJobsRecurringJob> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var created = await WindowJobRunner.RunAsync(db, clock, cancellationToken);

        if (created > 0)
        {
            LogEventsRecorded(created);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Window job runner recorded {Count} new event(s).")]
    private partial void LogEventsRecorded(int count);
}
