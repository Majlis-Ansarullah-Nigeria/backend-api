using FluentAssertions;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.Commands;
using ManagementApi.Application.Reports.DTOs;
using ManagementApi.Application.Reports.Queries;
using ManagementApi.Domain.Entities;
using ManagementApi.Domain.Entities.Reports;
using ManagementApi.Domain.Enums;
using ManagementApi.Domain.Identity;
using ManagementApi.Infrastructure.Persistence;
using ManagementApi.Shared.Authorization;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ManagementApi.Infrastructure.Tests.Reports;

public class ReportCommandsTests
{
    private readonly ApplicationDbContext _db;
    private readonly FixedClock _clock = new(new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc));
    private readonly Zone _zone = new("Lagos Zone", "Z01");
    private readonly Dila _dila;
    private readonly Muqam _muqam;
    private readonly Muqam _otherMuqam;
    private readonly ReportTemplate _template = new("Monthly Report", "Monthly", OrganizationLevel.Muqam);
    private readonly SubmissionWindow _window;

    public ReportCommandsTests()
    {
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        _dila = new Dila("Ikeja Dila", "D01", _zone.Id);
        _muqam = new Muqam("Agege Muqam", "M01", _dila.Id);
        _otherMuqam = new Muqam("Ojodu Muqam", "M02", _dila.Id);
        _window = new SubmissionWindow(_template.Id, "November 2026",
            new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 30, 23, 59, 0, DateTimeKind.Utc));

        _template.MarkPublished(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        _db.Zones.Add(_zone);
        _db.Dilas.Add(_dila);
        _db.Muqams.AddRange(_muqam, _otherMuqam);
        _db.ReportTemplates.Add(_template);
        _db.SubmissionWindows.Add(_window);
        _db.SaveChanges();
    }

    [Fact]
    public async Task Draft_then_submit_keeps_one_report_per_unit_even_when_the_head_changes()
    {
        var formerHead = MuqamHead(_muqam);
        var newHead = MuqamHead(_muqam);

        var draft = await SaveDraft(formerHead, "{\"q1\":\"a\"}");
        var submit = await Submit(newHead, "{\"q1\":\"b\"}");

        draft.Succeeded.Should().BeTrue();
        submit.Succeeded.Should().BeTrue(string.Join(", ", submit.Messages));
        submit.Data.Should().Be(draft.Data);

        var report = await _db.ReportSubmissions.SingleAsync();
        report.Status.Should().Be(SubmissionStatus.Submitted);
        report.OrganizationLevel.Should().Be(OrganizationLevel.Muqam);
        report.UnitId.Should().Be(_muqam.Id);
        report.DilaId.Should().Be(_dila.Id);
        report.ZoneId.Should().Be(_zone.Id);
        report.ResponseData.Should().Be("{\"q1\":\"b\"}");
    }

    [Fact]
    public async Task Each_unit_gets_its_own_report()
    {
        await Submit(MuqamHead(_muqam));
        await Submit(MuqamHead(_otherMuqam));

        (await _db.ReportSubmissions.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Submitting_twice_is_refused()
    {
        var head = MuqamHead(_muqam);
        await Submit(head);

        var again = await Submit(head);

        again.Succeeded.Should().BeFalse();
        again.Messages.Should().Contain(m => m.Contains("Withdraw"));
    }

    [Fact]
    public async Task Server_clock_refuses_submission_after_the_window_closes()
    {
        _clock.Now = _window.EndDate.AddMinutes(1);

        var result = await Submit(MuqamHead(_muqam));

        result.Succeeded.Should().BeFalse();
        result.Messages.Should().Contain(m => m.StartsWith("WINDOW_CLOSED"));
    }

    [Fact]
    public async Task Server_clock_refuses_submission_before_the_window_opens()
    {
        _clock.Now = _window.StartDate.AddMinutes(-1);

        var result = await SaveDraft(MuqamHead(_muqam));

        result.Messages.Should().Contain(m => m.StartsWith("WINDOW_NOT_OPEN"));
    }

    [Fact]
    public async Task Account_without_a_unit_cannot_submit()
    {
        var result = await Submit(User(OrganizationLevel.Muqam));

        result.Succeeded.Should().BeFalse();
        result.Messages.Should().Contain(m => m.Contains("not linked to a Muqam"));
    }

    [Fact]
    public async Task Template_must_match_the_window()
    {
        var other = new ReportTemplate("Other", "Monthly", OrganizationLevel.Muqam);
        _db.ReportTemplates.Add(other);
        await _db.SaveChangesAsync();

        var result = await Handlers.Submit(_db, MuqamHead(_muqam), _clock)
            .Handle(new SubmitReportCommand(new SubmitReportRequest
            {
                ReportTemplateId = other.Id,
                SubmissionWindowId = _window.Id,
                ResponseData = "{}"
            }), default);

        result.Succeeded.Should().BeFalse();
    }

    [Theory]
    [InlineData(OrganizationLevel.Dila, null)]
    [InlineData(OrganizationLevel.National, null)]
    [InlineData(OrganizationLevel.Muqam, Roles.ZaimAla)]
    public async Task Only_the_secretariat_can_acknowledge_or_return(OrganizationLevel level, string? role)
    {
        var id = (await Submit(MuqamHead(_muqam))).Data;
        var notSecretariat = User(level, dilaId: _dila.Id, role: role);

        var ack = await Review(notSecretariat).Handle(new AcknowledgeSubmissionCommand(new AcknowledgeSubmissionRequest { SubmissionId = id }), default);
        var ret = await Review(notSecretariat).Handle(new ReturnSubmissionCommand(new ReturnSubmissionRequest { SubmissionId = id, Reason = "x" }), default);

        ack.Messages.Should().ContainSingle(m => m.StartsWith("FORBIDDEN"));
        ret.Messages.Should().ContainSingle(m => m.StartsWith("FORBIDDEN"));
        (await _db.ReportSubmissions.SingleAsync()).Status.Should().Be(SubmissionStatus.Submitted);
    }

    [Fact]
    public async Task Secretariat_returns_then_unit_resubmits_after_the_window_closes_then_secretariat_acknowledges()
    {
        var head = MuqamHead(_muqam);
        var secretariat = User(OrganizationLevel.National, role: Roles.NationalAdmin);
        _clock.Now = _window.EndDate.AddDays(-1);
        var id = (await Submit(head)).Data;

        (await Review(secretariat).Handle(new ReturnSubmissionCommand(
            new ReturnSubmissionRequest { SubmissionId = id, Reason = "Attendance missing" }), default)).Succeeded.Should().BeTrue();

        _clock.Now = _window.EndDate.AddDays(2); // window closed, still inside the 7-day revision period
        var resubmit = await Submit(head, "{\"attendance\":40}");
        resubmit.Succeeded.Should().BeTrue(string.Join(", ", resubmit.Messages));

        (await Review(secretariat).Handle(new AcknowledgeSubmissionCommand(
            new AcknowledgeSubmissionRequest { SubmissionId = id }), default)).Succeeded.Should().BeTrue();

        var report = await _db.ReportSubmissions.Include(s => s.Approvals).SingleAsync();
        report.Status.Should().Be(SubmissionStatus.Acknowledged);
        report.Approvals.Select(a => a.Status).Should().Equal(ApprovalStatus.Returned, ApprovalStatus.Acknowledged);
    }

    [Fact]
    public async Task Submission_gets_a_reference_number_that_is_kept_on_resubmission()
    {
        var type = new ReportType("General Monthly Report", "GMR", ReportFrequency.Monthly);
        _db.ReportTypes.Add(type);
        _template.AssignReportType(type);
        await _db.SaveChangesAsync();
        var head = MuqamHead(_muqam);

        var id = (await Submit(head)).Data;
        var report = await _db.ReportSubmissions.SingleAsync();
        report.ReferenceNumber.Should().Be("GMR-2026-11-Z01D01M01");

        await Review(User(OrganizationLevel.National, role: Roles.NationalAdmin)).Handle(
            new ReturnSubmissionCommand(new ReturnSubmissionRequest { SubmissionId = id, Reason = "Fix" }), default);
        await Submit(head);

        (await _db.ReportSubmissions.SingleAsync()).ReferenceNumber.Should().Be("GMR-2026-11-Z01D01M01");
    }

    [Fact]
    public async Task Same_unit_and_period_in_a_second_window_gets_a_suffix()
    {
        var second = new SubmissionWindow(_template.Id, "November catch-up", _window.StartDate, _window.EndDate);
        _db.SubmissionWindows.Add(second);
        await _db.SaveChangesAsync();
        var head = MuqamHead(_muqam);

        await Submit(head);
        await Handlers.Submit(_db, head, _clock).Handle(new SubmitReportCommand(new SubmitReportRequest
        {
            ReportTemplateId = _template.Id,
            SubmissionWindowId = second.Id,
            ResponseData = "{}"
        }), default);

        var references = await _db.ReportSubmissions.Select(s => s.ReferenceNumber).ToListAsync();
        references.Should().BeEquivalentTo(new[] { "MON-2026-11-Z01D01M01", "MON-2026-11-Z01D01M01-2" });
    }

    [Fact]
    public async Task A_head_cannot_fill_a_report_meant_for_another_level()
    {
        var dilaHead = User(OrganizationLevel.Dila, dilaId: _dila.Id, zoneId: _zone.Id, role: Roles.NazimAla);

        var result = await Submit(dilaHead);

        result.Succeeded.Should().BeFalse();
        result.Messages.Should().Contain(m => m.Contains("isn't filled at Dila level"));
    }

    [Fact]
    public async Task A_template_filled_by_several_levels_accepts_each_level_once()
    {
        _template.SetSubmittingLevels(new[] { OrganizationLevel.Muqam, OrganizationLevel.Dila });
        await _db.SaveChangesAsync();

        (await Submit(MuqamHead(_muqam))).Succeeded.Should().BeTrue();
        (await Submit(User(OrganizationLevel.Dila, dilaId: _dila.Id, zoneId: _zone.Id, role: Roles.NazimAla))).Succeeded.Should().BeTrue();

        (await _db.ReportSubmissions.Select(s => s.OrganizationLevel).ToListAsync())
            .Should().BeEquivalentTo(new[] { OrganizationLevel.Muqam, OrganizationLevel.Dila });
    }

    [Fact]
    public async Task Observer_cannot_withdraw_a_child_report()
    {
        var id = (await Submit(MuqamHead(_muqam))).Data;
        var dilaHead = User(OrganizationLevel.Dila, dilaId: _dila.Id, zoneId: _zone.Id);

        var result = await Review(dilaHead).Handle(new WithdrawSubmissionCommand(id), default);

        result.Succeeded.Should().BeFalse();
        (await _db.ReportSubmissions.SingleAsync()).Status.Should().Be(SubmissionStatus.Submitted);
    }

    [Fact]
    public async Task Owner_can_withdraw_until_the_secretariat_opens_the_report()
    {
        var head = MuqamHead(_muqam);
        var id = (await Submit(head)).Data;

        (await Review(head).Handle(new WithdrawSubmissionCommand(id), default)).Succeeded.Should().BeTrue();
        await Submit(head);

        await Review(User(OrganizationLevel.National, role: Roles.SuperAdmin)).Handle(new StartReviewCommand(id), default);
        var late = await Review(head).Handle(new WithdrawSubmissionCommand(id), default);

        late.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Report_detail_is_hidden_outside_the_users_scope()
    {
        var id = (await Submit(MuqamHead(_muqam))).Data;
        var otherHead = MuqamHead(_otherMuqam);
        var dilaHead = User(OrganizationLevel.Dila, dilaId: _dila.Id, zoneId: _zone.Id);

        var hidden = await new GetSubmissionByIdQueryHandler(_db, otherHead).Handle(new GetSubmissionByIdQuery(id), default);
        var observed = await new GetSubmissionByIdQueryHandler(_db, dilaHead).Handle(new GetSubmissionByIdQuery(id), default);

        hidden.Succeeded.Should().BeFalse();
        observed.Succeeded.Should().BeTrue();
        observed.Data!.IsOwnUnit.Should().BeFalse();
        observed.Data.CanEdit.Should().BeFalse();
        observed.Data.CanReview.Should().BeFalse();
        observed.Data.MuqamName.Should().Be("Agege Muqam");
    }

    [Fact]
    public async Task Observers_cannot_read_or_write_review_comments_and_others_cannot_see_files()
    {
        var head = MuqamHead(_muqam);
        var id = (await Submit(head)).Data;
        var secretariat = User(OrganizationLevel.National, role: Roles.NationalAdmin);
        var dilaHead = User(OrganizationLevel.Dila, dilaId: _dila.Id, zoneId: _zone.Id);
        var otherHead = MuqamHead(_otherMuqam);

        var added = await new AddCommentCommandHandler(_db, secretariat, Mock.Of<INotificationService>())
            .Handle(new AddCommentCommand(new AddCommentRequest { SubmissionId = id, Content = "Please explain line 3" }), default);
        added.Succeeded.Should().BeTrue(string.Join(", ", added.Messages));

        (await Comments(head, id)).Succeeded.Should().BeTrue();
        (await Comments(dilaHead, id)).Succeeded.Should().BeFalse();
        (await new AddCommentCommandHandler(_db, dilaHead, Mock.Of<INotificationService>())
            .Handle(new AddCommentCommand(new AddCommentRequest { SubmissionId = id, Content = "x" }), default))
            .Succeeded.Should().BeFalse();

        (await new GetFileAttachmentsQueryHandler(_db, dilaHead).Handle(new GetFileAttachmentsQuery(id), default)).Succeeded.Should().BeTrue();
        (await new GetFileAttachmentsQueryHandler(_db, otherHead).Handle(new GetFileAttachmentsQuery(id), default)).Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task New_template_version_retires_the_old_one_and_existing_reports_keep_their_version()
    {
        var id = (await Submit(MuqamHead(_muqam))).Data;
        var secretariat = User(OrganizationLevel.National, role: Roles.NationalAdmin);
        var request = new CreateReportTemplateRequest
        {
            Name = "Monthly Report",
            OrganizationLevel = OrganizationLevel.Muqam,
            Sections = new()
            {
                new CreateReportSectionRequest
                {
                    Title = "Attendance",
                    Questions = new() { new CreateReportQuestionRequest { QuestionText = "How many attended?", QuestionType = "Number" } }
                }
            }
        };

        var denied = await new CreateTemplateVersionCommandHandler(_db, MuqamHead(_muqam))
            .Handle(new CreateTemplateVersionCommand(_template.Id, request), default);
        denied.Messages.Should().ContainSingle(m => m.StartsWith("FORBIDDEN"));

        var result = await new CreateTemplateVersionCommandHandler(_db, secretariat)
            .Handle(new CreateTemplateVersionCommand(_template.Id, request), default);

        result.Succeeded.Should().BeTrue(string.Join(", ", result.Messages));
        var v2 = await _db.ReportTemplates.SingleAsync(t => t.Id == result.Data);
        v2.Version.Should().Be(2);
        v2.RootTemplateId.Should().Be(_template.Id);
        (await _db.ReportTemplates.SingleAsync(t => t.Id == _template.Id)).IsActive.Should().BeFalse();
        (await _db.ReportSubmissions.SingleAsync(s => s.Id == id)).ReportTemplateId.Should().Be(_template.Id);

        var v3 = await new CreateTemplateVersionCommandHandler(_db, secretariat)
            .Handle(new CreateTemplateVersionCommand(v2.Id, request), default);
        (await _db.ReportTemplates.SingleAsync(t => t.Id == v3.Data)).Version.Should().Be(3);
    }

    [Fact]
    public async Task Closing_marks_drafts_and_unstarted_units_missed_except_extended_units_and_an_extension_reopens()
    {
        var third = new Muqam("Ifako Muqam", "M03", _dila.Id);
        _db.Muqams.Add(third);
        await _db.SaveChangesAsync();
        await SaveDraft(MuqamHead(_muqam));            // draft → missed
        await Submit(MuqamHead(_otherMuqam));          // submitted → untouched
        var window = await _db.SubmissionWindows.Include(w => w.Extensions).SingleAsync();
        window.GrantExtension(OrganizationLevel.Muqam, third.Id, window.EndDate.AddDays(2), window.EndDate.AddDays(3), "Flood");
        await _db.SaveChangesAsync();

        _clock.Now = _window.EndDate.AddHours(1);
        var jobs = new ReportWindowJobHandlers(_db, Mock.Of<INotificationService>(), _clock);
        var first = await jobs.Handle(new MarkMissedReportsCommand(), default);
        var again = await jobs.Handle(new MarkMissedReportsCommand(), default);

        first.Data.Should().Be(1);
        again.Data.Should().Be(0);
        var statuses = await _db.ReportSubmissions.ToDictionaryAsync(s => s.UnitId, s => s.Status);
        statuses[_muqam.Id].Should().Be(SubmissionStatus.Missed);
        statuses[_otherMuqam.Id].Should().Be(SubmissionStatus.Submitted);
        statuses.Should().NotContainKey(third.Id);

        _clock.Now = _window.EndDate.AddDays(4);   // extension over too
        (await jobs.Handle(new MarkMissedReportsCommand(), default)).Data.Should().Be(1);

        // The Secretariat extends the first Muqam: it can report again
        window.GrantExtension(OrganizationLevel.Muqam, _muqam.Id, _clock.Now.AddDays(1), _clock.Now.AddDays(2), "Late approval");
        await _db.SaveChangesAsync();
        var resubmit = await Submit(MuqamHead(_muqam));
        resubmit.Succeeded.Should().BeTrue(string.Join(", ", resubmit.Messages));
    }

    [Fact]
    public async Task Reminders_go_once_to_heads_of_units_that_have_not_submitted()
    {
        await Submit(MuqamHead(_otherMuqam));
        var pendingHead = new ApplicationUser { UserName = "agege", IsActive = true, OrganizationLevel = OrganizationLevel.Muqam, MuqamId = _muqam.Id };
        var doneHead = new ApplicationUser { UserName = "ojodu", IsActive = true, OrganizationLevel = OrganizationLevel.Muqam, MuqamId = _otherMuqam.Id };
        var someoneElse = new ApplicationUser { UserName = "member", IsActive = true, OrganizationLevel = OrganizationLevel.Muqam };
        _db.Users.AddRange(pendingHead, doneHead, someoneElse);
        await _db.SaveChangesAsync();

        var notifications = new Mock<INotificationService>();
        var recipients = new List<Guid>();
        notifications.Setup(n => n.NotifyDeadlineApproachingAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>(),
                It.IsAny<List<(Guid UserId, string UserName)>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string, DateTime, int, List<(Guid UserId, string UserName)>, CancellationToken>((_, _, _, _, users, _) => recipients.AddRange(users.Select(u => u.UserId)))
            .Returns(Task.CompletedTask);

        _clock.Now = _window.EndDate.AddDays(-1);   // 1 day before the due date
        var jobs = new ReportWindowJobHandlers(_db, notifications.Object, _clock);
        await jobs.Handle(new SendDueRemindersCommand(), default);
        await jobs.Handle(new SendDueRemindersCommand(), default);

        recipients.Should().Equal(pendingHead.Id);
    }

    private Task<Application.Common.Models.Result<PaginationResponse<SubmissionCommentDto>>> Comments(ICurrentUserService user, Guid id) =>
        new GetCommentsQueryHandler(_db, user).Handle(new GetCommentsQuery(id), default);

    private Task<Application.Common.Models.Result<Guid>> Submit(ICurrentUserService user, string data = "{}") =>
        Handlers.Submit(_db, user, _clock).Handle(new SubmitReportCommand(new SubmitReportRequest
        {
            ReportTemplateId = _template.Id,
            SubmissionWindowId = _window.Id,
            ResponseData = data
        }), default);

    private Task<Application.Common.Models.Result<Guid>> SaveDraft(ICurrentUserService user, string data = "{}") =>
        new SaveDraftCommandHandler(_db, user, _clock).Handle(new SaveDraftCommand(new SaveDraftRequest
        {
            ReportTemplateId = _template.Id,
            SubmissionWindowId = _window.Id,
            ResponseData = data
        }), default);

    private ReviewSubmissionCommandHandlers Review(ICurrentUserService user) =>
        new(_db, user, Mock.Of<INotificationService>(), _clock);

    private ICurrentUserService MuqamHead(Muqam muqam) =>
        User(OrganizationLevel.Muqam, muqamId: muqam.Id, dilaId: _dila.Id, zoneId: _zone.Id, role: Roles.ZaimAla);

    private static ICurrentUserService User(
        OrganizationLevel level,
        Guid? muqamId = null,
        Guid? dilaId = null,
        Guid? zoneId = null,
        string? role = null)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
        user.SetupGet(u => u.UserName).Returns("tester");
        user.SetupGet(u => u.ChandaNo).Returns("0001");
        user.SetupGet(u => u.OrganizationLevel).Returns(level);
        user.SetupGet(u => u.MuqamId).Returns(muqamId);
        user.SetupGet(u => u.DilaId).Returns(dilaId);
        user.SetupGet(u => u.ZoneId).Returns(zoneId);
        user.Setup(u => u.IsInRole(It.IsAny<string>())).Returns<string>(r => r == role);
        return user.Object;
    }

    private static class Handlers
    {
        public static SubmitReportCommandHandler Submit(ApplicationDbContext db, ICurrentUserService user, TimeProvider clock) =>
            new(db, user, clock);
    }

    private sealed class FixedClock : TimeProvider
    {
        public FixedClock(DateTime now) => Now = now;

        public DateTime Now { get; set; }

        public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);
    }
}
