using HRS.Domain.Submissions;

namespace HRS.Domain.UnitTests.Submissions;

/// <summary>FR-HIS-006: revision numbering and the action recorded for each lifecycle step.</summary>
public class ReportRevisionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid Actor = Guid.NewGuid();

    private static ReportInstance CreateSubmittedInstance(string answersJson = """{"q1":"a"}""")
    {
        var instance = ReportInstance.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);
        instance.Save(answersJson);
        instance.Submit(isLate: false, "REF-1", isNil: false, Now, Actor);
        return instance;
    }

    [Fact]
    public void FirstSubmissionIsRevisionOneWithASnapshotOfTheAnswers()
    {
        var instance = CreateSubmittedInstance("""{"q1":"a"}""");

        var revision = instance.RecordSubmission(Actor, Now);

        Assert.Equal(1, revision.RevisionNo);
        Assert.Equal(ReportRevisionAction.Submitted, revision.Action);
        Assert.Equal("""{"q1":"a"}""", revision.AnswersJson);
        Assert.Equal(instance.Id, revision.ReportInstanceId);
        Assert.Equal(Actor, revision.ActorUserId);
        Assert.Equal(Now, revision.CreatedAt);
        Assert.Equal(1, instance.RevisionCount);
    }

    [Fact]
    public void WithdrawThenSubmitAgainRecordsWithdrawnThenResubmitted()
    {
        var instance = CreateSubmittedInstance();
        instance.RecordSubmission(Actor, Now);

        instance.Withdraw();
        var withdrawn = instance.RecordWithdrawal(Actor, Now.AddHours(1));
        instance.Save("""{"q1":"b"}""");
        instance.Submit(isLate: false, "REF-1", isNil: false, Now.AddHours(2), Actor);
        var resubmitted = instance.RecordSubmission(Actor, Now.AddHours(2));

        Assert.Equal((2, ReportRevisionAction.Withdrawn), (withdrawn.RevisionNo, withdrawn.Action));
        Assert.Equal((3, ReportRevisionAction.Resubmitted), (resubmitted.RevisionNo, resubmitted.Action));
        Assert.Equal("""{"q1":"b"}""", resubmitted.AnswersJson);
    }

    [Fact]
    public void ResubmissionAfterAReturnIsRecordedAsResubmitted()
    {
        var instance = CreateSubmittedInstance();
        instance.RecordSubmission(Actor, Now);
        instance.Open();
        instance.Return("Please fix the figures.", Now);
        instance.Submit(isLate: false, "REF-1", isNil: false, Now, Actor);

        var revision = instance.RecordSubmission(Actor, Now);

        Assert.Equal(ReportRevisionAction.Resubmitted, revision.Action);
        Assert.Equal(2, revision.RevisionNo);
    }

    [Fact]
    public void RecordingASubmissionForAnUnsubmittedReportThrows()
    {
        var instance = ReportInstance.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);
        instance.Save("{}");

        Assert.Throws<InvalidOperationException>(() => instance.RecordSubmission(Actor, Now));
        Assert.Equal(0, instance.RevisionCount);
    }

    [Fact]
    public void RecordingAWithdrawalForANeverSubmittedDraftThrows()
    {
        var instance = ReportInstance.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);
        instance.Save("{}");

        Assert.Throws<InvalidOperationException>(() => instance.RecordWithdrawal(Actor, Now));
    }

    [Fact]
    public void RecordingAWithdrawalWhileStillSubmittedThrows()
    {
        var instance = CreateSubmittedInstance();

        Assert.Throws<InvalidOperationException>(() => instance.RecordWithdrawal(Actor, Now));
    }
}
