using FluentAssertions;
using ManagementApi.Domain.Entities.Reports;
using ManagementApi.Domain.Enums;
using Xunit;

namespace ManagementApi.Application.Tests.Reports;

public class ReportSubmissionLifecycleTests
{
    private static readonly DateTime Now = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Reviewer = Guid.NewGuid();

    private static ReportSubmission NewMuqamReport() => new(
        reportTemplateId: Guid.NewGuid(),
        submissionWindowId: Guid.NewGuid(),
        organizationLevel: OrganizationLevel.Muqam,
        unitId: Guid.NewGuid(),
        muqamId: null,
        dilaId: Guid.NewGuid(),
        zoneId: Guid.NewGuid(),
        submitterId: Guid.NewGuid(),
        submitterChandaNo: "1234",
        submitterName: "Head",
        submitterEmail: null,
        responseData: "{}");

    [Fact]
    public void A_unit_report_needs_its_unit()
    {
        var act = () => new ReportSubmission(Guid.NewGuid(), Guid.NewGuid(), OrganizationLevel.Dila, Guid.Empty,
            null, null, null, Guid.NewGuid(), "1", "Head", null, "{}");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void New_report_is_an_editable_draft()
    {
        var report = NewMuqamReport();

        report.Status.Should().Be(SubmissionStatus.Draft);
        report.IsEditable.Should().BeTrue();
    }

    [Fact]
    public void Submit_records_time_and_lateness()
    {
        var report = NewMuqamReport();

        report.Submit(Now, isLate: true);

        report.Status.Should().Be(SubmissionStatus.Submitted);
        report.SubmittedAt.Should().Be(Now);
        report.IsLate.Should().BeTrue();
        report.IsEditable.Should().BeFalse();
    }

    [Fact]
    public void Submitted_report_cannot_be_edited_until_withdrawn()
    {
        var report = NewMuqamReport();
        report.Submit(Now, false);

        var edit = () => report.SaveDraft("{\"a\":1}", Guid.NewGuid(), "1", "Head", null);
        edit.Should().Throw<InvalidOperationException>().WithMessage("*Withdraw*");

        report.Withdraw();

        report.Status.Should().Be(SubmissionStatus.Draft);
        report.SubmittedAt.Should().BeNull();
        edit.Should().NotThrow();
    }

    [Fact]
    public void Withdraw_is_not_allowed_once_the_secretariat_opened_the_report()
    {
        var report = NewMuqamReport();
        report.Submit(Now, false);
        report.StartReview(Now.AddHours(1));

        report.Status.Should().Be(SubmissionStatus.InReview);
        var withdraw = () => report.Withdraw();
        withdraw.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Acknowledge_is_final_and_recorded_in_history()
    {
        var report = NewMuqamReport();
        report.Submit(Now, false);

        report.Acknowledge(Reviewer, "Secretary", Now.AddDays(1), "Well done");

        report.Status.Should().Be(SubmissionStatus.Acknowledged);
        report.AcknowledgedAt.Should().Be(Now.AddDays(1));
        report.Approvals.Should().ContainSingle(a => a.Status == ApprovalStatus.Acknowledged);
        report.IsEditable.Should().BeFalse();
    }

    [Fact]
    public void Return_requires_a_reason_and_sets_a_seven_day_revision_deadline()
    {
        var report = NewMuqamReport();
        report.Submit(Now, false);

        var noReason = () => report.Return(Reviewer, "Secretary", " ", Now);
        noReason.Should().Throw<InvalidOperationException>();

        report.Return(Reviewer, "Secretary", "Fill section B", Now);

        report.Status.Should().Be(SubmissionStatus.Returned);
        report.ReturnReason.Should().Be("Fill section B");
        report.RevisionDueAt.Should().Be(Now.AddDays(7));
        report.IsEditable.Should().BeTrue();
    }

    [Fact]
    public void Returned_report_can_be_resubmitted_before_the_revision_deadline_and_keeps_its_lateness()
    {
        var report = NewMuqamReport();
        report.Submit(Now, isLate: true);
        report.Return(Reviewer, "Secretary", "Fix totals", Now);

        report.Submit(Now.AddDays(3), isLate: false);

        report.Status.Should().Be(SubmissionStatus.Submitted);
        report.IsLate.Should().BeTrue();
        report.ReviewStartedAt.Should().BeNull();
    }

    [Fact]
    public void Returned_report_cannot_be_resubmitted_after_the_revision_deadline()
    {
        var report = NewMuqamReport();
        report.Submit(Now, false);
        report.Return(Reviewer, "Secretary", "Fix totals", Now);

        var resubmit = () => report.Submit(Now.AddDays(8), false);

        resubmit.Should().Throw<InvalidOperationException>().WithMessage("*revision deadline*");
    }

    [Theory]
    [InlineData(SubmissionStatus.Draft)]
    [InlineData(SubmissionStatus.Acknowledged)]
    [InlineData(SubmissionStatus.Returned)]
    public void Only_submitted_or_in_review_reports_can_be_acknowledged(SubmissionStatus status)
    {
        var report = InStatus(status);

        var act = () => report.Acknowledge(Reviewer, "Secretary", Now);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reopen_moves_an_acknowledged_report_back_to_review_with_a_reason()
    {
        var report = InStatus(SubmissionStatus.Acknowledged);

        report.Reopen(Reviewer, "Secretary", "Totals disputed", Now.AddDays(2));

        report.Status.Should().Be(SubmissionStatus.InReview);
        report.AcknowledgedAt.Should().BeNull();
        report.Approvals.Should().Contain(a => a.Status == ApprovalStatus.Reopened);
    }

    [Fact]
    public void MarkMissed_only_affects_unsubmitted_drafts()
    {
        var draft = NewMuqamReport();
        var submitted = InStatus(SubmissionStatus.Submitted);

        draft.MarkMissed();
        submitted.MarkMissed();

        draft.Status.Should().Be(SubmissionStatus.Missed);
        submitted.Status.Should().Be(SubmissionStatus.Submitted);
    }

    private static ReportSubmission InStatus(SubmissionStatus status)
    {
        var report = NewMuqamReport();
        if (status == SubmissionStatus.Draft)
        {
            return report;
        }

        report.Submit(Now, false);
        switch (status)
        {
            case SubmissionStatus.Acknowledged:
                report.Acknowledge(Reviewer, "Secretary", Now);
                break;
            case SubmissionStatus.Returned:
                report.Return(Reviewer, "Secretary", "Fix", Now);
                break;
        }

        return report;
    }
}
