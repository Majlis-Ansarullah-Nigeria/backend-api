using HRS.Domain.Organization;
using HRS.Domain.Windows;

namespace HRS.Domain.UnitTests.Windows;

public class SubmissionWindowTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static SubmissionWindow CreateWindow(
        DateTimeOffset opensAt, int graceDays, DateTimeOffset closesAt, DateTimeOffset muqamDueAt) =>
        SubmissionWindow.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "October 2026",
            opensAt,
            graceDays,
            closesAt,
            [new WindowLevelDueDate(UnitLevelCode.Muqam, muqamDueAt)],
            Now);

    [Fact]
    public void DueAtForReturnsNullWhenTheLevelHasNoDueDate()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(20), Now.AddDays(10));

        Assert.Null(window.DueAtFor(UnitLevelCode.Zone));
    }

    [Fact]
    public void GraceEndForIsDueDatePlusGraceDays()
    {
        var dueAt = Now.AddDays(10);
        var window = CreateWindow(Now, 3, Now.AddDays(30), dueAt);

        Assert.Equal(dueAt.AddDays(3), window.GraceEndFor(UnitLevelCode.Muqam));
    }

    [Fact]
    public void GraceEndForIsCappedByTheHardClose()
    {
        var dueAt = Now.AddDays(10);
        var closesAt = Now.AddDays(11);
        var window = CreateWindow(Now, 30, closesAt, dueAt);

        Assert.Equal(closesAt, window.GraceEndFor(UnitLevelCode.Muqam));
    }

    [Fact]
    public void StatusForIsBeforeOpenBeforeOpensAt()
    {
        var window = CreateWindow(Now.AddDays(5), 3, Now.AddDays(30), Now.AddDays(15));

        Assert.Equal(WindowLevelStatus.BeforeOpen, window.StatusFor(UnitLevelCode.Muqam, Now));
    }

    [Fact]
    public void StatusForIsOnTimeBetweenOpenAndDue()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(30), Now.AddDays(10));

        Assert.Equal(WindowLevelStatus.OnTime, window.StatusFor(UnitLevelCode.Muqam, Now.AddDays(5)));
    }

    [Fact]
    public void StatusForIsLateBetweenDueAndGraceEnd()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(30), Now.AddDays(10));

        Assert.Equal(WindowLevelStatus.Late, window.StatusFor(UnitLevelCode.Muqam, Now.AddDays(11)));
    }

    [Fact]
    public void StatusForIsClosedAfterGraceEnd()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(30), Now.AddDays(10));

        Assert.Equal(WindowLevelStatus.Closed, window.StatusFor(UnitLevelCode.Muqam, Now.AddDays(14)));
    }

    [Fact]
    public void StatusForIsClosedAfterTheHardCloseEvenWithinGrace()
    {
        // Grace would extend to day 40, but the hard close at day 12 wins.
        var window = CreateWindow(Now, 30, Now.AddDays(12), Now.AddDays(10));

        Assert.Equal(WindowLevelStatus.Closed, window.StatusFor(UnitLevelCode.Muqam, Now.AddDays(13)));
    }

    [Fact]
    public void StatusForIsClosedForALevelWithNoDueDate()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(30), Now.AddDays(10));

        Assert.Equal(WindowLevelStatus.Closed, window.StatusFor(UnitLevelCode.Zone, Now.AddDays(5)));
    }

    [Fact]
    public void UpdateReplacesAllFieldsAndDueDates()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(30), Now.AddDays(10));
        var newDueAt = Now.AddDays(12);

        window.Update("November 2026", Now.AddDays(1), 5, Now.AddDays(40), [new WindowLevelDueDate(UnitLevelCode.Dila, newDueAt)]);

        Assert.Equal("November 2026", window.PeriodLabel);
        Assert.Equal(5, window.GraceDays);
        Assert.Null(window.DueAtFor(UnitLevelCode.Muqam));
        Assert.Equal(newDueAt, window.DueAtFor(UnitLevelCode.Dila));
    }

    [Fact]
    public void GrantExtensionOverridesDueAtForOnlyTheGrantedUnit()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(30), Now.AddDays(10));
        var unitId = Guid.NewGuid();
        var otherUnitId = Guid.NewGuid();
        var newDueAt = Now.AddDays(20);

        window.GrantExtension([unitId], newDueAt, Now.AddDays(25), "Flooding delayed the count.", Now);

        Assert.Equal(newDueAt, window.DueAtFor(UnitLevelCode.Muqam, unitId));
        Assert.Equal(Now.AddDays(10), window.DueAtFor(UnitLevelCode.Muqam, otherUnitId));
        Assert.Equal(Now.AddDays(10), window.DueAtFor(UnitLevelCode.Muqam));
    }

    [Fact]
    public void GrantExtensionOverridesEffectiveCloseForOnlyTheGrantedUnit()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(30), Now.AddDays(10));
        var unitId = Guid.NewGuid();
        var otherUnitId = Guid.NewGuid();
        var newClosesAt = Now.AddDays(40);

        window.GrantExtension([unitId], Now.AddDays(20), newClosesAt, "Reason", Now);

        Assert.Equal(newClosesAt, window.EffectiveClosesAtFor(unitId));
        Assert.Equal(Now.AddDays(30), window.EffectiveClosesAtFor(otherUnitId));
    }

    [Fact]
    public void TheLatestExtensionForAUnitWins()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(30), Now.AddDays(10));
        var unitId = Guid.NewGuid();

        window.GrantExtension([unitId], Now.AddDays(15), Now.AddDays(20), "First", Now);
        window.GrantExtension([unitId], Now.AddDays(25), Now.AddDays(35), "Second, later", Now.AddDays(1));

        Assert.Equal(Now.AddDays(25), window.DueAtFor(UnitLevelCode.Muqam, unitId));
        Assert.Equal(Now.AddDays(35), window.EffectiveClosesAtFor(unitId));
    }

    [Fact]
    public void StatusForIsLateForAUnitWithinItsExtendedGracePeriod()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(12), Now.AddDays(10));
        var unitId = Guid.NewGuid();

        window.GrantExtension([unitId], Now.AddDays(20), Now.AddDays(25), "Reason", Now);

        // Day 21 is past the window's original hard close (day 12) and past the extended due
        // date (day 20), but still within the extended grace (day 20 + 3).
        Assert.Equal(WindowLevelStatus.Late, window.StatusFor(UnitLevelCode.Muqam, Now.AddDays(21), unitId));
    }

    [Fact]
    public void StatusForIsClosedForAUnitWithNoReopenAfterItsEffectiveClose()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(12), Now.AddDays(10));
        var unitId = Guid.NewGuid();

        Assert.Equal(WindowLevelStatus.Closed, window.StatusFor(UnitLevelCode.Muqam, Now.AddDays(13), unitId));
    }

    [Fact]
    public void ReopenAllowsLateSubmissionForOnlyTheReopenedUnitUntilTheNewDeadline()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(12), Now.AddDays(10));
        var unitId = Guid.NewGuid();
        var otherUnitId = Guid.NewGuid();
        var reopenedUntil = Now.AddDays(20);

        window.Reopen([unitId], reopenedUntil, "Late due to a connectivity outage.", Now.AddDays(13));

        Assert.Equal(WindowLevelStatus.Late, window.StatusFor(UnitLevelCode.Muqam, Now.AddDays(15), unitId));
        Assert.Equal(WindowLevelStatus.Closed, window.StatusFor(UnitLevelCode.Muqam, Now.AddDays(15), otherUnitId));
    }

    [Fact]
    public void ReopenNoLongerAppliesAfterItsOwnDeadlinePasses()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(12), Now.AddDays(10));
        var unitId = Guid.NewGuid();
        var reopenedUntil = Now.AddDays(20);

        window.Reopen([unitId], reopenedUntil, "Reason", Now.AddDays(13));

        Assert.Equal(WindowLevelStatus.Closed, window.StatusFor(UnitLevelCode.Muqam, Now.AddDays(21), unitId));
    }
}
