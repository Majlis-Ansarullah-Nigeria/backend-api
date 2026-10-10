using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Submissions.Review;

public sealed class GetReportPreviousAnswersQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy)
    : IRequestHandler<GetReportPreviousAnswersQuery, Result<PreviousReportAnswers?>>
{
    public async ValueTask<Result<PreviousReportAnswers?>> Handle(GetReportPreviousAnswersQuery request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.CanEditReportForUnitAsync(request.UserId, request.UnitId, cancellationToken))
        {
            return Result.Failure<PreviousReportAnswers?>(Error.Forbidden("FORBIDDEN", "You don't have access to this unit's reports."));
        }

        // Same dual-mode resolution as Get/SaveReportDraft — works even for a never-saved report
        // (request.ReportId is then the window id), since "copy from last period" is exactly the
        // kind of thing you'd want as your very first action on a blank report.
        var instance = await db.ReportInstances.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == request.ReportId && i.UnitId == request.UnitId, cancellationToken);

        var window = await db.SubmissionWindows.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == (instance != null ? instance.WindowId : request.ReportId), cancellationToken);
        if (window is null)
        {
            return Result.Failure<PreviousReportAnswers?>(Error.NotFound("REPORT_NOT_FOUND", "Report not found."));
        }

        var previous = await db.ReportInstances.AsNoTracking()
            .Join(db.SubmissionWindows.AsNoTracking(), i => i.WindowId, w => w.Id, (i, w) => new { Instance = i, Window = w })
            .Where(x => x.Instance.UnitId == request.UnitId
                && x.Window.ReportTypeId == window.ReportTypeId
                && x.Instance.WindowId != window.Id
                && x.Instance.SubmittedAt != null)
            .OrderByDescending(x => x.Instance.SubmittedAt)
            .Select(x => new { x.Instance.AnswersJson, x.Window.PeriodLabel })
            .FirstOrDefaultAsync(cancellationToken);

        if (previous is null)
        {
            return Result.Success<PreviousReportAnswers?>(null);
        }

        var answers = JsonSerializer.Deserialize<Dictionary<string, object?>>(previous.AnswersJson, JsonSerializerOptions.Web) ?? [];
        return new PreviousReportAnswers(previous.PeriodLabel, answers);
    }
}
