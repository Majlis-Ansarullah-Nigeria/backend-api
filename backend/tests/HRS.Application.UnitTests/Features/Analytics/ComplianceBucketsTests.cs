using HRS.Application.Features.Analytics;
using HRS.Application.Features.Submissions.MyReports;
using HRS.Domain.Submissions;

namespace HRS.Application.UnitTests.Features.Analytics;

public class ComplianceBucketsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static ReportInstance Submitted(bool isLate)
    {
        var instance = ReportInstance.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);
        instance.Save("{}");
        instance.Submit(isLate, "REF", isNil: false, Now);
        return instance;
    }

    [Theory]
    [InlineData(ReportLineStatus.Submitted)]
    [InlineData(ReportLineStatus.InReview)]
    [InlineData(ReportLineStatus.Returned)]
    [InlineData(ReportLineStatus.Acknowledged)]
    public void SubmittedStatusesAreOnTimeOrLateByTheInstancesFlag(ReportLineStatus status)
    {
        Assert.Equal(ComplianceBucket.OnTime, ComplianceBuckets.Classify(status, Submitted(isLate: false)));
        Assert.Equal(ComplianceBucket.Late, ComplianceBuckets.Classify(status, Submitted(isLate: true)));
    }

    [Theory]
    [InlineData(ReportLineStatus.SubmittedLate, ComplianceBucket.Late)]
    [InlineData(ReportLineStatus.Missed, ComplianceBucket.Missed)]
    [InlineData(ReportLineStatus.Draft, ComplianceBucket.Drafting)]
    [InlineData(ReportLineStatus.Overdue, ComplianceBucket.Drafting)]
    [InlineData(ReportLineStatus.NotStarted, ComplianceBucket.NotStarted)]
    [InlineData(ReportLineStatus.Upcoming, ComplianceBucket.Upcoming)]
    public void OtherStatusesMapToTheirBucket(ReportLineStatus status, ComplianceBucket expected)
    {
        Assert.Equal(expected, ComplianceBuckets.Classify(status, null));
    }

    [Fact]
    public void OnlyUnitsThatCanStillSubmitAreRemindable()
    {
        Assert.True(ComplianceBuckets.IsRemindable(ComplianceBucket.Drafting));
        Assert.True(ComplianceBuckets.IsRemindable(ComplianceBucket.NotStarted));
        Assert.False(ComplianceBuckets.IsRemindable(ComplianceBucket.Missed));
        Assert.False(ComplianceBuckets.IsRemindable(ComplianceBucket.Upcoming));
        Assert.False(ComplianceBuckets.IsRemindable(ComplianceBucket.OnTime));
    }
}
