using HRS.Domain.Common;
using HRS.Domain.Organization;
using HRS.Domain.Submissions;
using HRS.Domain.Windows;

namespace HRS.Application.Features.Submissions.ReportInstances;

/// <summary>
/// Whether a report can currently be saved — one pure check shared by the GET (to show a
/// read-only banner and a CanSave flag) and the Save command (to actually reject the write), so
/// the two can never disagree about what's editable right now. Null means editable.
/// </summary>
public static class ReportEditability
{
    public static Error? Check(SubmissionWindow window, ReportInstance? instance, UnitLevelCode level, Guid unitId, DateTimeOffset now)
    {
        if (instance is not null
            && instance.Status is not (ReportInstanceStatus.NotStarted or ReportInstanceStatus.Draft or ReportInstanceStatus.Returned))
        {
            return Error.Conflict("REPORT_LOCKED", "This report can no longer be edited.");
        }

        var windowStatus = window.StatusFor(level, now, unitId);

        if (windowStatus == WindowLevelStatus.BeforeOpen)
        {
            return Error.Conflict("WINDOW_NOT_OPEN", "This window hasn't opened yet — the form can be previewed but not saved.");
        }

        if (windowStatus == WindowLevelStatus.Closed)
        {
            // BR-WIN-06: a Returned report can still be edited and resubmitted after hard close,
            // up to its own revision deadline — everything else is locked once the window closes.
            var canStillResubmit = instance?.Status == ReportInstanceStatus.Returned
                && instance.RevisionDeadline is { } deadline && now <= deadline;

            if (!canStillResubmit)
            {
                return Error.Conflict("WINDOW_CLOSED", "This window has closed.");
            }
        }

        return null;
    }
}
