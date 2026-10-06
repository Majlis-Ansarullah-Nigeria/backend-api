using HRS.Application.Features.Identity.Mfa;

namespace HRS.Application.UnitTests.Features.Identity.Mfa;

public class ConfirmTotpEnrollmentCommandValidatorTests
{
    private readonly ConfirmTotpEnrollmentCommandValidator _validator = new();

    [Fact]
    public void ValidCommandPassesValidation()
    {
        var result = _validator.Validate(new ConfirmTotpEnrollmentCommand(Guid.NewGuid(), "123456"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void EmptyCodeFailsValidation()
    {
        var result = _validator.Validate(new ConfirmTotpEnrollmentCommand(Guid.NewGuid(), string.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ConfirmTotpEnrollmentCommand.Code));
    }

    [Fact]
    public void WrongLengthCodeFailsValidation()
    {
        var result = _validator.Validate(new ConfirmTotpEnrollmentCommand(Guid.NewGuid(), "123"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ConfirmTotpEnrollmentCommand.Code));
    }
}
