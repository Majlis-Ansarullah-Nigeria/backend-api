using FluentAssertions;
using ManagementApi.Domain.Entities.Reports;
using ManagementApi.Domain.Enums;
using Xunit;

namespace ManagementApi.Application.Tests.Reports;

public class ReportReferenceNumberTests
{
    private static readonly DateTime November = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(ReportFrequency.Monthly, "2026-11")]
    [InlineData(ReportFrequency.Quarterly, "2026-Q4")]
    [InlineData(ReportFrequency.Annual, "2026")]
    [InlineData(ReportFrequency.AdHoc, "2026-11")]
    public void Period_follows_the_frequency(ReportFrequency frequency, string expected)
    {
        ReportReferenceNumber.Period(frequency, November).Should().Be(expected);
    }

    [Fact]
    public void Reference_joins_type_period_and_unit_codes()
    {
        var reference = ReportReferenceNumber.Build("GMR", "2026-11",
            ReportReferenceNumber.UnitPart('Z', "Z-03", Guid.NewGuid()),
            ReportReferenceNumber.UnitPart('D', "d12", Guid.NewGuid()),
            ReportReferenceNumber.UnitPart('M', "M04", Guid.NewGuid()));

        reference.Should().Be("GMR-2026-11-Z03D12M04");
    }

    [Fact]
    public void Unit_without_a_code_gets_a_stable_code_from_its_id()
    {
        var id = Guid.Parse("abcd1234-0000-0000-0000-000000000000");

        ReportReferenceNumber.UnitPart('M', null, id).Should().Be("MABCD");
        ReportReferenceNumber.UnitPart('M', "  ", id).Should().Be("MABCD");
    }

    [Theory]
    [InlineData("gmr", "GMR")]
    [InlineData(" Q2 ", "Q2")]
    public void Type_code_is_normalised(string input, string expected)
    {
        new ReportType("General Monthly", input, ReportFrequency.Monthly).Code.Should().Be(expected);
    }

    [Theory]
    [InlineData("G")]
    [InlineData("1GM")]
    [InlineData("TOOLONG")]
    [InlineData("G-R")]
    public void Invalid_type_codes_are_rejected(string code)
    {
        var act = () => new ReportType("Name", code, ReportFrequency.Monthly);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Locked_code_cannot_change_but_the_name_can()
    {
        var type = new ReportType("General Monthly", "GMR", ReportFrequency.Monthly);

        var change = () => type.Update("General Monthly", "GEN", ReportFrequency.Monthly, null, codeLocked: true);
        change.Should().Throw<InvalidOperationException>();

        type.Update("General Monthly Report", "GMR", ReportFrequency.Monthly, null, codeLocked: true);
        type.Name.Should().Be("General Monthly Report");
    }
}
