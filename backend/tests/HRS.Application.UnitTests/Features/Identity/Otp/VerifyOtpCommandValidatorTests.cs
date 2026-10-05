using HRS.Application.Features.Identity.Otp;

namespace HRS.Application.UnitTests.Features.Identity.Otp;

public class VerifyOtpCommandValidatorTests
{
    private readonly VerifyOtpCommandValidator _validator = new();

    [Fact]
    public void ValidCommandPassesValidation()
    {
        var result = _validator.Validate(new VerifyOtpCommand("head@example.org", "123456", null, null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void EmptyIdentifierFailsValidation()
    {
        var result = _validator.Validate(new VerifyOtpCommand(string.Empty, "123456", null, null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(VerifyOtpCommand.Identifier));
    }

    [Fact]
    public void WrongLengthCodeFailsValidation()
    {
        var result = _validator.Validate(new VerifyOtpCommand("head@example.org", "123", null, null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(VerifyOtpCommand.Code));
    }
}
