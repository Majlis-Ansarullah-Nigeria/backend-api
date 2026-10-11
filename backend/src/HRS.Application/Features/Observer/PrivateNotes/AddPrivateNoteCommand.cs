using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Observer.ChildReport;
using HRS.Domain.Common;
using HRS.Domain.Observer;
using Mediator;

namespace HRS.Application.Features.Observer.PrivateNotes;

public sealed record AddPrivateNoteCommand(Guid UserId, Guid ChildUnitId, Guid ReportId, string Body) : IRequest<Result<PrivateNoteView>>;

public sealed class AddPrivateNoteCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IIdentityService identityService, IClock clock)
    : IRequestHandler<AddPrivateNoteCommand, Result<PrivateNoteView>>
{
    public async ValueTask<Result<PrivateNoteView>> Handle(AddPrivateNoteCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return Result.Failure<PrivateNoteView>(Error.Validation("NOTE_REQUIRED", "The note can't be empty."));
        }

        var resolved = await ChildReportResolver.ResolveAsync(
            db, accessPolicy, request.UserId, request.ChildUnitId, request.ReportId, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure<PrivateNoteView>(resolved.Error);
        }

        var now = clock.UtcNow;
        var note = PrivateNote.Create(Guid.CreateVersion7(), resolved.Value.Instance.Id, resolved.Value.ParentUnitId, request.UserId, request.Body.Trim(), now);
        db.PrivateNotes.Add(note);
        await db.SaveChangesAsync(cancellationToken);

        var author = await identityService.GetUserAsync(request.UserId, cancellationToken);
        return Result.Success(new PrivateNoteView(note.Id, author?.Name ?? "Unknown", note.Body, note.CreatedAt));
    }
}
