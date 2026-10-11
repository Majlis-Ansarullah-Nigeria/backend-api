using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.Organization;
using HRS.Domain.Submissions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Review.Inbox;

/// <summary>FR-REV-001: every report the National Secretariat has received, filterable by type,
/// period (window), the submitting unit's level, zone, status, late and flagged. Never shows
/// NotStarted/Draft/Missed — those haven't "arrived" yet; a report counts as incoming from its
/// first Submit onward (Submitted, InReview, Returned, Acknowledged), matching the mockup's own
/// "Reports sent to the National Secretariat" subtitle.</summary>
public sealed record GetInboxQuery(
    Guid UserId,
    Guid? ReportTypeId,
    Guid? WindowId,
    UnitLevelCode? UnitLevel,
    Guid? ZoneUnitId,
    ReportInstanceStatus? Status,
    bool? IsLate,
    bool? IsFlagged) : IRequest<Result<IReadOnlyList<InboxItem>>>;

public sealed class GetInboxQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy)
    : IRequestHandler<GetInboxQuery, Result<IReadOnlyList<InboxItem>>>
{
    private static readonly HashSet<ReportInstanceStatus> IncomingStatuses =
        [ReportInstanceStatus.Submitted, ReportInstanceStatus.InReview, ReportInstanceStatus.Returned, ReportInstanceStatus.Acknowledged];

    public async ValueTask<Result<IReadOnlyList<InboxItem>>> Handle(GetInboxQuery request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.HasRoleAsync(request.UserId, RoleName.Secretariat, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<InboxItem>>(Error.Forbidden("FORBIDDEN", "Only the Secretariat has an inbox."));
        }

        var instances = await db.ReportInstances.AsNoTracking()
            .Where(i => IncomingStatuses.Contains(i.Status))
            .ToListAsync(cancellationToken);

        if (request.Status is { } status)
        {
            instances = [.. instances.Where(i => i.Status == status)];
        }

        if (request.IsLate is { } isLate)
        {
            instances = [.. instances.Where(i => i.IsLate == isLate)];
        }

        if (request.IsFlagged is { } isFlagged)
        {
            instances = [.. instances.Where(i => i.IsFlagged == isFlagged)];
        }

        if (instances.Count == 0)
        {
            return Result.Success<IReadOnlyList<InboxItem>>([]);
        }

        var windowIds = instances.Select(i => i.WindowId).Distinct().ToList();
        var windows = await db.SubmissionWindows.AsNoTracking().Where(w => windowIds.Contains(w.Id)).ToListAsync(cancellationToken);
        if (request.WindowId is { } windowId)
        {
            windows = [.. windows.Where(w => w.Id == windowId)];
        }

        var reportTypeIds = windows.Select(w => w.ReportTypeId).Distinct().ToList();
        var reportTypes = await db.ReportTypes.AsNoTracking()
            .Where(r => reportTypeIds.Contains(r.Id) && r.RecipientLevel == UnitLevelCode.National)
            .ToListAsync(cancellationToken);
        if (request.ReportTypeId is { } reportTypeId)
        {
            reportTypes = [.. reportTypes.Where(r => r.Id == reportTypeId)];
        }

        var reportTypeById = reportTypes.ToDictionary(r => r.Id);
        var windowsInScope = windows.Where(w => reportTypeById.ContainsKey(w.ReportTypeId)).ToDictionary(w => w.Id);

        var unitIds = instances.Select(i => i.UnitId).Distinct().ToList();
        var units = await db.Units.AsNoTracking()
            .Where(u => unitIds.Contains(u.Id))
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => new { Unit = u, LevelCode = l.Code })
            .ToListAsync(cancellationToken);
        var unitById = units.ToDictionary(u => u.Unit.Id);

        // Zone resolution needs every unit on a zone path (not just the reporting units
        // themselves) to turn a code-based path segment into a real Zone unit/name.
        var allUnits = await db.Units.AsNoTracking().ToListAsync(cancellationToken);
        var unitByPath = allUnits.ToDictionary(u => u.Path);

        var rows = new List<InboxItem>();
        foreach (var instance in instances)
        {
            if (!windowsInScope.TryGetValue(instance.WindowId, out var window)
                || !reportTypeById.TryGetValue(window.ReportTypeId, out var reportType)
                || !unitById.TryGetValue(instance.UnitId, out var unitEntry))
            {
                continue;
            }

            var (zoneUnitId, zoneName) = ResolveZone(unitEntry.Unit.Path, unitEntry.LevelCode, unitByPath);

            if (request.UnitLevel is { } unitLevel && unitEntry.LevelCode != unitLevel)
            {
                continue;
            }

            if (request.ZoneUnitId is { } requestedZoneUnitId && zoneUnitId != requestedZoneUnitId)
            {
                continue;
            }

            rows.Add(new InboxItem(
                instance.Id, reportType.Id, reportType.Name, unitEntry.Unit.Id, unitEntry.Unit.Name, unitEntry.LevelCode,
                zoneUnitId, zoneName, window.Id, window.PeriodLabel, instance.Status, instance.IsLate, instance.IsFlagged,
                instance.SubmittedAt, instance.ReferenceNumber));
        }

        IReadOnlyList<InboxItem> sorted = [.. rows.OrderByDescending(r => r.SubmittedAt)];
        return Result.Success(sorted);
    }

    private static (Guid? ZoneUnitId, string? ZoneName) ResolveZone(
        string unitPath, UnitLevelCode unitLevel, Dictionary<string, Domain.Organization.Unit> unitByPath)
    {
        if (unitLevel is UnitLevelCode.National or UnitLevelCode.Department)
        {
            return (null, null);
        }

        var zonePath = unitLevel == UnitLevelCode.Zone
            ? unitPath
            : string.Join('.', unitPath.Split('.').Take(2));

        return unitByPath.TryGetValue(zonePath, out var zoneUnit) ? (zoneUnit.Id, zoneUnit.Name) : (null, null);
    }
}
