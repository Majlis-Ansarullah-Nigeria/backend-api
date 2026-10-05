using HRS.Application.Features.Identity.Invitations;

namespace HRS.Application.UnitTests.Features.Identity.Invitations;

public class AcceptInvitationCommandValidatorTests
{
    private readonly AcceptInvitationCommandValidator _validator = new();

    private static AcceptInvitationCommand Valid(string? phone = null) =>
        new("token-value", "Abdulrahman Adeyemi", phone, "a-long-enough-password1", true, null, null);

    [Fact]
    public void ValidCommandPassesValidation()
    {
        Assert.True(_validator.Validate(Valid()).IsValid);
    }

    [Fact]
    public void ValidCommandWithPhonePassesValidation()
    {
        Assert.True(_validator.Validate(Valid("+234 803 000 0000")).IsValid);
    }

    [Fact]
    public void EmptyTokenFailsValidation()
    {
        var result = _validator.Validate(Valid() with { Token = string.Empty });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AcceptInvitationCommand.Token));
    }

    [Fact]
    public void EmptyNameFailsValidation()
    {
        var result = _validator.Validate(Valid() with { Name = string.Empty });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AcceptInvitationCommand.Name));
    }

    [Fact]
    public void EmptyPasswordFailsValidation()
    {
        var result = _validator.Validate(Valid() with { Password = string.Empty });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AcceptInvitationCommand.Password));
    }

    [Fact]
    public void MalformedPhoneFailsValidation()
    {
        var result = _validator.Validate(Valid() with { Phone = "not a phone number" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AcceptInvitationCommand.Phone));
    }

    [Fact]
    public void DecliningThePrivacyNoticeFailsValidation()
    {
        var result = _validator.Validate(Valid() with { AgreedToPrivacyNotice = false });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AcceptInvitationCommand.AgreedToPrivacyNotice));
    }
}
