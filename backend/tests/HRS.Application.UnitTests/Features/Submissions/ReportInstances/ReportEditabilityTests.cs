using HRS.Application.Features.Submissions.ReportInstances;
using HRS.Domain.Organization;
using HRS.Domain.Submissions;
using HRS.Domain.Windows;

namespace HRS.Application.UnitTests.Features.Submissions.ReportInstances;

public class ReportEditabilityTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid UnitId = Guid.NewGuid();

    private static SubmissionWindow CreateWindow(DateTimeOffset opensAt, int graceDays, DateTimeOffset closesAt, DateTimeOffset dueAt) =>
        SubmissionWindow.Create(
            Guid.NewGuid(), Guid.NewGuid(), "October 2026", opensAt, graceDays, closesAt,
            [new WindowLevelDueDate(UnitLevelCode.Muqam, dueAt)], Now);

    private static ReportInstance DraftInstance()
    {
        var instance = ReportInstance.Create(Guid.NewGuid(), Guid.NewGuid(), UnitId, Guid.NewGuid(), Now);
        instance.Save("""{"q1":"a"}""");
        return instance;
    }

    [Fact]
    public void NoInstanceBeforeWindowOpensIsNotEditable()
    {
        var window = CreateWindow(Now.AddDays(5), 3, Now.AddDays(30), Now.AddDays(15));

        var error = ReportEditability.Check(window, null, UnitLevelCode.Muqam, UnitId, Now);

        Assert.Equal("WINDOW_NOT_OPEN", error?.Code);
    }

    [Fact]
    public void NoInstanceWhileWindowIsOpenIsEditable()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(30), Now.AddDays(10));

        var error = ReportEditability.Check(window, null, UnitLevelCode.Muqam, UnitId, Now.AddDays(5));

        Assert.Null(error);
    }

    [Fact]
    public void NoInstanceAfterWindowClosesIsNotEditable()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(12), Now.AddDays(10));

        var error = ReportEditability.Check(window, null, UnitLevelCode.Muqam, UnitId, Now.AddDays(13));

        Assert.Equal("WINDOW_CLOSED", error?.Code);
    }

    [Fact]
    public void DraftWhileWindowIsOnTimeIsEditable()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(30), Now.AddDays(10));
        var instance = DraftInstance();

        var error = ReportEditability.Check(window, instance, UnitLevelCode.Muqam, UnitId, Now.AddDays(5));

        Assert.Null(error);
    }

    [Fact]
    public void DraftWithinGraceIsStillEditable()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(30), Now.AddDays(10));
        var instance = DraftInstance();

        var error = ReportEditability.Check(window, instance, UnitLevelCode.Muqam, UnitId, Now.AddDays(11));

        Assert.Null(error);
    }

    [Fact]
    public void DraftAfterWindowClosesIsNotEditable()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(12), Now.AddDays(10));
        var instance = DraftInstance();

        var error = ReportEditability.Check(window, instance, UnitLevelCode.Muqam, UnitId, Now.AddDays(13));

        Assert.Equal("WINDOW_CLOSED", error?.Code);
    }

    [Fact]
    public void ReturnedWithinItsRevisionDeadlineIsEditableEvenAfterTheWindowCloses()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(12), Now.AddDays(10));
        var instance = DraftInstance();
        instance.Submit(isLate: false, "REF-TEST", Now);
        instance.Open();
        instance.Return("Please fix figure 2", Now.AddDays(13), Now.AddDays(20));

        var error = ReportEditability.Check(window, instance, UnitLevelCode.Muqam, UnitId, Now.AddDays(14));

        Assert.Null(error);
    }

    [Fact]
    public void ReturnedPastItsRevisionDeadlineIsNotEditable()
    {
        var window = CreateWindow(Now, 3, Now.AddDays(12), Now.AddDays(10));
        var instance = DraftInstance();
        instance.Submit(isLate: false, "REF-TEST", Now);
        instance.Open();
        instance.Return("Please fix figure 2", Now.AddDays(13), Now.AddDays(20));

        var error = ReportEditability.Check(window, instance, UnitLevelCode.Muqam, UnitId, Now.AddDays(21));

        Assert.Equal("WINDOW_CLOSED", error?.Code);
    }

    [Theory]
    [InlineData(ReportInstanceStatus.Submitted)]
    [InlineData(ReportInstanceStatus.InReview)]
    [InlineData(ReportInstanceStatus.Acknowledged)]
    [InlineData(ReportInstanceStatus.Missed)]
    public void LockedStatusesAreNeverEditableRegardlessOfWindowState(ReportInstanceStatus status)
    {
        var window = CreateWindow(Now, 3, Now.AddDays(30), Now.AddDays(10));
        var instance = Move(status);

        var error = ReportEditability.Check(window, instance, UnitLevelCode.Muqam, UnitId, Now.AddDays(5));

        Assert.Equal("REPORT_LOCKED", error?.Code);
    }

    private static ReportInstance Move(ReportInstanceStatus target)
    {
        var instance = ReportInstance.Create(Guid.NewGuid(), Guid.NewGuid(), UnitId, Guid.NewGuid(), Now);

        if (target == ReportInstanceStatus.Missed)
        {
            instance.Save("""{"q1":"a"}""");
            instance.MarkMissed();
            return instance;
        }

        instance.Save("""{"q1":"a"}""");
        instance.Submit(isLate: false, "REF-TEST", Now);
        if (target == ReportInstanceStatus.Submitted)
        {
            return instance;
        }

        instance.Open();
        if (target == ReportInstanceStatus.InReview)
        {
            return instance;
        }

        instance.Acknowledge(Now);
        return instance;
    }
}
