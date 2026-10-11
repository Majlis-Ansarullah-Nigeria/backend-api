using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.Review;
using Mediator;

namespace HRS.Application.Features.Review.SavedViews;

public sealed record CreateSavedViewCommand(Guid UserId, string Name, InboxFilter Filter) : IRequest<Result<SavedViewSummary>>;

public sealed class CreateSavedViewCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<CreateSavedViewCommand, Result<SavedViewSummary>>
{
    public async ValueTask<Result<SavedViewSummary>> Handle(CreateSavedViewCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.HasRoleAsync(request.UserId, RoleName.Secretariat, cancellationToken))
        {
            return Result.Failure<SavedViewSummary>(Error.Forbidden("FORBIDDEN", "Only the Secretariat has saved views."));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure<SavedViewSummary>(Error.Validation("NAME_REQUIRED", "Give this view a name."));
        }

        var filterJson = System.Text.Json.JsonSerializer.Serialize(request.Filter);
        var view = SavedView.Create(Guid.CreateVersion7(), request.UserId, request.Name.Trim(), filterJson, clock.UtcNow);
        db.SavedViews.Add(view);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(SavedViewSummary.FromEntity(view));
    }
}
