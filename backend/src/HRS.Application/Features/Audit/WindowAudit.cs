using HRS.Domain.Windows;

namespace HRS.Application.Features.Audit;

/// <summary>The window fields an audit entry's before/after values show (FR-AUD-001).</summary>
public static class WindowAudit
{
    public sealed record WindowSnapshot(
        string PeriodLabel, DateTimeOffset OpensAt, int GraceDays, DateTimeOffset ClosesAt, IReadOnlyDictionary<string, DateTimeOffset> DueDates, int TargetUnits);

    public static WindowSnapshot Snapshot(SubmissionWindow window) =>
        new(
            window.PeriodLabel,
            window.OpensAt,
            window.GraceDays,
            window.ClosesAt,
            window.DueDates.ToDictionary(d => d.Level.ToString(), d => d.DueAt),
            window.TargetUnitIds.Count);
}
