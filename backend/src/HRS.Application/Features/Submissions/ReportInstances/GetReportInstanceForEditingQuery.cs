using System.Globalization;
using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Common;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Submissions.ReportInstances;

/// <summary>
/// Loads the report-form screen's data for one (window, unit) pair. Id is resolved two ways — as
/// an existing ReportInstance id (scoped to UnitId, so one unit can never address another's
/// report by guessing an id), or, when nothing has been saved yet, as the SubmissionWindow id
/// itself (M5.2's "my reports" rows link with the window id whenever no instance exists — see
/// that milestone's commit). Read-only: never creates a row — Save (a command) does that.
/// </summary>
public sealed record GetReportInstanceForEditingQuery(Guid UserId, Guid Id, Guid UnitId) : IRequest<Result<ReportInstanceForEditing>>;

public sealed class GetReportInstanceForEditingQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<GetReportInstanceForEditingQuery, Result<ReportInstanceForEditing>>
{
    public async ValueTask<Result<ReportInstanceForEditing>> Handle(GetReportInstanceForEditingQuery request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.CanEditReportForUnitAsync(request.UserId, request.UnitId, cancellationToken))
        {
            return Result.Failure<ReportInstanceForEditing>(
                Error.Forbidden("FORBIDDEN", "You don't have access to this unit's reports."));
        }

        var instance = await db.ReportInstances.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == request.Id && i.UnitId == request.UnitId, cancellationToken);

        var window = await db.SubmissionWindows.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == (instance != null ? instance.WindowId : request.Id), cancellationToken);

        if (window is null)
        {
            return Result.Failure<ReportInstanceForEditing>(Error.NotFound("REPORT_NOT_FOUND", "Report not found."));
        }

        var reportType = await db.ReportTypes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == window.ReportTypeId, cancellationToken);
        if (reportType is null)
        {
            return Result.Failure<ReportInstanceForEditing>(Error.NotFound("REPORT_NOT_FOUND", "Report not found."));
        }

        var unitRow = await db.Units.AsNoTracking()
            .Where(u => u.Id == request.UnitId)
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => new { u.Name, l.Code })
            .FirstOrDefaultAsync(cancellationToken);
        if (unitRow is null)
        {
            return Result.Failure<ReportInstanceForEditing>(Error.NotFound("REPORT_NOT_FOUND", "Report not found."));
        }

        TemplateVersion templateVersion;
        if (instance is not null)
        {
            var pinned = await db.TemplateVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == instance.TemplateVersionId, cancellationToken);
            if (pinned is null)
            {
                return Result.Failure<ReportInstanceForEditing>(Error.NotFound("REPORT_NOT_FOUND", "Report not found."));
            }

            templateVersion = pinned;
        }
        else
        {
            var published = await db.TemplateVersions.AsNoTracking()
                .FirstOrDefaultAsync(v => v.ReportTypeId == reportType.Id && v.Status == TemplateVersionStatus.Published, cancellationToken);
            if (published is null)
            {
                return Result.Failure<ReportInstanceForEditing>(
                    Error.Conflict("NO_PUBLISHED_TEMPLATE", "This report type has no published template yet."));
            }

            templateVersion = published;
        }

        var now = clock.UtcNow;
        var editError = ReportEditability.Check(window, instance, unitRow.Code, request.UnitId, now);

        var schema = JsonSerializer.Deserialize<TemplateSchema>(templateVersion.SchemaJson, JsonSerializerOptions.Web)!;
        var answers = instance is null
            ? []
            : JsonSerializer.Deserialize<Dictionary<string, object?>>(instance.AnswersJson, JsonSerializerOptions.Web) ?? [];

        return new ReportInstanceForEditing(
            instance?.Id,
            window.Id,
            request.UnitId,
            unitRow.Name,
            reportType.Id,
            reportType.Name,
            window.PeriodLabel,
            templateVersion.Id,
            templateVersion.VersionNumber,
            schema,
            (int)unitRow.Code,
            answers,
            instance?.Status ?? ReportInstanceStatus.NotStarted,
            editError is null,
            window.OpensAt,
            window.DueAtFor(unitRow.Code, request.UnitId),
            window.EffectiveClosesAtFor(request.UnitId),
            instance?.RevisionDeadline,
            instance is null ? null : instance.RowVersion.ToString(CultureInfo.InvariantCulture));
    }
}
