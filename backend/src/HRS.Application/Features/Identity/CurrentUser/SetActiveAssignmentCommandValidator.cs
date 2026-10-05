using FluentValidation;

namespace HRS.Application.Features.Identity.CurrentUser;

public sealed class SetActiveAssignmentCommandValidator : AbstractValidator<SetActiveAssignmentCommand>
{
    public SetActiveAssignmentCommandValidator()
    {
        RuleFor(x => x.RoleAssignmentId).NotEmpty();
    }
}
