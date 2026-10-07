using HRS.Application.Features.Windows.WindowSchedules;
using HRS.Domain.Organization;
using HRS.Domain.Templates;

namespace HRS.Application.UnitTests.Features.Windows.WindowSchedules;

public class CreateWindowScheduleCommandValidatorTests
{
    private readonly CreateWindowScheduleCommandValidator _validator = new();

    private static CreateWindowScheduleCommand Valid() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        ReportTypeFrequency.Monthly,
        1,
        3,
        25,
        [new ScheduleDueOffset(UnitLevelCode.Muqam, 10)]);

    [Fact]
    public void ValidCommandPassesValidation()
    {
        Assert.True(_validator.Validate(Valid()).IsValid);
    }

    [Fact]
    public void AdHocFrequencyFailsValidation()
    {
        Assert.False(_validator.Validate(Valid() with { Frequency = ReportTypeFrequency.AdHoc }).IsValid);
    }

    [Fact]
    public void NegativeGraceDaysFailsValidation()
    {
        Assert.False(_validator.Validate(Valid() with { GraceDays = -1 }).IsValid);
    }

    [Fact]
    public void ClosesDayOffsetBeforeOpensDayOffsetFailsValidation()
    {
        Assert.False(_validator.Validate(Valid() with { ClosesDayOffset = 0 }).IsValid);
    }

    [Fact]
    public void EmptyDueOffsetsFailsValidation()
    {
        Assert.False(_validator.Validate(Valid() with { DueOffsets = [] }).IsValid);
    }

    [Fact]
    public void DuplicateLevelsInDueOffsetsFailsValidation()
    {
        var command = Valid() with
        {
            DueOffsets = [new ScheduleDueOffset(UnitLevelCode.Muqam, 10), new ScheduleDueOffset(UnitLevelCode.Muqam, 12)],
        };

        Assert.False(_validator.Validate(command).IsValid);
    }

    [Fact]
    public void ADueOffsetBeforeOpensDayOffsetFailsValidation()
    {
        var command = Valid() with { DueOffsets = [new ScheduleDueOffset(UnitLevelCode.Muqam, 1)] };

        Assert.False(_validator.Validate(command).IsValid);
    }

    [Fact]
    public void ADueOffsetAfterClosesDayOffsetFailsValidation()
    {
        var command = Valid() with { DueOffsets = [new ScheduleDueOffset(UnitLevelCode.Muqam, 26)] };

        Assert.False(_validator.Validate(command).IsValid);
    }
}
