using FluentAssertions;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Directory;
using ManagementApi.Application.Profile;
using ManagementApi.Domain.Entities;
using ManagementApi.Domain.Entities.Reports;
using ManagementApi.Domain.Enums;
using ManagementApi.Domain.Identity;
using ManagementApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ManagementApi.Infrastructure.Tests.Profile;

public class ProfileAndDirectoryTests
{
    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly Mock<IAccountRecoveryService> _accounts = new();
    private readonly Mock<INotificationService> _notifications = new();
    private readonly Muqam _ikeja = new("Ikeja", "M01", null);
    private readonly Muqam _yaba = new("Yaba", "M02", null);
    private readonly Member _musa = new("1234", "Bello", "Musa");
    private readonly Member _ali = new("5678", "Okoro", "Ali");
    private readonly Member _sule = new("9012", "Adamu", "Sule");
    private readonly ApplicationUser _user;
    private readonly ApplicationUser _head;

    public ProfileAndDirectoryTests()
    {
        _db.Muqams.AddRange(_ikeja, _yaba);
        _musa.UpdateBasicInfo("Bello", "Musa", null, new DateTime(1960, 1, 2), "musa@gmail.com", "08030000000", null, "1 Private Rd");
        _musa.AssignToMuqam(_ikeja.Id);
        _ali.AssignToMuqam(_ikeja.Id);
        _sule.AssignToMuqam(_yaba.Id);
        _db.Members.AddRange(_musa, _ali, _sule);
        _db.MemberPositions.Add(new MemberPosition(_ali.Id, "Za'im", OrganizationLevel.Muqam, _ikeja.Id, DateTime.UtcNow));
        _user = new ApplicationUser { UserName = "1234", ChandaNo = "1234", MemberId = _musa.Id, MuqamId = _ikeja.Id, OrganizationLevel = OrganizationLevel.Muqam, Email = "musa@gmail.com", IsActive = true };
        _head = new ApplicationUser { UserName = "5678", ChandaNo = "5678", MemberId = _ali.Id, MuqamId = _ikeja.Id, OrganizationLevel = OrganizationLevel.Muqam, IsActive = true };
        _db.Users.AddRange(_user, _head);
        _db.SaveChanges();
    }

    private ICurrentUserService Current()
    {
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.UserId).Returns(_user.Id);
        user.Setup(u => u.MuqamId).Returns(_ikeja.Id);
        user.Setup(u => u.IsAuthenticated).Returns(true);
        return user.Object;
    }

    private MyProfileHandlers Profile() => new(_db, Current(), _accounts.Object, _notifications.Object, TimeProvider.System);

    [Fact]
    public async Task Directory_lists_my_muqam_by_default_with_positions()
    {
        var result = await new SearchDirectoryQueryHandler(_db, Current()).Handle(new SearchDirectoryQuery(null, null, null, null, null), default);

        result.Data!.Items.Select(i => i.FullName).Should().BeEquivalentTo("Musa Bello", "Ali Okoro");
        result.Data.Items.Single(i => i.FullName == "Ali Okoro").Positions.Should().ContainSingle(p => p.Title == "Za'im" && p.UnitName == "Ikeja");
        result.Data.MyMuqamName.Should().Be("Ikeja");
    }

    [Fact]
    public async Task Directory_search_needs_three_letters_or_a_full_number_and_never_returns_private_fields()
    {
        var handler = new SearchDirectoryQueryHandler(_db, Current());

        (await handler.Handle(new SearchDirectoryQuery("ad", null, null, null, null), default)).Succeeded.Should().BeFalse();
        (await handler.Handle(new SearchDirectoryQuery("ada", null, null, null, null), default)).Data!.Items.Should().ContainSingle(i => i.FullName == "Sule Adamu");
        (await handler.Handle(new SearchDirectoryQuery("9012", null, null, null, null), default)).Data!.Items.Should().ContainSingle();

        typeof(DirectoryMemberDto).GetProperties().Select(p => p.Name)
            .Should().NotContain(new[] { "DateOfBirth", "PhoneNo", "Email", "Address", "ChandaNo" });
    }

    [Fact]
    public async Task Profile_shows_my_own_private_record()
    {
        var result = await Profile().Handle(new GetMyProfileQuery(), default);

        result.Data!.DateOfBirth.Should().Be(new DateTime(1960, 1, 2));
        result.Data.Address.Should().Be("1 Private Rd");
        result.Data.MuqamName.Should().Be("Ikeja");
    }

    [Fact]
    public async Task Email_change_needs_the_password_and_waits_for_the_secretariat()
    {
        _accounts.Setup(a => a.CheckPasswordAsync(_user.Id, "right")).ReturnsAsync(true);

        var wrong = await Profile().Handle(new ChangeMyEmailCommand("new@gmail.com", "wrong"), default);
        var ok = await Profile().Handle(new ChangeMyEmailCommand("new@gmail.com", "right"), default);

        wrong.Messages.Should().ContainSingle(m => m.StartsWith("WRONG_PASSWORD"));
        ok.Succeeded.Should().BeTrue();
        _db.Users.Single(u => u.Id == _user.Id).Email.Should().Be("musa@gmail.com");
        (await Profile().Handle(new GetMyProfileQuery(), default)).Data!.PendingEmail.Should().Be("new@gmail.com");

        (await Profile().Handle(new CancelMyEmailChangeCommand(), default)).Succeeded.Should().BeTrue();
        (await Profile().Handle(new GetMyProfileQuery(), default)).Data!.PendingEmail.Should().BeNull();
    }

    [Fact]
    public async Task Reported_mistakes_go_to_the_muqam_head()
    {
        await Profile().Handle(new ReportRecordMistakeCommand("Date of birth", "3 January 1960"), default);

        _notifications.Verify(n => n.SendNotificationAsync(
            NotificationType.RecordCorrection, _head.Id, It.IsAny<string>(), It.IsAny<string>(),
            It.Is<string>(m => m.Contains("3 January 1960")), It.IsAny<NotificationPriority>(),
            It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
