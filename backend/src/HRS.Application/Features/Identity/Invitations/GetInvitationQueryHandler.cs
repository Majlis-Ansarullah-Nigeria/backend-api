using HRS.Application.Common;
using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Identity.Invitations;

public sealed class GetInvitationQueryHandler(IAppDbContext db, IClock clock)
    : IRequestHandler<GetInvitationQuery, Result<InvitationPreview>>
{
    public async ValueTask<Result<InvitationPreview>> Handle(GetInvitationQuery request, CancellationToken cancellationToken)
    {
        var tokenHash = SecureToken.Hash(request.Token);
        var invitation = await db.Invitations.AsNoTracking()
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

        if (invitation is null)
        {
            return Result.Failure<InvitationPreview>(Error.NotFound("INVITE_NOT_FOUND", "That invitation link isn't valid."));
        }

        if (invitation.AcceptedAt is not null)
        {
            return Result.Failure<InvitationPreview>(
                Error.Conflict("INVITE_ALREADY_ACCEPTED", "This invitation has already been used."));
        }

        if (invitation.RevokedAt is not null || clock.UtcNow >= invitation.ExpiresAt)
        {
            return Result.Failure<InvitationPreview>(
                Error.Conflict("INVITE_EXPIRED", "This invitation has expired. Ask your administrator to resend it."));
        }

        return new InvitationPreview(invitation.Name, invitation.Email, invitation.ExpiresAt);
    }
}
