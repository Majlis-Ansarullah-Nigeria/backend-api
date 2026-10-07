using HRS.Domain.Organization;
using HRS.Domain.Templates;
using HRS.Domain.Windows;

namespace HRS.Domain.UnitTests.Windows;

public class WindowScheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static WindowSchedule CreateSchedule(int opensDayOffset, int graceDays, int closesDayOffset, int muqamDueDayOffset) =>
        WindowSchedule.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ReportTypeFrequency.Monthly,
            opensDayOffset,
            graceDays,
            closesDayOffset,
            [new ScheduleLevelDueOffset(UnitLevelCode.Muqam, muqamDueDayOffset)],
            Now);

    [Fact]
    public void BuildWindowAddsTheOpensOffsetToThePeriodEnd()
    {
        var schedule = CreateSchedule(1, 3, 25, 10);
        var periodEnd = new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.Zero);

        var (opensAt, _, _, _) = schedule.BuildWindow(periodEnd);

        Assert.Equal(periodEnd.AddDays(1), opensAt);
    }

    [Fact]
    public void BuildWindowAddsTheCloseOffsetToThePeriodEnd()
    {
        var schedule = CreateSchedule(1, 3, 25, 10);
        var periodEnd = new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.Zero);

        var (_, _, closesAt, _) = schedule.BuildWindow(periodEnd);

        Assert.Equal(periodEnd.AddDays(25), closesAt);
    }

    [Fact]
    public void BuildWindowAddsEachLevelsDueOffsetToThePeriodEnd()
    {
        var schedule = CreateSchedule(1, 3, 25, 10);
        var periodEnd = new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.Zero);

        var (_, _, _, dueDates) = schedule.BuildWindow(periodEnd);

        var dueDate = Assert.Single(dueDates);
        Assert.Equal(UnitLevelCode.Muqam, dueDate.Level);
        Assert.Equal(periodEnd.AddDays(10), dueDate.DueAt);
    }

    [Fact]
    public void BuildWindowCarriesTheGraceDaysThrough()
    {
        var schedule = CreateSchedule(1, 5, 25, 10);

        var (_, graceDays, _, _) = schedule.BuildWindow(Now);

        Assert.Equal(5, graceDays);
    }

    [Fact]
    public void DeactivateThenActivateRoundTrips()
    {
        var schedule = CreateSchedule(1, 3, 25, 10);

        schedule.Deactivate();
        Assert.False(schedule.IsActive);

        schedule.Activate();
        Assert.True(schedule.IsActive);
    }

    [Fact]
    public void UpdateReplacesAllFieldsAndDueOffsets()
    {
        var schedule = CreateSchedule(1, 3, 25, 10);

        schedule.Update(ReportTypeFrequency.Quarterly, 2, 7, 30, [new ScheduleLevelDueOffset(UnitLevelCode.Dila, 15)]);

        Assert.Equal(ReportTypeFrequency.Quarterly, schedule.Frequency);
        Assert.Equal(2, schedule.OpensDayOffset);
        Assert.Equal(7, schedule.GraceDays);
        Assert.Equal(30, schedule.ClosesDayOffset);
        var offset = Assert.Single(schedule.DueOffsets);
        Assert.Equal(UnitLevelCode.Dila, offset.Level);
        Assert.Equal(15, offset.DueDayOffset);
    }
}
