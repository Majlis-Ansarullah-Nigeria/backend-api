using HRS.Domain.Templates;
using HRS.Domain.Windows;

namespace HRS.Application.Features.Windows.WindowSchedules;

public sealed record WindowScheduleSummary(
    Guid Id,
    Guid ReportTypeId,
    ReportTypeFrequency Frequency,
    int OpensDayOffset,
    int GraceDays,
    int ClosesDayOffset,
    IReadOnlyList<ScheduleDueOffset> DueOffsets,
    bool IsActive,
    DateTimeOffset CreatedAt)
{
    public static WindowScheduleSummary FromEntity(WindowSchedule schedule) => new(
        schedule.Id,
        schedule.ReportTypeId,
        schedule.Frequency,
        schedule.OpensDayOffset,
        schedule.GraceDays,
        schedule.ClosesDayOffset,
        schedule.DueOffsets.Select(o => new ScheduleDueOffset(o.Level, o.DueDayOffset)).ToList(),
        schedule.IsActive,
        schedule.CreatedAt);
}
