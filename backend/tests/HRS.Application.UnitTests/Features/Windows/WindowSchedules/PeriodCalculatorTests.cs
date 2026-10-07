using HRS.Application.Features.Windows.WindowSchedules;
using HRS.Domain.Templates;

namespace HRS.Application.UnitTests.Features.Windows.WindowSchedules;

public class PeriodCalculatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(ReportTypeFrequency.Monthly, 12)]
    [InlineData(ReportTypeFrequency.Quarterly, 4)]
    [InlineData(ReportTypeFrequency.Annual, 1)]
    public void PeriodsAheadForMatchesTheFrequency(ReportTypeFrequency frequency, int expected)
    {
        Assert.Equal(expected, PeriodCalculator.PeriodsAheadFor(frequency));
    }

    [Fact]
    public void MonthlyPeriodZeroIsTheCurrentCalendarMonth()
    {
        var (label, end) = PeriodCalculator.GetPeriod(ReportTypeFrequency.Monthly, Now, 0);

        Assert.Equal("October 2026", label);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(-1), end);
    }

    [Fact]
    public void MonthlyPeriodsAdvanceByOneCalendarMonthPerOffset()
    {
        var (label, _) = PeriodCalculator.GetPeriod(ReportTypeFrequency.Monthly, Now, 3);

        Assert.Equal("January 2027", label);
    }

    [Fact]
    public void QuarterlyPeriodZeroIsTheCurrentQuarter()
    {
        var (label, _) = PeriodCalculator.GetPeriod(ReportTypeFrequency.Quarterly, Now, 0);

        Assert.Equal("Q4 2026", label);
    }

    [Fact]
    public void QuarterlyPeriodsAdvanceByOneQuarterPerOffset()
    {
        var (label, _) = PeriodCalculator.GetPeriod(ReportTypeFrequency.Quarterly, Now, 1);

        Assert.Equal("Q1 2027", label);
    }

    [Fact]
    public void AnnualPeriodZeroIsTheCurrentYear()
    {
        var (label, _) = PeriodCalculator.GetPeriod(ReportTypeFrequency.Annual, Now, 0);

        Assert.Equal("2026", label);
    }

    [Fact]
    public void AnnualPeriodsAdvanceByOneYearPerOffset()
    {
        var (label, _) = PeriodCalculator.GetPeriod(ReportTypeFrequency.Annual, Now, 2);

        Assert.Equal("2028", label);
    }

    [Fact]
    public void AdHocFrequencyThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PeriodCalculator.GetPeriod(ReportTypeFrequency.AdHoc, Now, 0));
    }
}
