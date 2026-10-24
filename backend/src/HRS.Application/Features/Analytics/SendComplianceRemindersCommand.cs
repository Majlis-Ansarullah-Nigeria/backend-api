using System.Globalization;
using HRS.Application.Common.Interfaces;
using HRS.Application.Common.Options;
using HRS.Application.Features.Notifications;
using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.Notifications;
using HRS.Domain.Organization;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HRS.Application.Features.Analytics;

/// <summary>UnitsReminded: units whose head(s) were notified. UnitsWithoutHead: units that
/// hadn't submitted but have no active head to remind.</summary>
public sealed record SendRemindersResult(int UnitsReminded, int UnitsWithoutHead);

/// <summary>
/// FR-DSH-001's one-click reminder from a compliance table row: every unit at <paramref name="Level"/>
/// in the group (a zone, or one department) that hasn't submitted for the window yet. Before a
/// unit's (possibly extended) due date it gets the Reminder wording, after it the Overdue wording
/// (BR-WIN-03). No rate limit — the Secretariat decides when to remind. Secretariat only;
/// Leadership's dashboard access is read-only.
/// </summary>
public sealed record SendComplianceRemindersCommand(Guid UserId, Guid WindowId, UnitLevelCode Level, Guid? GroupUnitId)
    : IRequest<Result<SendRemindersResult>>;

public sealed class SendComplianceRemindersCommandHandler(
    IAppDbContext db, IAccessPolicy accessPolicy, IClock clock, IOptions<AppOptions> appOptions, IOrganizationBrandingProvider brandingProvider)
    : IRequestHandler<SendComplianceRemindersCommand, Result<SendRemindersResult>>
{
    public async ValueTask<Result<SendRemindersResult>> Handle(SendComplianceRemindersCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.HasRoleAsync(request.UserId, RoleName.Secretariat, cancellationToken))
        {
            return Result.Failure<SendRemindersResult>(Error.Forbidden("FORBIDDEN", "Only the Secretariat can send reminders."));
        }

        var window = await db.SubmissionWindows.AsNoTracking().FirstOrDefaultAsync(w => w.Id == request.WindowId, cancellationToken);
        if (window is null)
        {
            return Result.Failure<SendRemindersResult>(Error.NotFound("WINDOW_NOT_FOUND", "That window doesn't exist."));
        }

        var now = clock.UtcNow;
        if (now >= window.ClosesAt)
        {
            return Result.Failure<SendRemindersResult>(Error.Conflict("WINDOW_CLOSED", "This window has closed."));
        }

        var reportTypeName = await db.ReportTypes.AsNoTracking()
            .Where(r => r.Id == window.ReportTypeId).Select(r => r.Name).FirstAsync(cancellationToken);
        var context = await ComplianceContext.LoadAsync(db, [window.Id], cancellationToken);

        var targets = context.ExpectedUnits(window, request.Level)
            .Where(u => context.GroupOf(u).GroupUnitId == request.GroupUnitId)
            .Select(u => context.Outcome(window, u, now))
            .Where(o => ComplianceBuckets.IsRemindable(o.Bucket))
            .Select(o => o.Unit)
            .ToList();

        var targetIds = targets.Select(u => u.Id).ToList();
        var headsByUnit = (await db.RoleAssignments.AsNoTracking()
                .Where(a => a.Role == RoleName.UnitHead && targetIds.Contains(a.UnitId)
                    && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now))
                .Select(a => new { a.UnitId, a.UserId })
                .ToListAsync(cancellationToken))
            .GroupBy(a => a.UnitId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.UserId).Distinct().ToList());

        var publicUrl = appOptions.Value.PublicUrl.TrimEnd('/');
        var branding = await brandingProvider.GetAsync(cancellationToken);
        var reminded = 0;
        foreach (var unit in targets)
        {
            if (!headsByUnit.TryGetValue(unit.Id, out var headUserIds))
            {
                continue;
            }

            var dueAt = window.DueAtFor(unit.Level, unit.Id)!.Value;
            var placeholders = new Dictionary<string, string>
            {
                ["unitName"] = unit.Name,
                ["reportTypeName"] = reportTypeName,
                ["periodLabel"] = window.PeriodLabel,
                ["dueDate"] = branding.Date(dueAt),
                ["link"] = $"{publicUrl}/reports/{window.Id}/edit?unit={unit.Id}",
            };
            var eventType = now > dueAt ? NotificationEventType.Overdue : NotificationEventType.Reminder;
            foreach (var headUserId in headUserIds)
            {
                OutboxWriter.EnqueueForUser(db, headUserId, eventType, placeholders, now);
            }

            reminded++;
        }

        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(new SendRemindersResult(reminded, targets.Count - reminded));
    }
}
