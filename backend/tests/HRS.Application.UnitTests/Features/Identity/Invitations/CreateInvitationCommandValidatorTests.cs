using HRS.Application.Features.Identity.Invitations;

namespace HRS.Application.UnitTests.Features.Identity.Invitations;

public class CreateInvitationCommandValidatorTests
{
    private readonly CreateInvitationCommandValidator _validator = new();

    [Fact]
    public void ValidCommandPassesValidation()
    {
        var result = _validator.Validate(new CreateInvitationCommand("head@example.org", "Head"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void EmptyEmailFailsValidation()
    {
        var result = _validator.Validate(new CreateInvitationCommand(string.Empty, "Head"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateInvitationCommand.Email));
    }

    [Fact]
    public void MalformedEmailFailsValidation()
    {
        var result = _validator.Validate(new CreateInvitationCommand("not-an-email", "Head"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateInvitationCommand.Email));
    }

    [Fact]
    public void EmptyNameFailsValidation()
    {
        var result = _validator.Validate(new CreateInvitationCommand("head@example.org", string.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateInvitationCommand.Name));
    }
}
