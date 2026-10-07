using HRS.Application.Features.Windows.SubmissionWindows;

namespace HRS.Application.UnitTests.Features.Windows.SubmissionWindows;

public class GrantWindowExtensionCommandValidatorTests
{
    private readonly GrantWindowExtensionCommandValidator _validator = new();
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static GrantWindowExtensionCommand Valid() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        [Guid.NewGuid()],
        Now.AddDays(20),
        Now.AddDays(25),
        "Flooding delayed the count.");

    [Fact]
    public void ValidCommandPassesValidation()
    {
        Assert.True(_validator.Validate(Valid()).IsValid);
    }

    [Fact]
    public void EmptyUnitIdsFailsValidation()
    {
        Assert.False(_validator.Validate(Valid() with { UnitIds = [] }).IsValid);
    }

    [Fact]
    public void EmptyReasonFailsValidation()
    {
        Assert.False(_validator.Validate(Valid() with { Reason = "" }).IsValid);
    }

    [Fact]
    public void NewClosesAtBeforeNewDueAtFailsValidation()
    {
        var command = Valid() with { NewClosesAt = Now.AddDays(19) };

        Assert.False(_validator.Validate(command).IsValid);
    }

    [Fact]
    public void NewClosesAtEqualToNewDueAtFailsValidation()
    {
        var command = Valid() with { NewClosesAt = Now.AddDays(20) };

        Assert.False(_validator.Validate(command).IsValid);
    }
}
