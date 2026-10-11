using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Review.SavedViews;

public sealed record DeleteSavedViewCommand(Guid UserId, Guid SavedViewId) : IRequest<Result>;

public sealed class DeleteSavedViewCommandHandler(IAppDbContext db) : IRequestHandler<DeleteSavedViewCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteSavedViewCommand request, CancellationToken cancellationToken)
    {
        // Scoped by (Id, UserId) together rather than a separate role/ownership check — a saved
        // view not owned by this caller simply doesn't match, giving a clean 404 instead of a 403
        // that would leak whether the id exists at all.
        var view = await db.SavedViews.FirstOrDefaultAsync(v => v.Id == request.SavedViewId && v.UserId == request.UserId, cancellationToken);
        if (view is null)
        {
            return Result.Failure(Error.NotFound("SAVED_VIEW_NOT_FOUND", "That saved view doesn't exist."));
        }

        db.SavedViews.Remove(view);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
