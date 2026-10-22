using HRS.Application.Features.Analytics;
using HRS.Domain.Analytics;
using HRS.Domain.Organization;
using HRS.Domain.Templates;

namespace HRS.Application.UnitTests.Features.Analytics;

public class IndicatorCalculatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid Window = Guid.NewGuid();

    // National → Zone → Dila A (Muqam A1, A2), Dila B (Muqam B1); a Department with no filers.
    private static readonly IndicatorCalculator.UnitNode National = new(Guid.NewGuid(), null, "National", UnitLevelCode.National, "nat");
    private static readonly IndicatorCalculator.UnitNode Zone = new(Guid.NewGuid(), National.Id, "Zone", UnitLevelCode.Zone, "nat.z1");
    private static readonly IndicatorCalculator.UnitNode DilaA = new(Guid.NewGuid(), Zone.Id, "Dila A", UnitLevelCode.Dila, "nat.z1.da");
    private static readonly IndicatorCalculator.UnitNode DilaB = new(Guid.NewGuid(), Zone.Id, "Dila B", UnitLevelCode.Dila, "nat.z1.db");
    private static readonly IndicatorCalculator.UnitNode MuqamA1 = new(Guid.NewGuid(), DilaA.Id, "Muqam A1", UnitLevelCode.Muqam, "nat.z1.da.m1");
    private static readonly IndicatorCalculator.UnitNode MuqamA2 = new(Guid.NewGuid(), DilaA.Id, "Muqam A2", UnitLevelCode.Muqam, "nat.z1.da.m2");
    private static readonly IndicatorCalculator.UnitNode MuqamB1 = new(Guid.NewGuid(), DilaB.Id, "Muqam B1", UnitLevelCode.Muqam, "nat.z1.db.m1");
    private static readonly IndicatorCalculator.UnitNode Department = new(Guid.NewGuid(), National.Id, "Finance", UnitLevelCode.Department, "nat.fin");

    private static ReportMetric Metric(IndicatorCalculator.UnitNode unit, decimal value, string field = "members") =>
        ReportMetric.Create(Guid.NewGuid(), Guid.NewGuid(), Window, unit.Id, unit.Path, field, value, Now);

    private static IndicatorCalculator Calculator(IEnumerable<ReportMetric> metrics, IEnumerable<ReportFieldTarget>? targets = null) =>
        new([UnitLevelCode.Dila, UnitLevelCode.Muqam],
            [National, Zone, DilaA, DilaB, MuqamA1, MuqamA2, MuqamB1, Department],
            metrics,
            targets ?? []);

    [Fact]
    public void AFilingUnitUsesItsOwnReportNotItsChildren()
    {
        var calculator = Calculator([Metric(DilaA, 100), Metric(MuqamA1, 40), Metric(MuqamA2, 50)]);

        Assert.Equal(100, calculator.Value(DilaA, "members", AggregationRule.Sum, Window));
    }

    [Fact]
    public void ANonFilingUnitCombinesItsChildrenWithTheFieldsRule()
    {
        var calculator = Calculator([Metric(DilaA, 100), Metric(DilaB, 60)]);

        Assert.Equal(160, calculator.Value(Zone, "members", AggregationRule.Sum, Window));
        Assert.Equal(80, calculator.Value(National, "members", AggregationRule.Average, Window));
        Assert.Null(calculator.Value(Zone, "members", AggregationRule.None, Window));
    }

    [Fact]
    public void AFilingUnitWithoutAnAcknowledgedReportHasNoFigure()
    {
        var calculator = Calculator([Metric(DilaA, 100)]);

        Assert.Null(calculator.Value(DilaB, "members", AggregationRule.Sum, Window));
        Assert.Equal(100, calculator.Value(Zone, "members", AggregationRule.Sum, Window));
    }

    [Fact]
    public void TargetsAreTheLevelsTargetForFilersAndCombinedAbove()
    {
        var targets = new[] { ReportFieldTarget.Create(Guid.NewGuid(), Guid.NewGuid(), "members", UnitLevelCode.Dila, 120) };
        var calculator = Calculator([], targets);

        Assert.Equal(120, calculator.Target(DilaA, "members", AggregationRule.Sum));
        Assert.Equal(240, calculator.Target(Zone, "members", AggregationRule.Sum));
        Assert.Equal(120, calculator.Target(Zone, "members", AggregationRule.Average));
        Assert.Null(calculator.Target(Zone, "members", AggregationRule.Max));
        Assert.Null(calculator.Target(MuqamA1, "members", AggregationRule.Sum));
    }

    [Fact]
    public void ChildrenLeaveOutSubtreesWithNoFilers()
    {
        var calculator = Calculator([]);

        Assert.Equal([Zone], calculator.Children(National));
        Assert.Equal([DilaA, DilaB], calculator.Children(Zone));
    }

    [Theory]
    [InlineData(AggregationRule.Sum, 6.0)]
    [InlineData(AggregationRule.Average, 2.0)]
    [InlineData(AggregationRule.Min, 1.0)]
    [InlineData(AggregationRule.Max, 3.0)]
    public void CombineIgnoresMissingFigures(AggregationRule rule, double expected)
    {
        Assert.Equal(expected, IndicatorCalculator.Combine(rule, [1, null, 2, 3]));
    }

    [Fact]
    public void CombineHasNothingForLatestNoneOrNoFigures()
    {
        Assert.Null(IndicatorCalculator.Combine(AggregationRule.Latest, [1, 2]));
        Assert.Null(IndicatorCalculator.Combine(AggregationRule.Sum, [null, null]));
    }
}
