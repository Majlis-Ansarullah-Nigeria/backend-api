using FluentValidation;

namespace HRS.Application.Features.Templates.ReportTypes;

public sealed class UpdateReportTypeCommandValidator : AbstractValidator<UpdateReportTypeCommand>
{
    public UpdateReportTypeCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Frequency).IsInEnum();
        RuleFor(x => x.RecipientLevel).IsInEnum();
        RuleFor(x => x.ObserverDepth).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ApplicableLevels).NotEmpty();
        RuleForEach(x => x.ApplicableLevels).IsInEnum();
    }
}
