using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Review.SavedViews;

public sealed record GetSavedViewsQuery(Guid UserId) : IRequest<Result<IReadOnlyList<SavedViewSummary>>>;

public sealed class GetSavedViewsQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy)
    : IRequestHandler<GetSavedViewsQuery, Result<IReadOnlyList<SavedViewSummary>>>
{
    public async ValueTask<Result<IReadOnlyList<SavedViewSummary>>> Handle(GetSavedViewsQuery request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.HasRoleAsync(request.UserId, RoleName.Secretariat, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<SavedViewSummary>>(Error.Forbidden("FORBIDDEN", "Only the Secretariat has saved views."));
        }

        var views = await db.SavedViews.AsNoTracking()
            .Where(v => v.UserId == request.UserId)
            .OrderBy(v => v.CreatedAt)
            .ToListAsync(cancellationToken);

        IReadOnlyList<SavedViewSummary> summaries = [.. views.Select(SavedViewSummary.FromEntity)];
        return Result.Success(summaries);
    }
}
