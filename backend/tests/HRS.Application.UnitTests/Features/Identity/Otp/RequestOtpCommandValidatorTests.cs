using HRS.Application.Features.Identity.Otp;

namespace HRS.Application.UnitTests.Features.Identity.Otp;

public class RequestOtpCommandValidatorTests
{
    private readonly RequestOtpCommandValidator _validator = new();

    [Fact]
    public void ValidCommandPassesValidation()
    {
        var result = _validator.Validate(new RequestOtpCommand("head@example.org"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void EmptyIdentifierFailsValidation()
    {
        var result = _validator.Validate(new RequestOtpCommand(string.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RequestOtpCommand.Identifier));
    }
}
