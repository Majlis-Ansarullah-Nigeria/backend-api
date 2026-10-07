using HRS.Domain.Windows;

namespace HRS.Application.Features.Windows.SubmissionWindows;

public sealed record SubmissionWindowSummary(
    Guid Id,
    Guid ReportTypeId,
    string PeriodLabel,
    DateTimeOffset OpensAt,
    int GraceDays,
    DateTimeOffset ClosesAt,
    IReadOnlyList<WindowDueDate> DueDates,
    DateTimeOffset CreatedAt,
    Guid? ScheduleId,
    IReadOnlyList<Guid> TargetUnitIds)
{
    public static SubmissionWindowSummary FromEntity(SubmissionWindow window) => new(
        window.Id,
        window.ReportTypeId,
        window.PeriodLabel,
        window.OpensAt,
        window.GraceDays,
        window.ClosesAt,
        window.DueDates.Select(d => new WindowDueDate(d.Level, d.DueAt)).ToList(),
        window.CreatedAt,
        window.ScheduleId,
        window.TargetUnitIds);
}
