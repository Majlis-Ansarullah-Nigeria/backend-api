using HRS.Domain.Organization;
using HRS.Domain.Submissions;
using HRS.Domain.Windows;

namespace HRS.Application.Features.Submissions.MyReports;

/// <summary>
/// Derives a "my reports" row's display status (FR-SUB-001) by combining ReportInstance.Status
/// (when an instance exists yet — most rows won't, before M5.3's first Save) with
/// SubmissionWindow.StatusFor's window-level BeforeOpen/OnTime/Late/Closed. Pure and
/// DB-independent, same reason ParentBeforeChildWarnings and PeriodCalculator are their own
/// static classes — directly unit-testable without a database.
/// </summary>
public static class MyReportStatusCalculator
{
    /// <summary>
    /// No instance yet falls back to the window's own level status: BeforeOpen -&gt; Upcoming,
    /// Closed -&gt; Missed (display-only — nothing persists an actual Missed row yet; that
    /// reconciliation job doesn't exist anywhere in the plan), otherwise NotStarted. A Draft is
    /// reclassified the same way once its window closes or enters grace. Submitted/InReview/
    /// Returned/Acknowledged/Missed instances map straight through — Returned is deliberately
    /// never reclassified by a closed window, since BR-WIN-06 lets a returned report be
    /// resubmitted after hard close until its own revision deadline.
    /// </summary>
    public static ReportLineStatus Determine(SubmissionWindow window, ReportInstance? instance, UnitLevelCode level, Guid unitId, DateTimeOffset now)
    {
        var windowStatus = window.StatusFor(level, now, unitId);

        if (instance is null || instance.Status == ReportInstanceStatus.NotStarted)
        {
            return windowStatus switch
            {
                WindowLevelStatus.BeforeOpen => ReportLineStatus.Upcoming,
                WindowLevelStatus.Closed => ReportLineStatus.Missed,
                _ => ReportLineStatus.NotStarted,
            };
        }

        return instance.Status switch
        {
            ReportInstanceStatus.Draft => windowStatus switch
            {
                WindowLevelStatus.Closed => ReportLineStatus.Missed,
                WindowLevelStatus.Late => ReportLineStatus.Overdue,
                _ => ReportLineStatus.Draft,
            },
            ReportInstanceStatus.Submitted => instance.IsLate ? ReportLineStatus.SubmittedLate : ReportLineStatus.Submitted,
            ReportInstanceStatus.InReview => ReportLineStatus.InReview,
            ReportInstanceStatus.Returned => ReportLineStatus.Returned,
            ReportInstanceStatus.Acknowledged => ReportLineStatus.Acknowledged,
            ReportInstanceStatus.Missed => ReportLineStatus.Missed,
            _ => ReportLineStatus.NotStarted,
        };
    }
}
