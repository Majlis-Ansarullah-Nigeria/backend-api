using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Observer.ChildReport;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Observer.PrivateNotes;

public sealed record GetPrivateNotesQuery(Guid UserId, Guid ChildUnitId, Guid ReportId) : IRequest<Result<IReadOnlyList<PrivateNoteView>>>;

public sealed class GetPrivateNotesQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy, IIdentityService identityService)
    : IRequestHandler<GetPrivateNotesQuery, Result<IReadOnlyList<PrivateNoteView>>>
{
    public async ValueTask<Result<IReadOnlyList<PrivateNoteView>>> Handle(GetPrivateNotesQuery request, CancellationToken cancellationToken)
    {
        var resolved = await ChildReportResolver.ResolveAsync(
            db, accessPolicy, request.UserId, request.ChildUnitId, request.ReportId, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure<IReadOnlyList<PrivateNoteView>>(resolved.Error);
        }

        var notes = await db.PrivateNotes
            .AsNoTracking()
            .Where(n => n.ReportInstanceId == resolved.Value.Instance.Id && n.ObserverUnitId == resolved.Value.ParentUnitId)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync(cancellationToken);

        var authorNames = await identityService.GetUserNamesAsync(notes.Select(n => n.AuthorUserId).Distinct().ToList(), cancellationToken);

        IReadOnlyList<PrivateNoteView> views = notes
            .Select(n => new PrivateNoteView(n.Id, authorNames.GetValueOrDefault(n.AuthorUserId, "Unknown"), n.Body, n.CreatedAt))
            .ToList();

        return Result.Success(views);
    }
}
