using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Templates.FieldTargets;

/// <summary>Every field target of a report type (FR-TPL-011), for the template builder.</summary>
public sealed record GetFieldTargetsQuery(Guid UserId, Guid ReportTypeId) : IRequest<Result<IReadOnlyList<FieldTargetDto>>>;

public sealed class GetFieldTargetsQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy)
    : IRequestHandler<GetFieldTargetsQuery, Result<IReadOnlyList<FieldTargetDto>>>
{
    public async ValueTask<Result<IReadOnlyList<FieldTargetDto>>> Handle(GetFieldTargetsQuery request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsTemplateAdminAsync(request.UserId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<FieldTargetDto>>(Error.Forbidden("FORBIDDEN", "Only the Secretariat can manage report types."));
        }

        if (!await db.ReportTypes.AnyAsync(r => r.Id == request.ReportTypeId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<FieldTargetDto>>(Error.NotFound("REPORT_TYPE_NOT_FOUND", "Report type not found."));
        }

        var targets = await db.ReportFieldTargets.AsNoTracking()
            .Where(t => t.ReportTypeId == request.ReportTypeId)
            .OrderBy(t => t.FieldKey).ThenBy(t => t.Level)
            .Select(t => new FieldTargetDto(t.FieldKey, t.Level, t.Value))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<FieldTargetDto>>(targets);
    }
}
