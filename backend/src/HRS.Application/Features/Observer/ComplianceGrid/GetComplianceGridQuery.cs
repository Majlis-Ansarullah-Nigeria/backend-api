using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Submissions.MyReports;
using HRS.Domain.Access;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Unit = HRS.Domain.Organization.Unit;

namespace HRS.Application.Features.Observer.ComplianceGrid;

/// <summary>
/// FR-OBS-001's compliance grid. ParentUnitId/ReportTypeId are optional: omitted, the handler
/// picks the caller's first eligible parent unit (one they head that has children) and that
/// unit's first eligible report type, and always returns the full available-options lists so the
/// frontend can offer a picker without a second round trip.
/// </summary>
public sealed record GetComplianceGridQuery(Guid UserId, Guid? ParentUnitId, Guid? ReportTypeId, int Periods)
    : IRequest<Result<ComplianceGridResult>>;

public sealed class GetComplianceGridQueryHandler(IAppDbContext db, IIdentityService identityService, IClock clock)
    : IRequestHandler<GetComplianceGridQuery, Result<ComplianceGridResult>>
{
    private static readonly ComplianceGridResult EmptyResult = new(null, null, null, null, [], [], null, [], []);

    public async ValueTask<Result<ComplianceGridResult>> Handle(GetComplianceGridQuery request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var periodsToShow = Math.Clamp(request.Periods <= 0 ? 4 : request.Periods, 1, 24);

        var headUnitIds = await db.RoleAssignments
            .AsNoTracking()
            .Where(a => a.UserId == request.UserId && a.Role == RoleName.UnitHead
                && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now))
            .Select(a => a.UnitId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (headUnitIds.Count == 0)
        {
            return request.ParentUnitId is null
                ? Result.Success(EmptyResult)
                : Result.Failure<ComplianceGridResult>(
                    Error.Forbidden("FORBIDDEN", "You don't head a unit with children at that id."));
        }

        var headUnits = await db.Units
            .AsNoTracking()
            .Where(u => headUnitIds.Contains(u.Id) && u.ArchivedAt == null)
            .ToListAsync(cancellationToken);

        var headUnitIdSet = headUnits.Select(u => u.Id).ToHashSet();
        var childCountsByParent = await db.Units
            .AsNoTracking()
            .Where(u => u.ParentId != null && u.ArchivedAt == null && headUnitIdSet.Contains(u.ParentId.Value))
            .Select(u => u.ParentId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        var parentsWithChildren = childCountsByParent.ToHashSet();

        // In-memory ordering (StringComparer.Ordinal isn't SQL-translatable) — same lesson as
        // GetOrganizationTreeQueryHandler materializing before calling non-translatable members.
        var eligibleParents = headUnits
            .Where(u => parentsWithChildren.Contains(u.Id))
            .OrderBy(u => u.Name, StringComparer.Ordinal)
            .ToList();

        // An explicit id must be checked even when the caller is eligible for nothing else —
        // "forbidden" and "nothing to show" are different outcomes (IDOR-safety: never silently
        // fall through a rejected explicit id into the auto-pick empty state).
        if (request.ParentUnitId is { } requestedParentId)
        {
            var requested = eligibleParents.Find(u => u.Id == requestedParentId);
            if (requested is null)
            {
                return Result.Failure<ComplianceGridResult>(
                    Error.Forbidden("FORBIDDEN", "You don't head a unit with children at that id."));
            }
        }

        if (eligibleParents.Count == 0)
        {
            return Result.Success(EmptyResult);
        }

        Unit parent;
        if (request.ParentUnitId is { } selectedParentId)
        {
            parent = eligibleParents.Find(u => u.Id == selectedParentId)!;
        }
        else
        {
            parent = eligibleParents[0];
        }

        var availableParentUnits = eligibleParents.Select(u => new ObservableUnitOption(u.Id, u.Name)).ToList();

        var children = await db.Units
            .AsNoTracking()
            .Where(u => u.ParentId == parent.Id && u.ArchivedAt == null)
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => new { Unit = u, Level = l })
            .ToListAsync(cancellationToken);

        var childLevel = children[0].Level;
        var childLevelCode = childLevel.Code;

        var reportTypes = await db.ReportTypes.AsNoTracking().Where(r => r.ArchivedAt == null).ToListAsync(cancellationToken);

        var availableReportTypes = reportTypes
            .Where(r => r.ApplicableLevels.Contains(childLevelCode) && r.ObserverDepth >= 1)
            .OrderBy(r => r.Name, StringComparer.Ordinal)
            .ToList();

        if (availableReportTypes.Count == 0)
        {
            return Result.Success(new ComplianceGridResult(
                parent.Id, parent.Name, childLevel.Name, childLevel.PluralName,
                availableParentUnits, [], null, [], []));
        }

        var selectedReportType = request.ReportTypeId is { } requestedTypeId
            ? availableReportTypes.Find(r => r.Id == requestedTypeId) ?? availableReportTypes[0]
            : availableReportTypes[0];

        var windows = await db.SubmissionWindows
            .AsNoTracking()
            .Where(w => w.ReportTypeId == selectedReportType.Id)
            .ToListAsync(cancellationToken);

        var windowsInScope = windows
            .Where(w => w.DueDates.Any(d => d.Level == childLevelCode))
            .OrderByDescending(w => w.OpensAt)
            .Take(periodsToShow)
            .OrderBy(w => w.OpensAt)
            .ToList();

        var windowIds = windowsInScope.Select(w => w.Id).ToList();
        var childUnitIds = children.Select(c => c.Unit.Id).ToList();

        var instances = await db.ReportInstances
            .AsNoTracking()
            .Where(i => windowIds.Contains(i.WindowId) && childUnitIds.Contains(i.UnitId))
            .ToListAsync(cancellationToken);
        var instanceByKey = instances.ToDictionary(i => (i.WindowId, i.UnitId));

        var activeHeadAssignments = await db.RoleAssignments
            .AsNoTracking()
            .Where(a => a.Role == RoleName.UnitHead && childUnitIds.Contains(a.UnitId)
                && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now))
            .OrderByDescending(a => a.StartsAt)
            .Select(a => new { a.UnitId, a.UserId })
            .ToListAsync(cancellationToken);
        var headUserIdByUnit = activeHeadAssignments.GroupBy(a => a.UnitId).ToDictionary(g => g.Key, g => g.First().UserId);
        var headNames = await identityService.GetUserNamesAsync(headUserIdByUnit.Values.Distinct().ToList(), cancellationToken);

        var periods = windowsInScope
            .Select(w => new CompliancePeriod(w.Id, w.PeriodLabel, w.DueAtFor(childLevelCode)!.Value))
            .ToList();

        var rows = children
            .Select(entry =>
            {
                var unit = entry.Unit;
                var headName = headUserIdByUnit.TryGetValue(unit.Id, out var headUserId) && headNames.TryGetValue(headUserId, out var name)
                    ? name
                    : null;

                var cells = windowsInScope
                    .Select(w =>
                    {
                        instanceByKey.TryGetValue((w.Id, unit.Id), out var instance);
                        var status = MyReportStatusCalculator.Determine(w, instance, childLevelCode, unit.Id, now);
                        return new ComplianceGridCell(w.Id, status, instance?.Id);
                    })
                    .ToList();

                return new ComplianceGridRow(unit.Id, unit.Name, headName, headName is null, cells);
            })
            .OrderBy(r => r.UnitName, StringComparer.Ordinal)
            .ToList();

        var availableReportTypeOptions = availableReportTypes.Select(r => new ReportTypeOption(r.Id, r.Name)).ToList();

        return Result.Success(new ComplianceGridResult(
            parent.Id,
            parent.Name,
            childLevel.Name,
            childLevel.PluralName,
            availableParentUnits,
            availableReportTypeOptions,
            selectedReportType.Id,
            periods,
            rows));
    }
}
