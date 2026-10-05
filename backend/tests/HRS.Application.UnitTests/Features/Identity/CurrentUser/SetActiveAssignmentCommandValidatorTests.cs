using HRS.Application.Features.Identity.CurrentUser;

namespace HRS.Application.UnitTests.Features.Identity.CurrentUser;

public class SetActiveAssignmentCommandValidatorTests
{
    private readonly SetActiveAssignmentCommandValidator _validator = new();

    [Fact]
    public void ValidCommandPassesValidation()
    {
        var result = _validator.Validate(new SetActiveAssignmentCommand(Guid.NewGuid(), Guid.NewGuid()));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void EmptyRoleAssignmentIdFailsValidation()
    {
        var result = _validator.Validate(new SetActiveAssignmentCommand(Guid.NewGuid(), Guid.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SetActiveAssignmentCommand.RoleAssignmentId));
    }
}
