using FluentValidation;

namespace HRS.Application.Features.Organization.RoleAssignments;

public sealed class CreateRoleAssignmentCommandValidator : AbstractValidator<CreateRoleAssignmentCommand>
{
    public CreateRoleAssignmentCommandValidator()
    {
        RuleFor(x => x.UnitId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EndsAt)
            .GreaterThan(x => x.StartsAt)
            .When(x => x.EndsAt.HasValue)
            .WithMessage("The term's end date must be after its start date.");
    }
}
