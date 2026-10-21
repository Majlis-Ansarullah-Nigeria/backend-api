using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.Organization;
using HRS.Domain.Submissions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Analytics;

public sealed record OverviewWindow(Guid WindowId, Guid ReportTypeId, string ReportTypeName, string PeriodLabel, DateTimeOffset OpensAt, bool IsClosed);

/// <summary>The level whose due date comes next in the current window, and how many of its units
/// still haven't submitted.</summary>
public sealed record OverviewNextDue(UnitLevelCode Level, string LevelPluralName, DateTimeOffset DueAt, int NotSubmitted);

public sealed record OverviewLevelProgress(UnitLevelCode Level, string LevelName, string LevelPluralName, DateTimeOffset? DueAt, int Expected, int Submitted);

public sealed record OverviewGroupProgress(Guid? GroupUnitId, string Name, int Expected, int Submitted);

public sealed record OverviewLevelGroups(UnitLevelCode Level, string LevelName, IReadOnlyList<OverviewGroupProgress> Groups);

public enum OverviewWindowEventKind
{
    Opens,
    Closes,
}

public sealed record OverviewWindowEvent(
    Guid WindowId,
    OverviewWindowEventKind Kind,
    DateTimeOffset At,
    string ReportTypeName,
    string PeriodLabel,
    IReadOnlyList<string> LevelNames,
    bool IsScheduled);

public sealed record SecretariatOverview(
    string UserName,
    OverviewWindow? CurrentWindow,
    int Received,
    int Expected,
    int UnreadInInbox,
    int AssignedToMe,
    OverviewNextDue? NextDue,
    int VacantHeads,
    IReadOnlyList<OverviewLevelGroups> ByZone,
    IReadOnlyList<OverviewLevelProgress> ByLevel,
    IReadOnlyList<OverviewWindowEvent> UpcomingWindows,
    int FlaggedReports,
    int ReturnedPastRevisionDeadline);

/// <summary>
/// The Secretariat overview board: the current window's progress (received vs expected, by zone,
/// by level), the inbox backlog, what's due next, vacant heads, upcoming window events and the
/// things needing attention. The current window is the most recently opened one still open, or
/// failing that the most recently opened one. Secretariat only — Leadership's overview is its own
/// board (M10.2).
/// </summary>
public sealed record GetSecretariatOverviewQuery(Guid UserId) : IRequest<Result<SecretariatOverview>>;

