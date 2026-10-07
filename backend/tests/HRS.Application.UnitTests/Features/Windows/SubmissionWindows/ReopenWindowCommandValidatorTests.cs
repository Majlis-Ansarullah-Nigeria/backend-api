using HRS.Application.Features.Windows.SubmissionWindows;

namespace HRS.Application.UnitTests.Features.Windows.SubmissionWindows;

public class ReopenWindowCommandValidatorTests
{
    private readonly ReopenWindowCommandValidator _validator = new();
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static ReopenWindowCommand Valid() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        [Guid.NewGuid()],
        Now.AddDays(20),
        "Late due to a connectivity outage.");

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
    public void DefaultReopenedUntilFailsValidation()
    {
        Assert.False(_validator.Validate(Valid() with { ReopenedUntil = default }).IsValid);
    }
}
