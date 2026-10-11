using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.Organization;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;
using HRS.Domain.Windows;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Observer.ChildReport;

/// <summary>
/// Shared resolution for GetChildReportQuery and GetChildReportPdfQuery (FR-OBS-002) — same
/// reason Attachments' ExistingReportResolver (M5.4) exists: two handlers need the identical
/// access check and entity lookups, only what they build from the result differs.
/// </summary>
internal static class ChildReportResolver
{
    public sealed record Resolved(
        Guid ChildUnitId,
        string ChildUnitName,
        UnitLevelCode ChildLevelCode,
        ReportInstance Instance,
        SubmissionWindow Window,
        ReportType ReportType,
        TemplateSchema Schema,
        Guid ParentUnitId);

    public static async Task<Result<Resolved>> ResolveAsync(
        IAppDbContext db, IAccessPolicy accessPolicy, Guid userId, Guid childUnitId, Guid reportId, CancellationToken cancellationToken)
    {
        var childUnit = await db.Units
            .AsNoTracking()
            .Where(u => u.Id == childUnitId && u.ArchivedAt == null)
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => new { u.Name, u.ParentId, LevelCode = l.Code })
            .FirstOrDefaultAsync(cancellationToken);

        if (childUnit?.ParentId is not { } parentId)
        {
            return Result.Failure<Resolved>(Error.NotFound("UNIT_NOT_FOUND", "That unit doesn't exist."));
        }

        if (!await accessPolicy.HasRoleForUnitAsync(userId, parentId, RoleName.UnitHead, cancellationToken))
        {
            return Result.Failure<Resolved>(Error.Forbidden("FORBIDDEN", "You don't observe that unit's reports."));
        }

        var instance = await db.ReportInstances.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == reportId && i.UnitId == childUnitId, cancellationToken);
        instance ??= await db.ReportInstances.AsNoTracking()
            .FirstOrDefaultAsync(i => i.WindowId == reportId && i.UnitId == childUnitId, cancellationToken);

        if (instance is null)
        {
            return Result.Failure<Resolved>(Error.NotFound("REPORT_NOT_FOUND", "That report doesn't exist."));
        }

        if (instance.Status is ReportInstanceStatus.NotStarted or ReportInstanceStatus.Draft)
        {
            return Result.Failure<Resolved>(Error.Conflict("NOT_SUBMITTED", "This unit hasn't submitted this report yet."));
        }

        var window = await db.SubmissionWindows.AsNoTracking().FirstAsync(w => w.Id == instance.WindowId, cancellationToken);
        var reportType = await db.ReportTypes.AsNoTracking().FirstAsync(r => r.Id == window.ReportTypeId, cancellationToken);

        if (!reportType.ApplicableLevels.Contains(childUnit.LevelCode) || reportType.ObserverDepth < 1)
        {
            return Result.Failure<Resolved>(Error.Forbidden("FORBIDDEN", "This report type isn't observable."));
        }

        var templateVersion = await db.TemplateVersions.AsNoTracking().FirstAsync(v => v.Id == instance.TemplateVersionId, cancellationToken);
        var schema = JsonSerializer.Deserialize<TemplateSchema>(templateVersion.SchemaJson, JsonSerializerOptions.Web)!;

        return Result.Success(new Resolved(childUnitId, childUnit.Name, childUnit.LevelCode, instance, window, reportType, schema, parentId));
    }
}
