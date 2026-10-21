using HRS.Application.Features.Submissions.MyReports;
using HRS.Domain.Submissions;

namespace HRS.Application.Features.Analytics;

/// <summary>Where one unit stands in one window for the compliance views (FR-DSH-001).</summary>
public enum ComplianceBucket
{
    OnTime,
    Late,
    Missed,
    Drafting,
    NotStarted,
    Upcoming,
}

public static class ComplianceBuckets
{
    /// <summary>Submitted, in review, returned and acknowledged reports all count as submitted —
    /// on time or late by the IsLate flag set at submission (a Returned report keeps the flag of
    /// its last submission). An unsubmitted report with a saved draft is Drafting, without one
    /// NotStarted; either becomes Missed once the window has closed for that unit.</summary>
    public static ComplianceBucket Classify(ReportLineStatus status, ReportInstance? instance) =>
        status switch
        {
            ReportLineStatus.Submitted or ReportLineStatus.InReview or ReportLineStatus.Returned or ReportLineStatus.Acknowledged
                => instance?.IsLate == true ? ComplianceBucket.Late : ComplianceBucket.OnTime,
            ReportLineStatus.SubmittedLate => ComplianceBucket.Late,
            ReportLineStatus.Missed => ComplianceBucket.Missed,
            ReportLineStatus.Draft or ReportLineStatus.Overdue => ComplianceBucket.Drafting,
            ReportLineStatus.Upcoming => ComplianceBucket.Upcoming,
            _ => ComplianceBucket.NotStarted,
        };

    public static bool IsSubmitted(ComplianceBucket bucket) => bucket is ComplianceBucket.OnTime or ComplianceBucket.Late;

    /// <summary>Who a "Remind" click reaches: the window is open for the unit and it hasn't
    /// submitted yet — whether or not its due date has passed.</summary>
    public static bool IsRemindable(ComplianceBucket bucket) => bucket is ComplianceBucket.Drafting or ComplianceBucket.NotStarted;
}
