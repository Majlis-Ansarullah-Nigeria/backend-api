using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Identity.CurrentUser;

public sealed class SetActiveAssignmentCommandHandler(IAppDbContext db, IIdentityService identityService, IClock clock)
    : IRequestHandler<SetActiveAssignmentCommand, Result>
{
    public async ValueTask<Result> Handle(SetActiveAssignmentCommand request, CancellationToken cancellationToken)
    {
        var assignment = await db.RoleAssignments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.RoleAssignmentId, cancellationToken);

        var now = clock.UtcNow;

        // Doesn't exist, belongs to someone else, or isn't currently active — all the same
        // "not available to you" outcome to the caller.
        if (assignment is null || assignment.UserId != request.UserId || !assignment.IsActive(now))
        {
            return Result.Failure(
                Error.NotFound("ROLE_ASSIGNMENT_NOT_FOUND", "That role assignment isn't available to you."));
        }

        await identityService.SetActiveRoleAssignmentAsync(request.UserId, assignment.Id, cancellationToken);

        return Result.Success();
    }
}
