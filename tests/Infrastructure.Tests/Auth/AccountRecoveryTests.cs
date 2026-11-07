using FluentAssertions;
using ManagementApi.Application.Auth.Recovery;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Domain.Enums;
using ManagementApi.Domain.Identity;
using ManagementApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Roles = ManagementApi.Shared.Authorization.Roles;

namespace ManagementApi.Infrastructure.Tests.Auth;

public class AccountRecoveryTests
{
    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly Mock<IAccountRecoveryService> _recovery = new();
    private readonly List<(string To, string Subject)> _sent = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly ApplicationUser _member = new()
    {
        UserName = "1234", ChandaNo = "1234", Email = "adeyemi@gmail.com", FirstName = "Musa", LastName = "Bello", IsActive = true
    };

    public AccountRecoveryTests()
    {
        _db.Users.Add(_member);
        _db.SaveChanges();
        _recovery.Setup(r => r.CreatePasswordResetLinkAsync(It.IsAny<ApplicationUser>())).ReturnsAsync("http://portal/reset-password?id=x&token=y");
        _recovery.Setup(r => r.ChangeEmailAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .Callback<ApplicationUser, string>((u, e) => u.Email = e)
            .ReturnsAsync(Result.Success());
        _email.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, string, string?, CancellationToken>((to, _, subject, _, _, _) => _sent.Add((to, subject)))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public void Emails_are_masked()
    {
        AccountRecoveryHandlers.MaskEmail("adeyemi@gmail.com").Should().Be("a••••••@g••••.com");
        AccountRecoveryHandlers.MaskEmail("a@b.ng").Should().Be("a•••@b•••.ng");
    }

    [Theory]
    [InlineData("1234@temp.local", false)]
    [InlineData("not-an-email", false)]
    [InlineData("musa@gmail.com", true)]
    public void Placeholder_and_invalid_emails_are_not_usable(string email, bool usable)
    {
        AccountRecoveryHandlers.IsUsableEmail(email).Should().Be(usable);
    }

    [Fact]
    public async Task Lookup_masks_the_email_and_unknown_numbers_look_like_accounts_without_email()
    {
        var known = await Handlers(Anonymous()).Handle(new ForgotPasswordLookupQuery(new("1234")), default);
        var unknown = await Handlers(Anonymous()).Handle(new ForgotPasswordLookupQuery(new("9999")), default);

        known.Data.Should().Be(new ForgotPasswordLookupResult(true, "a••••••@g••••.com"));
        unknown.Data.Should().Be(new ForgotPasswordLookupResult(false, null));
    }

    [Fact]
    public async Task Reset_link_goes_to_the_email_on_record_once_a_minute_and_unknown_numbers_get_the_same_answer()
    {
        var handlers = Handlers(Anonymous());

        var first = await handlers.Handle(new SendPasswordResetLinkCommand(new("1234")), default);
        var second = await handlers.Handle(new SendPasswordResetLinkCommand(new("1234")), default);
        var unknown = await handlers.Handle(new SendPasswordResetLinkCommand(new("9999")), default);

        _sent.Should().ContainSingle().Which.Should().Be(("adeyemi@gmail.com", "Reset your password"));
        first.Messages.Should().Equal(second.Messages).And.Equal(unknown.Messages);
    }

    [Fact]
    public async Task Reset_link_can_be_requested_with_the_email_address_instead_of_the_number()
    {
        _member.NormalizedEmail = _member.Email!.ToUpperInvariant();
        await _db.SaveChangesAsync();

        await Handlers(Anonymous()).Handle(new SendPasswordResetLinkCommand(new(" Adeyemi@Gmail.com ")), default);

        _sent.Should().ContainSingle().Which.Should().Be(("adeyemi@gmail.com", "Reset your password"));
    }

    [Fact]
    public async Task Email_change_needs_secretariat_approval_then_the_link_goes_to_the_new_address()
    {
        await Handlers(Anonymous()).Handle(new RequestEmailChangeCommand(new("1234", "old@typo.com", null, null)), default);
        await Handlers(Anonymous()).Handle(new RequestEmailChangeCommand(new("1234", "musa.bello@yahoo.com", "0803", "Lost access")), default);
        _sent.Should().BeEmpty();

        var requests = await _db.EmailChangeRequests.ToListAsync();
        requests.Should().ContainSingle(r => r.Status == EmailChangeRequestStatus.Pending);
        var pending = requests.Single(r => r.Status == EmailChangeRequestStatus.Pending);

        var denied = await Handlers(Member()).Handle(new ApproveEmailChangeCommand(pending.Id), default);
        denied.Messages.Should().ContainSingle(m => m.StartsWith("FORBIDDEN"));

        var approved = await Handlers(Secretariat()).Handle(new ApproveEmailChangeCommand(pending.Id), default);

        approved.Succeeded.Should().BeTrue(string.Join(", ", approved.Messages));
        _member.Email.Should().Be("musa.bello@yahoo.com");
        _sent.Should().Contain(("musa.bello@yahoo.com", "Reset your password"));
        _sent.Should().Contain(("adeyemi@gmail.com", "Your email address was updated"));
        _sent.Should().Contain(("musa.bello@yahoo.com", "Your email address was updated"));
        (await Handlers(Secretariat()).Handle(new GetPendingEmailChangeCountQuery(), default)).Data.Should().Be(0);
    }

    [Fact]
    public async Task Unknown_number_email_change_is_accepted_silently_and_stores_nothing()
    {
        var result = await Handlers(Anonymous()).Handle(new RequestEmailChangeCommand(new("9999", "x@y.com", null, null)), default);

        result.Succeeded.Should().BeTrue();
        (await _db.EmailChangeRequests.CountAsync()).Should().Be(0);
    }

    private AccountRecoveryHandlers Handlers(ICurrentUserService user) => new(
        _db, _recovery.Object, _email.Object, user, _cache, TimeProvider.System, NullLogger<AccountRecoveryHandlers>.Instance);

    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    private static ICurrentUserService Anonymous() => User(null);

    private static ICurrentUserService Member() => User(Roles.Member);

    private static ICurrentUserService Secretariat() => User(Roles.NationalAdmin);

    private static ICurrentUserService User(string? role)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
        user.SetupGet(u => u.UserName).Returns("admin");
        user.SetupGet(u => u.OrganizationLevel).Returns(OrganizationLevel.National);
        user.Setup(u => u.IsInRole(It.IsAny<string>())).Returns<string>(r => r == role);
        return user.Object;
    }
}
