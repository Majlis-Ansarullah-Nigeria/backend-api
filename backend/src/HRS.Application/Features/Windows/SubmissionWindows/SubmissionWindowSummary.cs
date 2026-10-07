using HRS.Domain.Windows;

namespace HRS.Application.Features.Windows.SubmissionWindows;

public sealed record WindowExtensionSummary(Guid UnitId, DateTimeOffset NewDueAt, DateTimeOffset NewClosesAt, string Reason, DateTimeOffset GrantedAt)
{
    public static WindowExtensionSummary FromEntity(WindowExtension extension) => new(
        extension.UnitId, extension.NewDueAt, extension.NewClosesAt, extension.Reason, extension.GrantedAt);
}

public sealed record WindowReopenSummary(Guid UnitId, DateTimeOffset ReopenedUntil, string Reason, DateTimeOffset ReopenedAt)
{
    public static WindowReopenSummary FromEntity(WindowReopen reopen) => new(
        reopen.UnitId, reopen.ReopenedUntil, reopen.Reason, reopen.ReopenedAt);
}

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
    IReadOnlyList<Guid> TargetUnitIds,
    IReadOnlyList<WindowExtensionSummary> Extensions,
    IReadOnlyList<WindowReopenSummary> Reopens)
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
        window.TargetUnitIds,
        window.Extensions.Select(WindowExtensionSummary.FromEntity).ToList(),
        window.Reopens.Select(WindowReopenSummary.FromEntity).ToList());
}
