using HRS.Application.Features.Organization.Units;

namespace HRS.Application.UnitTests.Features.Organization.Units;

public class UpdateUnitCommandValidatorTests
{
    private readonly UpdateUnitCommandValidator _validator = new();

    private static UpdateUnitCommand Valid() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Zone 3", null, null, null, null, null);

    [Fact]
    public void ValidCommandPassesValidation()
    {
        Assert.True(_validator.Validate(Valid()).IsValid);
    }

    [Fact]
    public void EmptyUnitIdFailsValidation()
    {
        var result = _validator.Validate(Valid() with { UnitId = Guid.Empty });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateUnitCommand.UnitId));
    }

    [Fact]
    public void EmptyNameFailsValidation()
    {
        var result = _validator.Validate(Valid() with { Name = string.Empty });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateUnitCommand.Name));
    }

    [Fact]
    public void MalformedContactEmailFailsValidation()
    {
        var result = _validator.Validate(Valid() with { ContactEmail = "not-an-email" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateUnitCommand.ContactEmail));
    }
}
