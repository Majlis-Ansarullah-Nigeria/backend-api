using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Submissions.MyReports;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Common;
using HRS.Domain.Organization;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;
using HRS.Domain.Windows;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.History;

/// <summary>
/// Shared resolution for single-report PDF/Excel downloads and the per-instance recheck inside a
/// bulk export (FR-HIS-003/004) — the exact same scope rule as GetHistoryQuery, including the
/// per-(unit, report type) observability gate the fix for that query's own leak added. One
/// instance at a time, unlike GetHistoryQuery's bulk pass, since downloads are requested by id.
/// </summary>
public static class HistoryReportResolver
{
    public sealed record Resolved(
        ReportInstance Instance,
        SubmissionWindow Window,
        ReportType ReportType,
        TemplateSchema Schema,
        Guid UnitId,
        string UnitName,
        UnitLevelCode UnitLevelCode,
        bool IsObserved);

    public static async Task<Result<Resolved>> ResolveAsync(
        IAppDbContext db, Guid userId, Guid instanceId, IClock clock, CancellationToken cancellationToken)
    {
        var instance = await db.ReportInstances.AsNoTracking().FirstOrDefaultAsync(i => i.Id == instanceId, cancellationToken);
        if (instance is null)
        {
            return Result.Failure<Resolved>(Error.NotFound("REPORT_NOT_FOUND", "That report doesn't exist."));
        }

        var now = clock.UtcNow;
        var scope = await HistoryScopeResolver.ResolveAsync(db, userId, now, cancellationToken);
        if (!scope.UnitIds.Contains(instance.UnitId))
        {
            return Result.Failure<Resolved>(Error.Forbidden("FORBIDDEN", "You don't have access to that unit's reports."));
        }

        var window = await db.SubmissionWindows.AsNoTracking().FirstAsync(w => w.Id == instance.WindowId, cancellationToken);
        var reportType = await db.ReportTypes.AsNoTracking().FirstAsync(r => r.Id == window.ReportTypeId, cancellationToken);
        var unitRow = await db.Units.AsNoTracking()
            .Where(u => u.Id == instance.UnitId)
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => new { u.Name, LevelCode = l.Code })
            .FirstAsync(cancellationToken);

        var isObserved = scope.ObservedUnitIds.Contains(instance.UnitId);
        if (isObserved && (reportType.ObserverDepth < 1 || !reportType.ApplicableLevels.Contains(unitRow.LevelCode)))
        {
            return Result.Failure<Resolved>(Error.Forbidden("FORBIDDEN", "This report type isn't observable."));
        }

        var status = MyReportStatusCalculator.Determine(window, instance, unitRow.LevelCode, instance.UnitId, now);
        if (!HistoryStatuses.All.Contains(status))
        {
            return Result.Failure<Resolved>(Error.Conflict("NOT_SUBMITTED", "This unit hasn't submitted this report yet."));
        }

        var templateVersion = await db.TemplateVersions.AsNoTracking().FirstAsync(v => v.Id == instance.TemplateVersionId, cancellationToken);
        var schema = JsonSerializer.Deserialize<TemplateSchema>(templateVersion.SchemaJson, JsonSerializerOptions.Web)!;

        return Result.Success(new Resolved(instance, window, reportType, schema, instance.UnitId, unitRow.Name, unitRow.LevelCode, isObserved));
    }
}
