using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.Organization;
using HRS.Domain.Templates;

namespace HRS.Application.Features.History;

/// <summary>
/// Who may read one unit's reports of one report type for version history and period comparison
/// (FR-HIS-005/006): the History scope (heads of the unit; observers of it, with the same
/// per-(unit, report type) observability gate as HistoryReportResolver) plus the Secretariat, the
/// recipient of every report. Observers never see confidential fields (FR-AC-006); only the
/// unit's own head sees the unit's current draft.
/// </summary>
public static class ReportReadAccess
{
    public sealed record Grant(bool HideConfidential, bool CanSeeDrafts);

    public static async Task<Result<Grant>> ResolveAsync(
        IAppDbContext db,
        IAccessPolicy accessPolicy,
        Guid userId,
        Guid unitId,
        UnitLevelCode unitLevel,
        ReportType reportType,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var scope = await HistoryScopeResolver.ResolveAsync(db, userId, now, cancellationToken);
        if (scope.UnitIds.Contains(unitId))
        {
            if (!scope.ObservedUnitIds.Contains(unitId))
            {
                return Result.Success(new Grant(HideConfidential: false, CanSeeDrafts: true));
            }

            if (reportType.ObserverDepth >= 1 && reportType.ApplicableLevels.Contains(unitLevel))
            {
                return Result.Success(new Grant(HideConfidential: true, CanSeeDrafts: false));
            }
        }

        if (await accessPolicy.HasRoleAsync(userId, RoleName.Secretariat, cancellationToken))
        {
            return Result.Success(new Grant(HideConfidential: false, CanSeeDrafts: false));
        }

        return Result.Failure<Grant>(Error.Forbidden("FORBIDDEN", "You don't have access to that unit's reports."));
    }
}
