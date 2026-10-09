using System.Globalization;
using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Submissions.ReportInstances;

public sealed class SaveReportDraftCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<SaveReportDraftCommand, Result<SaveReportDraftResult>>
{
    public async ValueTask<Result<SaveReportDraftResult>> Handle(SaveReportDraftCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.CanEditReportForUnitAsync(request.UserId, request.UnitId, cancellationToken))
        {
            return Result.Failure<SaveReportDraftResult>(
                Error.Forbidden("FORBIDDEN", "You don't have access to this unit's reports."));
        }

        // Tracked, not AsNoTracking — this one gets mutated and saved.
        var instance = await db.ReportInstances.FirstOrDefaultAsync(i => i.Id == request.Id && i.UnitId == request.UnitId, cancellationToken);

        var window = await db.SubmissionWindows.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == (instance != null ? instance.WindowId : request.Id), cancellationToken);
        if (window is null)
        {
            return Result.Failure<SaveReportDraftResult>(Error.NotFound("REPORT_NOT_FOUND", "Report not found."));
        }

        var reportType = await db.ReportTypes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == window.ReportTypeId, cancellationToken);
        if (reportType is null)
        {
            return Result.Failure<SaveReportDraftResult>(Error.NotFound("REPORT_NOT_FOUND", "Report not found."));
        }

        var unitRow = await db.Units.AsNoTracking()
            .Where(u => u.Id == request.UnitId)
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => new { l.Code })
            .FirstOrDefaultAsync(cancellationToken);
        if (unitRow is null)
        {
            return Result.Failure<SaveReportDraftResult>(Error.NotFound("REPORT_NOT_FOUND", "Report not found."));
        }

        var now = clock.UtcNow;
        var editError = ReportEditability.Check(window, instance, unitRow.Code, request.UnitId, now);
        if (editError is not null)
        {
            return Result.Failure<SaveReportDraftResult>(editError);
        }

        var answersJson = JsonSerializer.Serialize(request.Answers);

        if (instance is null)
        {
            var published = await db.TemplateVersions.AsNoTracking()
                .FirstOrDefaultAsync(v => v.ReportTypeId == reportType.Id && v.Status == TemplateVersionStatus.Published, cancellationToken);
            if (published is null)
            {
                return Result.Failure<SaveReportDraftResult>(
                    Error.Conflict("NO_PUBLISHED_TEMPLATE", "This report type has no published template yet."));
            }

            instance = ReportInstance.Create(Guid.CreateVersion7(), window.Id, request.UnitId, published.Id, now);
            instance.Save(answersJson);
            db.ReportInstances.Add(instance);
        }
        else
        {
            // If-Match arrives as a quoted HTTP entity tag (e.g. "123") — strip the quotes before
            // parsing the raw numeric RowVersion underneath.
            var unquoted = request.IfMatch?.Trim('"');
            if (string.IsNullOrEmpty(unquoted) || !uint.TryParse(unquoted, out var claimedVersion))
            {
                return Result.Failure<SaveReportDraftResult>(
                    Error.Validation("IF_MATCH_REQUIRED", "A valid If-Match header is required to save an existing report."));
            }

            db.Entry(instance).Property(i => i.RowVersion).OriginalValue = claimedVersion;
            instance.Save(answersJson);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<SaveReportDraftResult>(Error.Conflict(
                "CONCURRENCY_CONFLICT", "Someone else has saved changes to this report. Reload to see the latest version."));
        }

        return new SaveReportDraftResult(instance.Id, instance.Status, instance.RowVersion.ToString(CultureInfo.InvariantCulture));
    }
}
