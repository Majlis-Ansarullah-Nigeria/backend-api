using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Submissions.MyReports;
using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.Observer;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Observer.Nudge;

public sealed class SendNudgeCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IIdentityService identityService, IEmailSender emailSender, IClock clock)
    : IRequestHandler<SendNudgeCommand, Result>
{
    private const int CooldownHours = 24;

    public async ValueTask<Result> Handle(SendNudgeCommand request, CancellationToken cancellationToken)
    {
        var childUnit = await db.Units
            .AsNoTracking()
            .Where(u => u.Id == request.ChildUnitId && u.ArchivedAt == null)
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => new { u.Name, u.ParentId, LevelCode = l.Code })
            .FirstOrDefaultAsync(cancellationToken);

        if (childUnit?.ParentId is not { } parentId)
        {
            return Result.Failure(Error.NotFound("UNIT_NOT_FOUND", "That unit doesn't exist."));
        }

        if (!await accessPolicy.HasRoleForUnitAsync(request.UserId, parentId, RoleName.UnitHead, cancellationToken))
        {
            return Result.Failure(Error.Forbidden("FORBIDDEN", "You don't observe that unit's reports."));
        }

        var window = await db.SubmissionWindows.AsNoTracking().FirstOrDefaultAsync(w => w.Id == request.WindowId, cancellationToken);
        if (window is null)
        {
            return Result.Failure(Error.NotFound("WINDOW_NOT_FOUND", "That window doesn't exist."));
        }

        var reportType = await db.ReportTypes.AsNoTracking().FirstAsync(r => r.Id == window.ReportTypeId, cancellationToken);
        if (!reportType.ApplicableLevels.Contains(childUnit.LevelCode) || reportType.ObserverDepth < 1)
        {
            return Result.Failure(Error.Forbidden("FORBIDDEN", "This report type isn't observable."));
        }

        var now = clock.UtcNow;
        var instance = await db.ReportInstances.AsNoTracking()
            .FirstOrDefaultAsync(i => i.WindowId == request.WindowId && i.UnitId == request.ChildUnitId, cancellationToken);
        var status = MyReportStatusCalculator.Determine(window, instance, childUnit.LevelCode, request.ChildUnitId, now);

        if (status != ReportLineStatus.Overdue)
        {
            return Result.Failure(Error.Conflict("NOT_OVERDUE", "This unit isn't overdue for this report."));
        }

        var cooldownStart = now.AddHours(-CooldownHours);
        var lastNudge = await db.Nudges
            .AsNoTracking()
            .Where(n => n.UnitId == request.ChildUnitId)
            .OrderByDescending(n => n.SentAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastNudge is not null && lastNudge.SentAt > cooldownStart)
        {
            var retryAfterSeconds = (int)(lastNudge.SentAt.AddHours(CooldownHours) - now).TotalSeconds;
            return Result.Failure(Error.RateLimited(
                "NUDGE_RATE_LIMITED", "This unit was already nudged in the last 24 hours.", Math.Max(retryAfterSeconds, 1)));
        }

        var headUserIds = await db.RoleAssignments
            .AsNoTracking()
            .Where(a => a.Role == RoleName.UnitHead && a.UnitId == request.ChildUnitId
                && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now))
            .Select(a => a.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var headUserId in headUserIds)
        {
            var head = await identityService.GetUserAsync(headUserId, cancellationToken);
            if (head is null)
            {
                continue;
            }

            await emailSender.QueueAsync(
                new EmailMessage(
                    head.Email,
                    $"Reminder: {reportType.Name} — {window.PeriodLabel}",
                    $"{childUnit.Name}'s {window.PeriodLabel} {reportType.Name} is overdue. Please submit it as soon as you can."),
                cancellationToken);
        }

        db.Nudges.Add(HRS.Domain.Observer.Nudge.Create(Guid.CreateVersion7(), request.ChildUnitId, request.UserId, now));
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
