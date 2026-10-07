using HRS.Application.Features.Windows.SubmissionWindows;
using HRS.Domain.Organization;

namespace HRS.Application.UnitTests.Features.Windows.SubmissionWindows;

public class ParentBeforeChildWarningsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NoWarningWhenDueDatesFollowTheRollUpOrder()
    {
        WindowDueDate[] dueDates =
        [
            new(UnitLevelCode.Muqam, Now.AddDays(5)),
            new(UnitLevelCode.Dila, Now.AddDays(10)),
            new(UnitLevelCode.Zone, Now.AddDays(15)),
        ];

        Assert.Empty(ParentBeforeChildWarnings.Detect(dueDates));
    }

    [Fact]
    public void WarnsWhenADilaIsDueBeforeItsMuqamaat()
    {
        WindowDueDate[] dueDates =
        [
            new(UnitLevelCode.Muqam, Now.AddDays(5)),
            new(UnitLevelCode.Dila, Now.AddDays(3)),
        ];

        var warnings = ParentBeforeChildWarnings.Detect(dueDates);

        Assert.Single(warnings);
        Assert.Contains("Dila is due before Muqam", warnings[0]);
    }

    [Fact]
    public void WarnsOnceForEachViolatedPairInTheChain()
    {
        WindowDueDate[] dueDates =
        [
            new(UnitLevelCode.Muqam, Now.AddDays(10)),
            new(UnitLevelCode.Dila, Now.AddDays(5)),
            new(UnitLevelCode.Zone, Now.AddDays(1)),
        ];

        var warnings = ParentBeforeChildWarnings.Detect(dueDates);

        Assert.Equal(2, warnings.Count);
    }

    [Fact]
    public void DoesNotWarnWhenOnlyOneLevelOfAPairHasADueDate()
    {
        WindowDueDate[] dueDates = [new(UnitLevelCode.Dila, Now.AddDays(5))];

        Assert.Empty(ParentBeforeChildWarnings.Detect(dueDates));
    }

    [Fact]
    public void EqualDueDatesDoNotWarn()
    {
        var sameDay = Now.AddDays(5);
        WindowDueDate[] dueDates = [new(UnitLevelCode.Muqam, sameDay), new(UnitLevelCode.Dila, sameDay)];

        Assert.Empty(ParentBeforeChildWarnings.Detect(dueDates));
    }
}
