using FluentValidation;
using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using HRS.Domain.Templates;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Templates.FieldTargets;

/// <summary>
/// FR-TPL-011: sets one field's targets, level by level. Levels left out keep their target; a null
/// value clears it. Targets live on the report type, so this works whether the field is in a draft
/// or a published version and never needs a new version. Only levels the report type is filed at
/// can have a target. Secretariat only, like the rest of template management.
/// </summary>
public sealed record SetFieldTargetsCommand(Guid UserId, Guid ReportTypeId, string FieldKey, IReadOnlyList<FieldTargetLevelValue> Targets)
    : IRequest<Result<IReadOnlyList<FieldTargetDto>>>;

public sealed class SetFieldTargetsCommandValidator : AbstractValidator<SetFieldTargetsCommand>
{
    public SetFieldTargetsCommandValidator()
    {
        RuleFor(x => x.ReportTypeId).NotEmpty();
        RuleFor(x => x.FieldKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Targets).NotNull();
        RuleForEach(x => x.Targets).ChildRules(target =>
        {
            target.RuleFor(t => t.Level).IsInEnum();
            target.RuleFor(t => t.Value).GreaterThanOrEqualTo(0).When(t => t.Value is not null)
                .WithMessage("A target can't be negative.");
        });
        RuleFor(x => x.Targets)
            .Must(targets => targets.Select(t => t.Level).Distinct().Count() == targets.Count)
            .When(x => x.Targets is not null)
            .WithMessage("Each level can only have one target.");
    }
}

public sealed class SetFieldTargetsCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy)
    : IRequestHandler<SetFieldTargetsCommand, Result<IReadOnlyList<FieldTargetDto>>>
{
    public async ValueTask<Result<IReadOnlyList<FieldTargetDto>>> Handle(SetFieldTargetsCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsTemplateAdminAsync(request.UserId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<FieldTargetDto>>(Error.Forbidden("FORBIDDEN", "Only the Secretariat can manage report types."));
        }

        var reportType = await db.ReportTypes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == request.ReportTypeId, cancellationToken);
        if (reportType is null)
        {
            return Result.Failure<IReadOnlyList<FieldTargetDto>>(Error.NotFound("REPORT_TYPE_NOT_FOUND", "Report type not found."));
        }

        var notFiled = request.Targets.FirstOrDefault(t => !reportType.ApplicableLevels.Contains(t.Level));
        if (notFiled is not null)
        {
            return Result.Failure<IReadOnlyList<FieldTargetDto>>(
                Error.Validation("LEVEL_NOT_APPLICABLE", "This report type isn't filed at one of those levels."));
        }

        var existing = await db.ReportFieldTargets
            .Where(t => t.ReportTypeId == reportType.Id && t.FieldKey == request.FieldKey)
            .ToListAsync(cancellationToken);

        foreach (var target in request.Targets)
        {
            var current = existing.FirstOrDefault(t => t.Level == target.Level);
            if (target.Value is not { } value)
            {
                if (current is not null)
                {
                    db.ReportFieldTargets.Remove(current);
                }
            }
            else if (current is not null)
            {
                current.SetValue(value);
            }
            else
            {
                db.ReportFieldTargets.Add(ReportFieldTarget.Create(Guid.CreateVersion7(), reportType.Id, request.FieldKey, target.Level, value));
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var saved = await db.ReportFieldTargets.AsNoTracking()
            .Where(t => t.ReportTypeId == reportType.Id && t.FieldKey == request.FieldKey)
            .OrderBy(t => t.Level)
            .Select(t => new FieldTargetDto(t.FieldKey, t.Level, t.Value))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<FieldTargetDto>>(saved);
    }
}
