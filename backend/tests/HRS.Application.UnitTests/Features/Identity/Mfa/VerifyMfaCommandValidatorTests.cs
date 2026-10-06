using HRS.Application.Features.Identity.Mfa;

namespace HRS.Application.UnitTests.Features.Identity.Mfa;

public class VerifyMfaCommandValidatorTests
{
    private readonly VerifyMfaCommandValidator _validator = new();

    [Fact]
    public void ValidCommandPassesValidation()
    {
        var result = _validator.Validate(new VerifyMfaCommand("challenge-token", "123456", null, null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void EmptyChallengeTokenFailsValidation()
    {
        var result = _validator.Validate(new VerifyMfaCommand(string.Empty, "123456", null, null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(VerifyMfaCommand.ChallengeToken));
    }

    [Fact]
    public void EmptyCodeFailsValidation()
    {
        var result = _validator.Validate(new VerifyMfaCommand("challenge-token", string.Empty, null, null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(VerifyMfaCommand.Code));
    }

    [Fact]
    public void ARecoveryCodeShapedCodePassesValidation()
    {
        // Recovery codes aren't 6-digit TOTP codes — the validator must not assume a format.
        var result = _validator.Validate(new VerifyMfaCommand("challenge-token", "7KQ2-91LD", null, null));

        Assert.True(result.IsValid);
    }
}
