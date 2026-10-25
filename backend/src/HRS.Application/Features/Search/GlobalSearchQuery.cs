using HRS.Application.Common.Interfaces;
using HRS.Application.Features.History;
using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.Submissions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Search;

/// <summary>How the caller reaches a report — decides which screen the palette opens.</summary>
public enum SearchReportAccess
{
    /// <summary>Secretariat: the review screen.</summary>
    Review,

    /// <summary>A unit the caller heads: edit or submitted view, by status.</summary>
    Own,

    /// <summary>A child unit the caller observes: the read-only observer view.</summary>
    Observed,
}

public sealed record SearchUnitHit(Guid Id, string Name, string Code, string LevelName, bool IsObserved);

public sealed record SearchUserHit(Guid Id, string Name, string Email);

public sealed record SearchReportHit(
    Guid InstanceId,
    Guid UnitId,
    string UnitName,
    string ReportTypeName,
    string PeriodLabel,
    string? ReferenceNumber,
    ReportInstanceStatus Status,
    SearchReportAccess Access);

public sealed record SearchReportTypeHit(Guid Id, string Name);

public sealed record GlobalSearchResults(
    IReadOnlyList<SearchUnitHit> Units,
    IReadOnlyList<SearchUserHit> Users,
    IReadOnlyList<SearchReportHit> Reports,
    IReadOnlyList<SearchReportTypeHit> ReportTypes)
{
    public static readonly GlobalSearchResults Empty = new([], [], [], []);
}

/// <summary>
/// FR-SRC-001: the Ctrl/Cmd+K palette's search across units, users, reports and templates,
/// filtered by what the caller may already open elsewhere:
/// <list type="bullet">
/// <item>Units — Secretariat/SysAdmin everything (the organization screen); heads their own and
/// observed units (the History scope).</item>
/// <item>Users — Secretariat/SysAdmin only (the users screen's rule).</item>
/// <item>Reports — Secretariat every submitted report (the inbox); heads their own units' reports
/// in open-or-past windows, and observed units' submitted reports of observable report types.</item>
/// <item>Report types — Secretariat only (template admin).</item>
/// </list>
/// Every whitespace-separated term must match somewhere (unit name/code, report type, period,
/// reference number), so "September Zone 3" narrows rather than widens. Matching runs in memory
/// over projections — same trade-off as the org tree and users screen at this org's scale.
/// </summary>
public sealed record GlobalSearchQuery(Guid UserId, string? Term) : IRequest<Result<GlobalSearchResults>>;