public sealed class GetSecretariatOverviewQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy, IIdentityService identityService, IClock clock)
    : IRequestHandler<GetSecretariatOverviewQuery, Result<SecretariatOverview>>
{
    private const int UpcomingCount = 3;

    public async ValueTask<Result<SecretariatOverview>> Handle(GetSecretariatOverviewQuery request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.HasRoleAsync(request.UserId, RoleName.Secretariat, cancellationToken))
        {
            return Result.Failure<SecretariatOverview>(Error.Forbidden("FORBIDDEN", "Only the Secretariat has this overview."));
        }

        var now = clock.UtcNow;
        var names = await identityService.GetUserNamesAsync([request.UserId], cancellationToken);
        var userName = names.GetValueOrDefault(request.UserId, string.Empty);

        var reportTypes = await db.ReportTypes.AsNoTracking().Where(r => r.ArchivedAt == null).ToDictionaryAsync(r => r.Id, cancellationToken);
        var windows = (await db.SubmissionWindows.AsNoTracking().ToListAsync(cancellationToken))
            .Where(w => reportTypes.ContainsKey(w.ReportTypeId))
            .ToList();

        var current = windows
            .Where(w => w.OpensAt <= now)
            .OrderBy(w => now < w.ClosesAt ? 0 : 1)
            .ThenByDescending(w => w.OpensAt)
            .FirstOrDefault();

        var context = await ComplianceContext.LoadAsync(db, current is null ? [] : [current.Id], cancellationToken);

        var unread = await db.ReportInstances.AsNoTracking().CountAsync(i => i.Status == ReportInstanceStatus.Submitted, cancellationToken);
        var assignedToMe = await db.ReportInstances.AsNoTracking().CountAsync(
            i => i.AssignedToUserId == request.UserId
                && (i.Status == ReportInstanceStatus.Submitted || i.Status == ReportInstanceStatus.InReview),
            cancellationToken);
        var flagged = await db.ReportInstances.AsNoTracking().CountAsync(
            i => i.IsFlagged && i.Status != ReportInstanceStatus.Acknowledged, cancellationToken);
        var returnedPastDeadline = await db.ReportInstances.AsNoTracking().CountAsync(
            i => i.Status == ReportInstanceStatus.Returned && i.RevisionDeadline != null && i.RevisionDeadline < now, cancellationToken);

        // Vacant heads, as on the organization tree — except National, which the Secretariat
        // itself runs rather than a unit head.
        var headedUnitIds = await db.RoleAssignments.AsNoTracking()
            .Where(a => a.Role == RoleName.UnitHead && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now))
            .Select(a => a.UnitId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var headed = headedUnitIds.ToHashSet();
        var vacantHeads = context.Units.Count(u => u.Level != UnitLevelCode.National && !headed.Contains(u.Id));

        var upcoming = windows
            .SelectMany(w => new[]
            {
                (Window: w, Kind: OverviewWindowEventKind.Opens, At: w.OpensAt),
                (Window: w, Kind: OverviewWindowEventKind.Closes, At: w.ClosesAt),
            })
            .Where(e => e.At > now)
            .OrderBy(e => e.At)
            .Take(UpcomingCount)
            .Select(e => new OverviewWindowEvent(
                e.Window.Id,
                e.Kind,
                e.At,
                reportTypes[e.Window.ReportTypeId].Name,
                e.Window.PeriodLabel,
                [.. ComplianceContext.DisplayOrder
                    .Where(l => e.Window.DueDates.Any(d => d.Level == l) && context.Levels.ContainsKey(l))
                    .Select(l => context.Levels[l].Name)],
                e.Window.ScheduleId is not null))
            .ToList();

        if (current is null)
        {
            return Result.Success(new SecretariatOverview(
                userName, null, 0, 0, unread, assignedToMe, null, vacantHeads, [], [], upcoming, flagged, returnedPastDeadline));
        }

        var levelsInWindow = ComplianceContext.DisplayOrder
            .Where(l => current.DueDates.Any(d => d.Level == l) && context.Levels.ContainsKey(l))
            .ToList();
        var outcomes = context.ExpectedUnits(current).Select(u => context.Outcome(current, u, now)).ToList();

        var byLevel = levelsInWindow
            .Select(level =>
            {
                var atLevel = outcomes.Where(o => o.Unit.Level == level).ToList();
                return new OverviewLevelProgress(
                    level,
                    context.Levels[level].Name,
                    context.Levels[level].PluralName,
                    current.DueAtFor(level),
                    atLevel.Count,
                    atLevel.Count(o => ComplianceBuckets.IsSubmitted(o.Bucket)));
            })
            .ToList();

        var byZone = levelsInWindow
            .Where(l => l is UnitLevelCode.Muqam or UnitLevelCode.Dila or UnitLevelCode.Zone)
            .Select(level => new OverviewLevelGroups(
                level,
                context.Levels[level].Name,
                [.. ComplianceGroups.Build(context, current, level, now)
                    .Select(g => new OverviewGroupProgress(g.GroupUnitId, g.Name, g.Expected, g.Submitted))]))
            .ToList();

        var nextDue = levelsInWindow
            .Select(level => (Level: level, DueAt: current.DueAtFor(level)))
            .Where(x => x.DueAt is { } dueAt && dueAt >= now)
            .OrderBy(x => x.DueAt)
            .Select(x => new OverviewNextDue(
                x.Level,
                context.Levels[x.Level].PluralName,
                x.DueAt!.Value,
                outcomes.Count(o => o.Unit.Level == x.Level && !ComplianceBuckets.IsSubmitted(o.Bucket))))
            .FirstOrDefault();

        var reportType = reportTypes[current.ReportTypeId];
        return Result.Success(new SecretariatOverview(
            userName,
            new OverviewWindow(current.Id, reportType.Id, reportType.Name, current.PeriodLabel, current.OpensAt, now >= current.ClosesAt),
            outcomes.Count(o => ComplianceBuckets.IsSubmitted(o.Bucket)),
            outcomes.Count,
            unread,
            assignedToMe,
            nextDue,
            vacantHeads,
            byZone,
            byLevel,
            upcoming,
            flagged,
            returnedPastDeadline));
    }
}
