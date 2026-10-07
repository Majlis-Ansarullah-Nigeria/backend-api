using FluentValidation;

namespace HRS.Application.Features.Organization.Units;

public sealed class MoveUnitCommandValidator : AbstractValidator<MoveUnitCommand>
{
    public MoveUnitCommandValidator()
    {
        RuleFor(x => x.UnitId).NotEmpty();
        RuleFor(x => x.NewParentId).NotEmpty();
    }
}
