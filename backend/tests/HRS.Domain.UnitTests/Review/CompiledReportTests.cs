using HRS.Domain.Review;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;

namespace HRS.Domain.UnitTests.Review;

public class CompiledReportTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void UpdatingTheNarrativeRecordsWhoAndWhen()
    {
        var report = CompiledReport.Create(Guid.NewGuid(), Guid.NewGuid());
        var userId = Guid.NewGuid();

        report.UpdateNarrative("Membership grew in every zone.", userId, Now);

        Assert.Equal(("Membership grew in every zone.", Now, userId), (report.Narrative, report.NarrativeUpdatedAt!.Value, report.NarrativeUpdatedByUserId!.Value));
    }

    [Fact]
    public void ANarrativeOverTheLimitIsRefused()
    {
        var report = CompiledReport.Create(Guid.NewGuid(), Guid.NewGuid());

        Assert.Throws<ArgumentException>(() => report.UpdateNarrative(new string('x', CompiledReport.MaxNarrativeLength + 1), Guid.NewGuid(), Now));
    }

    [Theory]
    [InlineData(ReportTypeCategory.GeneralMonthly, "September 2026", "NAT-GMR-2026-09")]
    [InlineData(ReportTypeCategory.Quarterly, "Q3 2026", "NAT-QTR-2026-Q3")]
    public void TheNationalReferenceNumberFollowsTheMockup(ReportTypeCategory category, string periodLabel, string expected)
    {
        Assert.Equal(expected, ReportReferenceNumber.GenerateNational(category, periodLabel, Now));
    }
}