public sealed class GlobalSearchQueryHandler(
    IAppDbContext db,
    IAccessPolicy accessPolicy,
    IIdentityService identityService,
    IClock clock) : IRequestHandler<GlobalSearchQuery, Result<GlobalSearchResults>>
{
    public const int MaxPerGroup = 5;
    public const int MinTermLength = 2;

    public async ValueTask<Result<GlobalSearchResults>> Handle(GlobalSearchQuery request, CancellationToken cancellationToken)
    {
        var term = request.Term?.Trim() ?? string.Empty;
        if (term.Length < MinTermLength)
        {
            return Result.Success(GlobalSearchResults.Empty);
        }

        var tokens = term.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var now = clock.UtcNow;

        var isOrgAdmin = await accessPolicy.IsOrganizationAdminAsync(request.UserId, cancellationToken);
        var isSecretariat = await accessPolicy.HasRoleAsync(request.UserId, RoleName.Secretariat, cancellationToken);
        var scope = await HistoryScopeResolver.ResolveAsync(db, request.UserId, now, cancellationToken);

        if (!isOrgAdmin && !isSecretariat && scope.UnitIds.Count == 0)
        {
            return Result.Success(GlobalSearchResults.Empty);
        }

        var unitQuery = db.Units.AsNoTracking().Where(u => u.ArchivedAt == null);
        if (!isOrgAdmin && !isSecretariat)
        {
            var scopedIds = scope.UnitIds.ToList();
            unitQuery = unitQuery.Where(u => scopedIds.Contains(u.Id));
        }

        var units = await unitQuery
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id,
                (u, l) => new UnitRow(u.Id, u.Name, u.Code, l.Name, l.Code, l.Depth))
            .ToListAsync(cancellationToken);
        var unitById = units.ToDictionary(u => u.Id);

        var unitHits = isOrgAdmin || scope.UnitIds.Count > 0
            ? units
                .Where(u => isOrgAdmin || scope.UnitIds.Contains(u.Id))
                .Where(u => tokens.All(t => Matches(t, u.Name, u.Code)))
                .OrderBy(u => u.Depth).ThenBy(u => u.Name, StringComparer.OrdinalIgnoreCase)
                .Take(MaxPerGroup)
                .Select(u => new SearchUnitHit(u.Id, u.Name, u.Code, u.LevelName, scope.ObservedUnitIds.Contains(u.Id)))
                .ToList()
            : [];

        var userHits = isOrgAdmin ? await SearchUsersAsync(tokens, cancellationToken) : [];

        var reportTypes = await db.ReportTypes.AsNoTracking().Where(r => r.ArchivedAt == null).ToListAsync(cancellationToken);
        var reportTypeById = reportTypes.ToDictionary(r => r.Id);

        var reportTypeHits = isSecretariat
            ? reportTypes
                .Where(r => tokens.All(t => Matches(t, r.Name)))
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .Take(MaxPerGroup)
                .Select(r => new SearchReportTypeHit(r.Id, r.Name))
                .ToList()
            : [];

        var reportHits = await SearchReportsAsync(
            tokens, isSecretariat, scope, unitById, reportTypeById, now, cancellationToken);

        return Result.Success(new GlobalSearchResults(unitHits, userHits, reportHits, reportTypeHits));
    }

    private async Task<List<SearchUserHit>> SearchUsersAsync(string[] tokens, CancellationToken cancellationToken)
    {
        var users = await identityService.GetAllUsersAsync(cancellationToken);
        return users
            .Where(u => u.DeactivatedAt is null)
            .Where(u => tokens.All(t => Matches(t, u.Name, u.Email)))
            .OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase)
            .Take(MaxPerGroup)
            .Select(u => new SearchUserHit(u.Id, u.Name, u.Email))
            .ToList();
    }

    private async Task<List<SearchReportHit>> SearchReportsAsync(
        string[] tokens,
        bool isSecretariat,
        HistoryScopeResolver.Scope scope,
        Dictionary<Guid, UnitRow> unitById,
        Dictionary<Guid, Domain.Templates.ReportType> reportTypeById,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var reportTypeIds = reportTypeById.Keys.ToList();
        var windows = await db.SubmissionWindows.AsNoTracking()
            .Where(w => reportTypeIds.Contains(w.ReportTypeId) && w.OpensAt <= now)
            .Select(w => new { w.Id, w.ReportTypeId, w.PeriodLabel, w.OpensAt })
            .ToListAsync(cancellationToken);
        var windowById = windows.ToDictionary(w => w.Id);
        var windowIds = windows.Select(w => w.Id).ToList();

        var instanceQuery = db.ReportInstances.AsNoTracking().Where(i => windowIds.Contains(i.WindowId));
        if (!isSecretariat)
        {
            var scopedIds = scope.UnitIds.ToList();
            instanceQuery = instanceQuery.Where(i => scopedIds.Contains(i.UnitId));
        }

        var instances = await instanceQuery
            .Select(i => new { i.Id, i.UnitId, i.WindowId, i.ReferenceNumber, i.Status, i.SubmittedAt })
            .ToListAsync(cancellationToken);

        var hits = new List<(SearchReportHit Hit, DateTimeOffset OpensAt)>();
        foreach (var instance in instances)
        {
            if (!windowById.TryGetValue(instance.WindowId, out var window)
                || !reportTypeById.TryGetValue(window.ReportTypeId, out var reportType)
                || !unitById.TryGetValue(instance.UnitId, out var unit))
            {
                continue;
            }

            SearchReportAccess access;
            if (isSecretariat)
            {
                if (instance.SubmittedAt is null)
                {
                    continue;
                }

                access = SearchReportAccess.Review;
            }
            else if (scope.ObservedUnitIds.Contains(instance.UnitId))
            {
                // Same per-report-type observability check as GetHistoryQuery: the unit being
                // observable for some type doesn't make every type's report visible.
                if (instance.SubmittedAt is null
                    || reportType.ObserverDepth < 1
                    || !reportType.ApplicableLevels.Contains(unit.LevelCode))
                {
                    continue;
                }

                access = SearchReportAccess.Observed;
            }
            else
            {
                access = SearchReportAccess.Own;
            }

            if (!tokens.All(t => Matches(t, unit.Name, unit.Code, reportType.Name, window.PeriodLabel, instance.ReferenceNumber)))
            {
                continue;
            }

            hits.Add((new SearchReportHit(
                instance.Id, unit.Id, unit.Name, reportType.Name, window.PeriodLabel,
                instance.ReferenceNumber, instance.Status, access), window.OpensAt));
        }

        return hits
            .OrderByDescending(h => h.OpensAt)
            .ThenBy(h => h.Hit.UnitName, StringComparer.OrdinalIgnoreCase)
            .Take(MaxPerGroup)
            .Select(h => h.Hit)
            .ToList();
    }

    private static bool Matches(string token, params string?[] fields) =>
        fields.Any(f => f is not null && f.Contains(token, StringComparison.OrdinalIgnoreCase));

    private sealed record UnitRow(Guid Id, string Name, string Code, string LevelName, Domain.Organization.UnitLevelCode LevelCode, int Depth);
}
