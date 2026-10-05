using HRS.Application.Features.Identity.Login;

namespace HRS.Application.UnitTests.Features.Identity.Login;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void ValidCommandPassesValidation()
    {
        var result = _validator.Validate(new LoginCommand("head@example.org", "correct horse battery staple", null, null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void EmptyIdentifierFailsValidation()
    {
        var result = _validator.Validate(new LoginCommand(string.Empty, "password", null, null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginCommand.Identifier));
    }

    [Fact]
    public void EmptyPasswordFailsValidation()
    {
        var result = _validator.Validate(new LoginCommand("head@example.org", string.Empty, null, null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginCommand.Password));
    }
}
