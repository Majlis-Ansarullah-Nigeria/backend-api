using HRS.Domain.Organization;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;

namespace HRS.Domain.UnitTests.Templates;

/// <summary>FR-SUB-010: each report type's own reference code.</summary>
public class ReportTypeCodeTests
{
    private static ReportType Create(string code) => ReportType.Create(
        Guid.NewGuid(), "Dila Monthly Report", null, ReportTypeCategory.GeneralMonthly, ReportTypeFrequency.Monthly,
        [UnitLevelCode.Dila], UnitLevelCode.National, 1, false, false, code, DateTimeOffset.UtcNow);

    [Theory]
    [InlineData("GMR", true)]
    [InlineData("DMR2", true)]
    [InlineData("AB", true)]
    [InlineData("ABCDEF", true)]
    [InlineData("A", false)]
    [InlineData("ABCDEFG", false)]
    [InlineData("2GM", false)]
    [InlineData("GM-R", false)]
    [InlineData("gmr", false)]
    [InlineData(null, false)]
    public void ACodeIsTwoToSixCapitalLettersOrDigitsStartingWithALetter(string? code, bool valid) =>
        Assert.Equal(valid, ReportType.IsValidCode(code));

    [Fact]
    public void CodesAreTrimmedAndUpperCasedAndBadOnesRefused()
    {
        Assert.Equal("DMR", Create(" dmr ").Code);
        Assert.Throws<ArgumentException>(() => Create("D-M"));

        var reportType = Create("DMR");
        reportType.ChangeCode("dmr2");
        Assert.Equal("DMR2", reportType.Code);
    }

    [Fact]
    public void TwoMonthlyTypesForTheSameUnitAndMonthGetDifferentReferences()
    {
        var now = new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);

        var general = ReportReferenceNumber.Generate("GMR", ReportTypeCategory.GeneralMonthly, "September 2026", now, "Z03-D12");
        var dila = ReportReferenceNumber.Generate("DMR", ReportTypeCategory.GeneralMonthly, "September 2026", now, "Z03-D12");

        Assert.Equal(("GMR-2026-09-Z03D12", "DMR-2026-09-Z03D12"), (general, dila));
    }

    [Fact]
    public void ATakenReferenceGetsTheNextSuffix()
    {
        Assert.Equal("GMR-2026-09-Z03D12", ReportReferenceNumber.WithSuffix("GMR-2026-09-Z03D12", 1));
        Assert.Equal("GMR-2026-09-Z03D12-2", ReportReferenceNumber.WithSuffix("GMR-2026-09-Z03D12", 2));
    }
}
