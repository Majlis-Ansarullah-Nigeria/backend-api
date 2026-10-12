using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.Submissions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Submissions.Comments;

/// <summary>FR-REV-003, read side. Secretariat (any) or this report's current active
/// UnitHead/Delegate (FR-AC-005: observers never see review comments — this check deliberately
/// never includes ChildReportResolver's observer path).</summary>
public sealed record GetReportCommentsQuery(Guid UserId, Guid ReportId) : IRequest<Result<IReadOnlyList<CommentThread>>>;

public sealed class GetReportCommentsQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy, IIdentityService identityService)
    : IRequestHandler<GetReportCommentsQuery, Result<IReadOnlyList<CommentThread>>>
{
    public async ValueTask<Result<IReadOnlyList<CommentThread>>> Handle(GetReportCommentsQuery request, CancellationToken cancellationToken)
    {
        var instance = await db.ReportInstances.AsNoTracking().FirstOrDefaultAsync(i => i.Id == request.ReportId, cancellationToken);
        if (instance is null || instance.Status is ReportInstanceStatus.NotStarted or ReportInstanceStatus.Draft or ReportInstanceStatus.Missed)
        {
            return Result.Failure<IReadOnlyList<CommentThread>>(Error.NotFound("REPORT_NOT_FOUND", "That report doesn't exist."));
        }

        var isSecretariat = await accessPolicy.HasRoleAsync(request.UserId, RoleName.Secretariat, cancellationToken);
        var canEdit = isSecretariat || await accessPolicy.CanEditReportForUnitAsync(request.UserId, instance.UnitId, cancellationToken);
        if (!canEdit)
        {
            return Result.Failure<IReadOnlyList<CommentThread>>(Error.Forbidden("FORBIDDEN", "You don't have access to this report's comments."));
        }

        var comments = await db.ReportComments.AsNoTracking()
            .Where(c => c.ReportInstanceId == instance.Id)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        if (comments.Count == 0)
        {
            return Result.Success<IReadOnlyList<CommentThread>>([]);
        }

        var authorIds = comments.Select(c => c.AuthorUserId).Distinct().ToList();
        var namesByUserId = await identityService.GetUserNamesAsync(authorIds, cancellationToken);

        var templateVersion = await db.TemplateVersions.AsNoTracking().FirstAsync(v => v.Id == instance.TemplateVersionId, cancellationToken);
        var schema = JsonSerializer.Deserialize<TemplateSchema>(templateVersion.SchemaJson, JsonSerializerOptions.Web)!;
        var labelByFieldKey = schema.Sections.SelectMany(s => s.Fields).ToDictionary(f => f.Key, f => f.Label);

        CommentView ToView(ReportComment c) => new(c.Id, c.AuthorUserId, namesByUserId.GetValueOrDefault(c.AuthorUserId, "Unknown"), c.Body, c.CreatedAt);

        var repliesByRootId = comments.Where(c => c.ParentId is not null).GroupBy(c => c.ParentId!.Value).ToDictionary(g => g.Key, g => g.ToList());

        IReadOnlyList<CommentThread> threads = comments
            .Where(c => c.ParentId is null)
            .OrderBy(c => c.CreatedAt)
            .Select(root => new CommentThread(
                root.FieldKey,
                labelByFieldKey.GetValueOrDefault(root.FieldKey, root.FieldKey),
                ToView(root),
                repliesByRootId.GetValueOrDefault(root.Id, []).OrderBy(r => r.CreatedAt).Select(ToView).ToList()))
            .ToList();

        return Result.Success(threads);
    }
}
