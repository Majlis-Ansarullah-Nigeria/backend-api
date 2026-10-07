using HRS.Application.Features.Windows.SubmissionWindows;
using HRS.Domain.Organization;

namespace HRS.Application.UnitTests.Features.Windows.SubmissionWindows;

public class CreateSubmissionWindowCommandValidatorTests
{
    private readonly CreateSubmissionWindowCommandValidator _validator = new();
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static CreateSubmissionWindowCommand Valid() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "October 2026",
        Now,
        3,
        Now.AddDays(30),
        [new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))]);

    [Fact]
    public void ValidCommandPassesValidation()
    {
        Assert.True(_validator.Validate(Valid()).IsValid);
    }

    [Fact]
    public void EmptyPeriodLabelFailsValidation()
    {
        Assert.False(_validator.Validate(Valid() with { PeriodLabel = "" }).IsValid);
    }

    [Fact]
    public void NegativeGraceDaysFailsValidation()
    {
        Assert.False(_validator.Validate(Valid() with { GraceDays = -1 }).IsValid);
    }

    [Fact]
    public void ClosesAtBeforeOpensAtFailsValidation()
    {
        var command = Valid() with { ClosesAt = Now.AddDays(-1) };

        Assert.False(_validator.Validate(command).IsValid);
    }

    [Fact]
    public void EmptyDueDatesFailsValidation()
    {
        Assert.False(_validator.Validate(Valid() with { DueDates = [] }).IsValid);
    }

    [Fact]
    public void DuplicateLevelsInDueDatesFailsValidation()
    {
        var command = Valid() with
        {
            DueDates = [new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10)), new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(12))],
        };

        Assert.False(_validator.Validate(command).IsValid);
    }

    [Fact]
    public void ADueDateBeforeOpensAtFailsValidation()
    {
        var command = Valid() with { DueDates = [new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(-1))] };

        Assert.False(_validator.Validate(command).IsValid);
    }

    [Fact]
    public void ADueDateAfterClosesAtFailsValidation()
    {
        var command = Valid() with { DueDates = [new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(31))] };

        Assert.False(_validator.Validate(command).IsValid);
    }
}
