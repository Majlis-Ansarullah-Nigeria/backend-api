using HRS.Domain.Submissions;

namespace HRS.Domain.UnitTests.Submissions;

public class ReportInstanceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static ReportInstance CreateInstance() =>
        ReportInstance.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);

    [Fact]
    public void CreateStartsAsNotStartedWithEmptyAnswers()
    {
        var instance = CreateInstance();

        Assert.Equal(ReportInstanceStatus.NotStarted, instance.Status);
        Assert.Equal("{}", instance.AnswersJson);
        Assert.Equal(Now, instance.CreatedAt);
    }

    [Fact]
    public void SaveFromNotStartedMovesToDraft()
    {
        var instance = CreateInstance();

        instance.Save("""{"q1":"a"}""");

        Assert.Equal(ReportInstanceStatus.Draft, instance.Status);
        Assert.Equal("""{"q1":"a"}""", instance.AnswersJson);
    }

    [Fact]
    public void SaveFromDraftUpdatesAnswersAndStaysDraft()
    {
        var instance = CreateInstance();
        instance.Save("""{"q1":"a"}""");

        instance.Save("""{"q1":"b"}""");

        Assert.Equal(ReportInstanceStatus.Draft, instance.Status);
        Assert.Equal("""{"q1":"b"}""", instance.AnswersJson);
    }

    [Fact]
    public void SaveFromReturnedUpdatesAnswersWithoutLeavingReturned()
    {
        var instance = CreateInstance();
        instance.Save("""{"q1":"a"}""");
        instance.Submit(isLate: false, "REF-TEST", false, Now);
        instance.Open();
        instance.Return("Please fix figure 2", Now);

        instance.Save("""{"q1":"c"}""");

        Assert.Equal(ReportInstanceStatus.Returned, instance.Status);
        Assert.Equal("""{"q1":"c"}""", instance.AnswersJson);
    }

    [Theory]
    [InlineData(ReportInstanceStatus.Submitted)]
    [InlineData(ReportInstanceStatus.InReview)]
    [InlineData(ReportInstanceStatus.Acknowledged)]
    [InlineData(ReportInstanceStatus.Missed)]
    public void SaveThrowsWhenLocked(ReportInstanceStatus status)
    {
        var instance = Move(CreateInstance(), status);

        Assert.Throws<InvalidOperationException>(() => instance.Save("""{"q1":"x"}"""));
    }

    [Fact]
    public void SubmitFromDraftSetsSubmittedAtIsLateAndReferenceNumber()
    {
        var instance = CreateInstance();
        instance.Save("""{"q1":"a"}""");

        instance.Submit(isLate: true, "GMR-2026-09-Z03D12M04", false, Now);

        Assert.Equal(ReportInstanceStatus.Submitted, instance.Status);
        Assert.Equal(Now, instance.SubmittedAt);
        Assert.True(instance.IsLate);
        Assert.Equal("GMR-2026-09-Z03D12M04", instance.ReferenceNumber);
    }

    [Fact]
    public void SubmitWithIsNilSetsTheNilFlag()
    {
        var instance = CreateInstance();
        instance.Save("{}");

        instance.Submit(isLate: false, "GMR-2026-09-Z03D12M04", isNil: true, Now);

        Assert.True(instance.IsNil);
    }

    [Fact]
    public void SubmitWithoutIsNilLeavesTheNilFlagFalse()
    {
        var instance = CreateInstance();
        instance.Save("""{"q1":"a"}""");

        instance.Submit(isLate: false, "GMR-2026-09-Z03D12M04", isNil: false, Now);

        Assert.False(instance.IsNil);
    }

    [Fact]
    public void ResubmitFromReturnedClearsReturnCommentAndRevisionDeadlineButKeepsTheOriginalReferenceNumber()
    {
        var instance = CreateInstance();
        instance.Save("""{"q1":"a"}""");
        instance.Submit(isLate: false, "GMR-2026-09-Z03D12M04", false, Now);
        instance.Open();
        instance.Return("Please fix figure 2", Now);

        instance.Submit(isLate: false, "GMR-2026-10-Z03D12M04", false, Now.AddDays(1));

        Assert.Equal(ReportInstanceStatus.Submitted, instance.Status);
        Assert.Null(instance.ReturnComment);
        Assert.Null(instance.RevisionDeadline);
        Assert.Equal("GMR-2026-09-Z03D12M04", instance.ReferenceNumber);
    }

    [Theory]
    [InlineData(ReportInstanceStatus.NotStarted)]
    [InlineData(ReportInstanceStatus.Submitted)]
    [InlineData(ReportInstanceStatus.InReview)]
    [InlineData(ReportInstanceStatus.Acknowledged)]
    [InlineData(ReportInstanceStatus.Missed)]
    public void SubmitThrowsFromAnyStateOtherThanDraftOrReturned(ReportInstanceStatus status)
    {
        var instance = Move(CreateInstance(), status);

        Assert.Throws<InvalidOperationException>(() => instance.Submit(isLate: false, "REF-TEST", false, Now));
    }

    [Fact]
    public void OpenFromSubmittedMovesToInReview()
    {
        var instance = CreateInstance();
        instance.Save("""{"q1":"a"}""");
        instance.Submit(isLate: false, "REF-TEST", false, Now);

        instance.Open();

        Assert.Equal(ReportInstanceStatus.InReview, instance.Status);
    }

    [Fact]
    public void OpenThrowsWhenNotSubmitted()
    {
        var instance = CreateInstance();

        Assert.Throws<InvalidOperationException>(instance.Open);
    }

    [Fact]
    public void WithdrawFromSubmittedMovesBackToDraft()
    {
        var instance = CreateInstance();
        instance.Save("""{"q1":"a"}""");
        instance.Submit(isLate: false, "REF-TEST", false, Now);

        instance.Withdraw();

        Assert.Equal(ReportInstanceStatus.Draft, instance.Status);
    }

    [Fact]
    public void WithdrawThrowsOnceInReview()
    {
        var instance = CreateInstance();
        instance.Save("""{"q1":"a"}""");
        instance.Submit(isLate: false, "REF-TEST", false, Now);
        instance.Open();

        Assert.Throws<InvalidOperationException>(instance.Withdraw);
    }

    [Fact]
    public void AcknowledgeFromInReviewSetsAcknowledgedAt()
    {
        var instance = CreateInstance();
        instance.Save("""{"q1":"a"}""");
        instance.Submit(isLate: false, "REF-TEST", false, Now);
        instance.Open();

        instance.Acknowledge(Now.AddHours(1));

        Assert.Equal(ReportInstanceStatus.Acknowledged, instance.Status);
        Assert.Equal(Now.AddHours(1), instance.AcknowledgedAt);
    }

    [Fact]
    public void AcknowledgeThrowsWhenNotInReview()
    {
        var instance = CreateInstance();
        instance.Save("""{"q1":"a"}""");
        instance.Submit(isLate: false, "REF-TEST", false, Now);

        Assert.Throws<InvalidOperationException>(() => instance.Acknowledge(Now));
    }

    [Fact]
    public void ReturnFromInReviewDefaultsRevisionDeadlineToSevenDaysOut()
    {
        var instance = CreateInstance();
        instance.Save("""{"q1":"a"}""");
        instance.Submit(isLate: false, "REF-TEST", false, Now);
        instance.Open();

        instance.Return("Please fix figure 2", Now);

        Assert.Equal(ReportInstanceStatus.Returned, instance.Status);
        Assert.Equal("Please fix figure 2", instance.ReturnComment);
        Assert.Equal(Now.AddDays(7), instance.RevisionDeadline);
    }

    [Fact]
    public void ReturnAcceptsAnOverriddenRevisionDeadline()
    {
        var instance = CreateInstance();
        instance.Save("""{"q1":"a"}""");
        instance.Submit(isLate: false, "REF-TEST", false, Now);
        instance.Open();

        instance.Return("Please fix figure 2", Now, Now.AddDays(14));

        Assert.Equal(Now.AddDays(14), instance.RevisionDeadline);
    }

    [Fact]
    public void ReturnThrowsWhenNotInReview()
    {
        var instance = CreateInstance();

        Assert.Throws<InvalidOperationException>(() => instance.Return("Reason", Now));
    }

    [Theory]
    [InlineData(ReportInstanceStatus.NotStarted)]
    [InlineData(ReportInstanceStatus.Draft)]
    public void MarkMissedSucceedsFromNotStartedOrDraft(ReportInstanceStatus status)
    {
        var instance = Move(CreateInstance(), status);

        instance.MarkMissed();

        Assert.Equal(ReportInstanceStatus.Missed, instance.Status);
    }

    [Fact]
    public void MarkMissedKeepsTheDraftAnswersForReference()
    {
        var instance = CreateInstance();
        instance.Save("""{"q1":"a"}""");

        instance.MarkMissed();

        Assert.Equal("""{"q1":"a"}""", instance.AnswersJson);
    }

    [Fact]
    public void MarkMissedThrowsOnceSubmitted()
    {
        var instance = CreateInstance();
        instance.Save("""{"q1":"a"}""");
        instance.Submit(isLate: false, "REF-TEST", false, Now);

        Assert.Throws<InvalidOperationException>(instance.MarkMissed);
    }

    [Theory]
    [InlineData(ReportInstanceStatus.Missed)]
    [InlineData(ReportInstanceStatus.Acknowledged)]
    public void ReopenMovesBackToDraftAndStampsReopenedAt(ReportInstanceStatus status)
    {
        var instance = Move(CreateInstance(), status);

        instance.Reopen(Now.AddDays(2));

        Assert.Equal(ReportInstanceStatus.Draft, instance.Status);
        Assert.Equal(Now.AddDays(2), instance.ReopenedAt);
    }

    [Theory]
    [InlineData(ReportInstanceStatus.NotStarted)]
    [InlineData(ReportInstanceStatus.Draft)]
    [InlineData(ReportInstanceStatus.Submitted)]
    [InlineData(ReportInstanceStatus.InReview)]
    [InlineData(ReportInstanceStatus.Returned)]
    public void ReopenThrowsFromAnyStateOtherThanMissedOrAcknowledged(ReportInstanceStatus status)
    {
        var instance = Move(CreateInstance(), status);

        Assert.Throws<InvalidOperationException>(() => instance.Reopen(Now));
    }

    [Theory]
    [InlineData(ReportInstanceStatus.NotStarted, true)]
    [InlineData(ReportInstanceStatus.Draft, true)]
    [InlineData(ReportInstanceStatus.Submitted, false)]
    [InlineData(ReportInstanceStatus.InReview, false)]
    [InlineData(ReportInstanceStatus.Returned, false)]
    [InlineData(ReportInstanceStatus.Acknowledged, false)]
    [InlineData(ReportInstanceStatus.Missed, false)]
    public void IsOverdueIsTrueOnlyForNotStartedOrDraftPastDue(ReportInstanceStatus status, bool expected)
    {
        var instance = Move(CreateInstance(), status);

        Assert.Equal(expected, instance.IsOverdue(Now.AddDays(1), Now));
    }

    [Fact]
    public void IsOverdueIsFalseBeforeTheDueDateEvenInDraft()
    {
        var instance = CreateInstance();
        instance.Save("""{"q1":"a"}""");

        Assert.False(instance.IsOverdue(Now, Now.AddDays(1)));
    }

    /// <summary>Drives a fresh instance through whatever real transitions reach the target status,
    /// so every test exercises the actual state machine rather than asserting on contrived state.</summary>
    private static ReportInstance Move(ReportInstance instance, ReportInstanceStatus target)
    {
        switch (target)
        {
            case ReportInstanceStatus.NotStarted:
                return instance;
            case ReportInstanceStatus.Draft:
                instance.Save("""{"q1":"a"}""");
                return instance;
            case ReportInstanceStatus.Submitted:
                instance.Save("""{"q1":"a"}""");
                instance.Submit(isLate: false, "REF-TEST", false, Now);
                return instance;
            case ReportInstanceStatus.InReview:
                instance.Save("""{"q1":"a"}""");
                instance.Submit(isLate: false, "REF-TEST", false, Now);
                instance.Open();
                return instance;
            case ReportInstanceStatus.Returned:
                instance.Save("""{"q1":"a"}""");
                instance.Submit(isLate: false, "REF-TEST", false, Now);
                instance.Open();
                instance.Return("Reason", Now);
                return instance;
            case ReportInstanceStatus.Acknowledged:
                instance.Save("""{"q1":"a"}""");
                instance.Submit(isLate: false, "REF-TEST", false, Now);
                instance.Open();
                instance.Acknowledge(Now);
                return instance;
            case ReportInstanceStatus.Missed:
                instance.Save("""{"q1":"a"}""");
                instance.MarkMissed();
                return instance;
            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, "Unhandled status.");
        }
    }
}
