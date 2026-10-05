using HRS.Application.Features.Identity.Refresh;

namespace HRS.Application.UnitTests.Features.Identity.Refresh;

public class RefreshCommandValidatorTests
{
    private readonly RefreshCommandValidator _validator = new();

    [Fact]
    public void ValidCommandPassesValidation()
    {
        var result = _validator.Validate(new RefreshCommand("some-refresh-token", null, null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void EmptyRefreshTokenFailsValidation()
    {
        var result = _validator.Validate(new RefreshCommand(string.Empty, null, null));

        Assert.False(result.IsValid);
    }
}
