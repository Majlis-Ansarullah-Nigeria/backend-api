using HRS.Domain.Submissions;
using HRS.Domain.Templates;

namespace HRS.Domain.UnitTests.Submissions;

public class ReportReferenceNumberTests
{
    private static readonly DateTimeOffset Fallback = new(2026, 10, 4, 10, 42, 0, TimeSpan.Zero);

    [Fact]
    public void GeneralMonthlyUsesGmrAndParsesTheMonthFromThePeriodLabel()
    {
        var reference = ReportReferenceNumber.Generate("GMR", ReportTypeCategory.GeneralMonthly, "September 2026", Fallback, "Z03-D12-M04");

        Assert.Equal("GMR-2026-09-Z03D12M04", reference);
    }

    [Fact]
    public void QuarterlyUsesQtrAndParsesTheQuarterFromThePeriodLabel()
    {
        var reference = ReportReferenceNumber.Generate("QTR", ReportTypeCategory.Quarterly, "Q3 2026", Fallback, "Z03-D12-M04");

        Assert.Equal("QTR-2026-Q3-Z03D12M04", reference);
    }

    [Fact]
    public void QuarterlyAcceptsTheYearFirstOrdering()
    {
        var reference = ReportReferenceNumber.Generate("QTR", ReportTypeCategory.Quarterly, "2026 Q3", Fallback, "Z03-D12-M04");

        Assert.Equal("QTR-2026-Q3-Z03D12M04", reference);
    }

    [Fact]
    public void AnnualUsesAnnAndTheYearAlone()
    {
        var reference = ReportReferenceNumber.Generate("ANN", ReportTypeCategory.Annual, "2026", Fallback, "Z03-D12-M04");

        Assert.Equal("ANN-2026-Z03D12M04", reference);
    }

    [Theory]
    [InlineData(ReportTypeCategory.AdHoc, "ADH")]
    [InlineData(ReportTypeCategory.Event, "EVT")]
    [InlineData(ReportTypeCategory.Survey, "SUR")]
    public void NonRecurringCategoriesUseTheFallbackDateAsAFullDate(ReportTypeCategory category, string typeCode)
    {
        var reference = ReportReferenceNumber.Generate(ReportReferenceNumber.CategoryCode(category), category, "Teachers' survey", Fallback, "Z03-D12-M04");

        Assert.Equal($"{typeCode}-2026-10-04-Z03D12M04", reference);
    }

    [Fact]
    public void FallsBackToTheSuppliedDateWhenTheMonthlyPeriodLabelCantBeParsed()
    {
        var reference = ReportReferenceNumber.Generate("GMR", ReportTypeCategory.GeneralMonthly, "not a real period", Fallback, "Z03-D12-M04");

        Assert.Equal("GMR-2026-10-Z03D12M04", reference);
    }

    [Fact]
    public void FallsBackToAQuarterComputedFromTheSuppliedDateWhenTheQuarterlyPeriodLabelCantBeParsed()
    {
        var reference = ReportReferenceNumber.Generate("QTR", ReportTypeCategory.Quarterly, "not a real period", Fallback, "Z03-D12-M04");

        Assert.Equal("QTR-2026-Q4-Z03D12M04", reference);
    }

    [Fact]
    public void StripsDashesFromTheUnitCode()
    {
        var reference = ReportReferenceNumber.Generate("GMR", ReportTypeCategory.GeneralMonthly, "September 2026", Fallback, "Z03-D12-M04");

        Assert.DoesNotContain('-', reference.Split('-', 4)[3]);
    }
}
