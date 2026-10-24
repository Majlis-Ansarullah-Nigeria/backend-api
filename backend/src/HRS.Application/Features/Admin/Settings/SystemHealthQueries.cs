using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Audit;
using HRS.Application.Features.Notifications;
using HRS.Domain.Access;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Admin.Settings;

/// <summary>Notifications in the outbox: sent in the last 24 hours, waiting for another attempt,
/// given up after the dispatcher's maximum attempts, and not tried yet.</summary>
public sealed record NotificationHealth(int SentLast24Hours, int WaitingRetry, int GivenUp, int Pending);

public sealed record SystemHealth(JobQueueStatus? Jobs, NotificationHealth Notifications, long AttachmentBytes, int AttachmentCount, string EmailTransport);

internal static class HealthAccess
{
    /// <summary>The Secretariat and the system administrator see system health and can retry
    /// failed notifications; only the administrator changes settings.</summary>
    public static async Task<bool> CanViewAsync(IAccessPolicy accessPolicy, Guid userId, CancellationToken cancellationToken) =>
        await accessPolicy.HasRoleAsync(userId, RoleName.SysAdmin, cancellationToken)
        || await accessPolicy.HasRoleAsync(userId, RoleName.Secretariat, cancellationToken);

    public static readonly Error Forbidden = Error.Forbidden("FORBIDDEN", "Only the Secretariat and the system administrator see system health.");
}

/// <summary>FR-ADM-005's system health: job queue, notifications, storage used by attachments.</summary>
public sealed record GetSystemHealthQuery(Guid UserId) : IRequest<Result<SystemHealth>>;

public sealed class GetSystemHealthQueryHandler(
    IAppDbContext db, IAccessPolicy accessPolicy, IJobQueueMonitor jobQueueMonitor, IEmailTransportInfo emailTransport, IClock clock)
    : IRequestHandler<GetSystemHealthQuery, Result<SystemHealth>>
{
    public async ValueTask<Result<SystemHealth>> Handle(GetSystemHealthQuery request, CancellationToken cancellationToken)
    {
        if (!await HealthAccess.CanViewAsync(accessPolicy, request.UserId, cancellationToken))
        {
            return Result.Failure<SystemHealth>(HealthAccess.Forbidden);
        }

        JobQueueStatus? jobs;
        try
        {
            jobs = jobQueueMonitor.GetStatus();
        }
        catch (Exception)
        {
            // The job store being unreachable is itself the health signal — shown as "unknown".
            jobs = null;
        }

        var since = clock.UtcNow.AddHours(-24);
        var max = OutboxDispatcher.MaxAttempts;
        var outbox = db.OutboxMessages.AsNoTracking();
        var notifications = new NotificationHealth(
            await outbox.CountAsync(m => m.ProcessedAt != null && m.ProcessedAt >= since, cancellationToken),
            await outbox.CountAsync(m => m.ProcessedAt == null && m.Attempts > 0 && m.Attempts < max, cancellationToken),
            await outbox.CountAsync(m => m.ProcessedAt == null && m.Attempts >= max, cancellationToken),
            await outbox.CountAsync(m => m.ProcessedAt == null && m.Attempts == 0, cancellationToken));

        var attachmentBytes = await db.ReportAttachments.AsNoTracking().SumAsync(a => (long?)a.SizeBytes, cancellationToken) ?? 0;
        var attachmentCount = await db.ReportAttachments.AsNoTracking().CountAsync(cancellationToken);

        return Result.Success(new SystemHealth(jobs, notifications, attachmentBytes, attachmentCount, emailTransport.Description));
    }
}

/// <summary>FR-ADM-005's "Retry failed notifications": every message the dispatcher gave up on gets
/// a fresh set of attempts on its next run. Audited with the count.</summary>
public sealed record RetryFailedNotificationsCommand(Guid UserId) : IRequest<Result<int>>;

public sealed class RetryFailedNotificationsCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IAuditLog auditLog)
    : IRequestHandler<RetryFailedNotificationsCommand, Result<int>>
{
    public async ValueTask<Result<int>> Handle(RetryFailedNotificationsCommand request, CancellationToken cancellationToken)
    {
        if (!await HealthAccess.CanViewAsync(accessPolicy, request.UserId, cancellationToken))
        {
            return Result.Failure<int>(HealthAccess.Forbidden);
        }

        var max = OutboxDispatcher.MaxAttempts;
        var givenUp = await db.OutboxMessages.Where(m => m.ProcessedAt == null && m.Attempts >= max).ToListAsync(cancellationToken);
        foreach (var message in givenUp)
        {
            message.ResetForRetry();
        }

        auditLog.Add(new AuditEntry(
            AuditActions.NotificationsRetried, request.UserId, "notification", null, $"Retried {givenUp.Count} failed notification(s)",
            After: new { Retried = givenUp.Count }));
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(givenUp.Count);
    }
}
