using FluentValidation;

namespace HRS.Application.Features.Windows.SubmissionWindows;

public sealed class CreateSubmissionWindowCommandValidator : AbstractValidator<CreateSubmissionWindowCommand>
{
    public CreateSubmissionWindowCommandValidator()
    {
        RuleFor(x => x.ReportTypeId).NotEmpty();
        RuleFor(x => x.PeriodLabel).NotEmpty().MaximumLength(100);
        RuleFor(x => x.GraceDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ClosesAt).GreaterThan(x => x.OpensAt).WithMessage("Hard close must be after the window opens.");

        RuleFor(x => x.DueDates).NotEmpty().WithMessage("Set a due date for at least one level.");

        RuleFor(x => x.DueDates)
            .Must(dueDates => dueDates.Select(d => d.Level).Distinct().Count() == dueDates.Count)
            .WithMessage("Each level can only have one due date.")
            .When(x => x.DueDates is not null);

        RuleForEach(x => x.DueDates).ChildRules(dueDate =>
        {
            dueDate.RuleFor(d => d.Level).IsInEnum();
        });

        RuleFor(x => x)
            .Custom((command, context) =>
            {
                foreach (var dueDate in command.DueDates ?? [])
                {
                    if (dueDate.DueAt <= command.OpensAt)
                    {
                        context.AddFailure(nameof(CreateSubmissionWindowCommand.DueDates), $"{dueDate.Level} is due before the window opens.");
                    }

                    if (dueDate.DueAt > command.ClosesAt)
                    {
                        context.AddFailure(nameof(CreateSubmissionWindowCommand.DueDates), $"{dueDate.Level} is due after the hard close.");
                    }
                }
            });
    }
}
