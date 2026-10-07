using FluentValidation;
using HRS.Domain.Templates;

namespace HRS.Application.Features.Windows.WindowSchedules;

public sealed class UpdateWindowScheduleCommandValidator : AbstractValidator<UpdateWindowScheduleCommand>
{
    public UpdateWindowScheduleCommandValidator()
    {
        RuleFor(x => x.ScheduleId).NotEmpty();
        RuleFor(x => x.Frequency).IsInEnum();
        RuleFor(x => x.Frequency).NotEqual(ReportTypeFrequency.AdHoc)
            .WithMessage("A recurring schedule can't be Ad-hoc frequency — create a one-off window instead.");
        RuleFor(x => x.GraceDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ClosesDayOffset).GreaterThan(x => x.OpensDayOffset)
            .WithMessage("Hard close must be after the window opens.");

        RuleFor(x => x.DueOffsets).NotEmpty().WithMessage("Set a due-day offset for at least one level.");

        RuleFor(x => x.DueOffsets)
            .Must(offsets => offsets.Select(o => o.Level).Distinct().Count() == offsets.Count)
            .WithMessage("Each level can only have one due-day offset.")
            .When(x => x.DueOffsets is not null);

        RuleForEach(x => x.DueOffsets).ChildRules(offset =>
        {
            offset.RuleFor(o => o.Level).IsInEnum();
        });

        RuleFor(x => x)
            .Custom((command, context) =>
            {
                foreach (var offset in command.DueOffsets ?? [])
                {
                    if (offset.DueDayOffset <= command.OpensDayOffset)
                    {
                        context.AddFailure(nameof(UpdateWindowScheduleCommand.DueOffsets), $"{offset.Level} is due before the window opens.");
                    }

                    if (offset.DueDayOffset > command.ClosesDayOffset)
                    {
                        context.AddFailure(nameof(UpdateWindowScheduleCommand.DueOffsets), $"{offset.Level} is due after the hard close.");
                    }
                }
            });
    }
}
