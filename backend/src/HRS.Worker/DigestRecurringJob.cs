using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Notifications;

namespace HRS.Worker;

/// <summary>Hangfire's resolvable unit of work for FR-NOT-004's daily digest — a thin adapter over
/// <see cref="DigestDispatcher"/>, the same shape as <see cref="OutboxDispatcherRecurringJob"/>.</summary>
public sealed partial class DigestRecurringJob(
    IAppDbContext db, IIdentityService identityService, IEmailSender emailSender, IClock clock, ILogger<DigestRecurringJob> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var sent = await DigestDispatcher.RunAsync(db, identityService, emailSender, clock, cancellationToken);

        if (sent > 0)
        {
            LogDigestsSent(sent);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Digest job sent {Count} digest email(s).")]
    private partial void LogDigestsSent(int count);
}
