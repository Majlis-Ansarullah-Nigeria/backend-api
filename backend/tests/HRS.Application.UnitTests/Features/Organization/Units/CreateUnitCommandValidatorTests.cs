using HRS.Application.Features.Organization.Units;

namespace HRS.Application.UnitTests.Features.Organization.Units;

public class CreateUnitCommandValidatorTests
{
    private readonly CreateUnitCommandValidator _validator = new();

    private static CreateUnitCommand Valid() =>
        new(Guid.NewGuid(), "Z03", "Zone 3", Guid.NewGuid(), Guid.NewGuid(), null, null, null, null, null);

    [Fact]
    public void ValidCommandPassesValidation()
    {
        Assert.True(_validator.Validate(Valid()).IsValid);
    }

    [Fact]
    public void EmptyCodeFailsValidation()
    {
        var result = _validator.Validate(Valid() with { Code = string.Empty });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateUnitCommand.Code));
    }

    [Fact]
    public void EmptyNameFailsValidation()
    {
        var result = _validator.Validate(Valid() with { Name = string.Empty });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateUnitCommand.Name));
    }

    [Fact]
    public void EmptyLevelIdFailsValidation()
    {
        var result = _validator.Validate(Valid() with { LevelId = Guid.Empty });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateUnitCommand.LevelId));
    }

    [Fact]
    public void EmptyParentIdFailsValidation()
    {
        var result = _validator.Validate(Valid() with { ParentId = Guid.Empty });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateUnitCommand.ParentId));
    }

    [Fact]
    public void MalformedContactEmailFailsValidation()
    {
        var result = _validator.Validate(Valid() with { ContactEmail = "not-an-email" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateUnitCommand.ContactEmail));
    }

    [Theory]
    [InlineData(-91)]
    [InlineData(91)]
    public void OutOfRangeLatitudeFailsValidation(double latitude)
    {
        var result = _validator.Validate(Valid() with { Latitude = latitude });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateUnitCommand.Latitude));
    }

    [Theory]
    [InlineData(-181)]
    [InlineData(181)]
    public void OutOfRangeLongitudeFailsValidation(double longitude)
    {
        var result = _validator.Validate(Valid() with { Longitude = longitude });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateUnitCommand.Longitude));
    }
}
