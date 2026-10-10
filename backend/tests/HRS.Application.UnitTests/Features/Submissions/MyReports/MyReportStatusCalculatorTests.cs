using HRS.Application.Features.Submissions.MyReports;
using HRS.Domain.Organization;
using HRS.Domain.Submissions;
using HRS.Domain.Windows;

namespace HRS.Application.UnitTests.Features.Submissions.MyReports;

public class MyReportStatusCalculatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid UnitId = Guid.NewGuid();

    private static SubmissionWindow CreateWindow(DateTimeOffset opensAt, int graceDays, DateTimeOffset closesAt, DateTimeOffset dueAt) =>
        SubmissionWindow.Create(
            Guid.NewGuid(), Guid.NewGuid(), "October 2026", opensAt, graceDays, closesAt,
            [new WindowLevelDueDate(UnitLevelCode.Muqam, dueAt)], Now);

    private static ReportInstance DraftInstance() =>
        CreateAndSave(ReportInstanceStatus.Draft);

    private static ReportInstance CreateAndSave(ReportInstanceStatus target, bool isLate = false)
    {
        var instance = ReportInstance.Create(Guid.NewGuid(), Guid.NewGuid(), UnitId, Guid.NewGuid(), Now);
        if (target == ReportInstanceStatus.NotStarted)
        {
            return instance;
        }

        instance.Save("""{"q1":"a"}""");
        if (target == ReportInstanceStatus.Draft)
        {
            return instance;
        }

        instance.Submit(isLate, "REF-TEST", false, Now);
        if (target == ReportInstanceStatus.Submitted)
        {
            return instance;
        }

        instance.Open();
        if (target == ReportInstanceStatus.InReview)
        {
            return instance;
        }

        if (target == ReportInstanceStatus.Returned)
        {
            instance.Return("Please fix figure 2", Now);
            return instance;
        }

        instance.Acknowledge(Now);
        return instance;
    }

    [Fact]
    public void NoInstanceBeforeWindowOpensIsUpcoming()
    {
        var window = CreateWindow(Now.AddDays(5), 3, Now.AddDays(30), Now.AddDays(15));

        var status = MyReportStatusCalculator.Determine(window, null, UnitLevelCode.Muqam, UnitId, Now);

        Assert.Equal(ReportLineStatus.Upcoming, status);
    }

    [Fact]
    public void NoInstanceWhileWindowIsOpenIsNotStarted()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(30), Now.AddDays(10));

        var status = MyReportStatusCalculator.Determine(window, null, UnitLevelCode.Muqam, UnitId, Now.AddDays(5));

        Assert.Equal(ReportLineStatus.NotStarted, status);
    }

    [Fact]
    public void NoInstanceWhileWindowIsInGraceIsStillNotStarted()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(30), Now.AddDays(10));

        var status = MyReportStatusCalculator.Determine(window, null, UnitLevelCode.Muqam, UnitId, Now.AddDays(11));

        Assert.Equal(ReportLineStatus.NotStarted, status);
    }

    [Fact]
    public void NoInstanceAfterWindowClosesIsMissed()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(12), Now.AddDays(10));

        var status = MyReportStatusCalculator.Determine(window, null, UnitLevelCode.Muqam, UnitId, Now.AddDays(13));

        Assert.Equal(ReportLineStatus.Missed, status);
    }

    [Fact]
    public void AFreshlyCreatedNotStartedInstanceIsTreatedTheSameAsNoInstance()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(12), Now.AddDays(10));
        var instance = CreateAndSave(ReportInstanceStatus.NotStarted);

        var status = MyReportStatusCalculator.Determine(window, instance, UnitLevelCode.Muqam, UnitId, Now.AddDays(13));

        Assert.Equal(ReportLineStatus.Missed, status);
    }

    [Fact]
    public void DraftWhileWindowIsOpenIsDraft()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(30), Now.AddDays(10));
        var instance = DraftInstance();

        var status = MyReportStatusCalculator.Determine(window, instance, UnitLevelCode.Muqam, UnitId, Now.AddDays(5));

        Assert.Equal(ReportLineStatus.Draft, status);
    }

    [Fact]
    public void DraftWhileWindowIsInGraceIsOverdue()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(30), Now.AddDays(10));
        var instance = DraftInstance();

        var status = MyReportStatusCalculator.Determine(window, instance, UnitLevelCode.Muqam, UnitId, Now.AddDays(11));

        Assert.Equal(ReportLineStatus.Overdue, status);
    }

    [Fact]
    public void DraftAfterWindowClosesIsMissed()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(12), Now.AddDays(10));
        var instance = DraftInstance();

        var status = MyReportStatusCalculator.Determine(window, instance, UnitLevelCode.Muqam, UnitId, Now.AddDays(13));

        Assert.Equal(ReportLineStatus.Missed, status);
    }

    [Fact]
    public void SubmittedOnTimeIsSubmitted()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(30), Now.AddDays(10));
        var instance = CreateAndSave(ReportInstanceStatus.Submitted, isLate: false);

        var status = MyReportStatusCalculator.Determine(window, instance, UnitLevelCode.Muqam, UnitId, Now.AddDays(5));

        Assert.Equal(ReportLineStatus.Submitted, status);
    }

    [Fact]
    public void SubmittedLateIsSubmittedLate()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(30), Now.AddDays(10));
        var instance = CreateAndSave(ReportInstanceStatus.Submitted, isLate: true);

        var status = MyReportStatusCalculator.Determine(window, instance, UnitLevelCode.Muqam, UnitId, Now.AddDays(11));

        Assert.Equal(ReportLineStatus.SubmittedLate, status);
    }

    [Fact]
    public void InReviewMapsStraightThrough()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(30), Now.AddDays(10));
        var instance = CreateAndSave(ReportInstanceStatus.InReview);

        var status = MyReportStatusCalculator.Determine(window, instance, UnitLevelCode.Muqam, UnitId, Now.AddDays(5));

        Assert.Equal(ReportLineStatus.InReview, status);
    }

    [Fact]
    public void ReturnedStaysReturnedEvenAfterTheWindowHasClosed()
    {
        // BR-WIN-06: a returned report can be resubmitted after hard close until its revision
        // deadline — the closed window must never downgrade this to Missed.
        var window = CreateWindow(Now, 3, Now.AddDays(12), Now.AddDays(10));
        var instance = CreateAndSave(ReportInstanceStatus.Returned);

        var status = MyReportStatusCalculator.Determine(window, instance, UnitLevelCode.Muqam, UnitId, Now.AddDays(13));

        Assert.Equal(ReportLineStatus.Returned, status);
    }

    [Fact]
    public void AcknowledgedMapsStraightThrough()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(30), Now.AddDays(10));
        var instance = CreateAndSave(ReportInstanceStatus.Acknowledged);

        var status = MyReportStatusCalculator.Determine(window, instance, UnitLevelCode.Muqam, UnitId, Now.AddDays(5));

        Assert.Equal(ReportLineStatus.Acknowledged, status);
    }

    [Fact]
    public void MissedInstanceMapsStraightThrough()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(12), Now.AddDays(10));
        var instance = ReportInstance.Create(Guid.NewGuid(), Guid.NewGuid(), UnitId, Guid.NewGuid(), Now);
        instance.Save("""{"q1":"a"}""");
        instance.MarkMissed();

        var status = MyReportStatusCalculator.Determine(window, instance, UnitLevelCode.Muqam, UnitId, Now.AddDays(13));

        Assert.Equal(ReportLineStatus.Missed, status);
    }
}
