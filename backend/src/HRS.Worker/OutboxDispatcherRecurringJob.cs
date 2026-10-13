using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Notifications;

namespace HRS.Worker;

/// <summary>Hangfire's resolvable unit of work for M8.1's outbox dispatcher — a thin adapter that
/// hands off to the actual (and independently testable) <see cref="OutboxDispatcher"/>, the same
/// shape as <see cref="WindowJobsRecurringJob"/>.</summary>
public sealed partial class OutboxDispatcherRecurringJob(
    IAppDbContext db, IIdentityService identityService, IEmailSender emailSender, IClock clock, ILogger<OutboxDispatcherRecurringJob> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var sent = await OutboxDispatcher.RunAsync(db, identityService, emailSender, clock, cancellationToken);

        if (sent > 0)
        {
            LogMessagesSent(sent);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox dispatcher sent {Count} notification(s).")]
    private partial void LogMessagesSent(int count);
}
