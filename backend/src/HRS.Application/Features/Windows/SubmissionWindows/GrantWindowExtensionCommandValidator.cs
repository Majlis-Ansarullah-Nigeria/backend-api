using FluentValidation;

namespace HRS.Application.Features.Windows.SubmissionWindows;

public sealed class GrantWindowExtensionCommandValidator : AbstractValidator<GrantWindowExtensionCommand>
{
    public GrantWindowExtensionCommandValidator()
    {
        RuleFor(x => x.ReportTypeId).NotEmpty();
        RuleFor(x => x.WindowId).NotEmpty();
        RuleFor(x => x.UnitIds).NotEmpty().WithMessage("Grant the extension to at least one unit.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.NewClosesAt).GreaterThan(x => x.NewDueAt).WithMessage("The new close date must be after the new due date.");
    }
}
