using FluentValidation;

namespace HRS.Application.Features.Windows.SubmissionWindows;

public sealed class ReopenWindowCommandValidator : AbstractValidator<ReopenWindowCommand>
{
    public ReopenWindowCommandValidator()
    {
        RuleFor(x => x.ReportTypeId).NotEmpty();
        RuleFor(x => x.WindowId).NotEmpty();
        RuleFor(x => x.UnitIds).NotEmpty().WithMessage("Reopen for at least one unit.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ReopenedUntil).NotEqual(default(DateTimeOffset));
    }
}
