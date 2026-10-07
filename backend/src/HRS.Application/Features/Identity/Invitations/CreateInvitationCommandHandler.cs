using HRS.Application.Common;
using HRS.Application.Common.Interfaces;
using HRS.Application.Common.Options;
using HRS.Domain.Common;
using HRS.Domain.Invitations;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HRS.Application.Features.Identity.Invitations;

public sealed class CreateInvitationCommandHandler(
    IAppDbContext db,
    IIdentityService identityService,
    IEmailSender emailSender,
    IClock clock,
    IOptions<AppOptions> appOptions) : IRequestHandler<CreateInvitationCommand, Result<CreateInvitationResult>>
{
    public async ValueTask<Result<CreateInvitationResult>> Handle(
        CreateInvitationCommand request, CancellationToken cancellationToken)
    {
        if (await identityService.EmailIsRegisteredAsync(request.Email, cancellationToken))
        {
            return Result.Failure<CreateInvitationResult>(
                Error.Conflict("USER_ALREADY_EXISTS", "An account with this email already exists."));
        }

        var now = clock.UtcNow;

        // Re-inviting supersedes any invite still pending for this email.
        // Exact match is enough here: the authoritative duplicate-account check above already
        // goes through Identity's normalized-email lookup. This just avoids piling up pending
        // invitation rows when the same email is re-invited with matching casing.
        var pending = await db.Invitations
            .Where(i => i.Email == request.Email && i.AcceptedAt == null && i.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var invite in pending)
        {
            invite.Revoke(now);
        }

        var token = SecureToken.Generate();
        var invitation = Invitation.Create(
            Guid.CreateVersion7(), request.Email, request.Name, SecureToken.Hash(token), now,
            request.PendingHeadOfUnitId, request.PendingRole, request.PendingDelegateScope, request.AppointedBy,
            request.PendingStartsAt, request.PendingEndsAt);

        db.Invitations.Add(invitation);
        await db.SaveChangesAsync(cancellationToken);

        var acceptUrl = $"{appOptions.Value.PublicUrl.TrimEnd('/')}/invite/{token}";

        await emailSender.QueueAsync(
            new EmailMessage(
                invitation.Email,
                "You've been invited to HRS",
                $"Hi {invitation.Name},\n\n"
                    + $"You've been invited to join the Reporting Service. Set up your account:\n{acceptUrl}\n\n"
                    + $"This invitation expires on {invitation.ExpiresAt:d MMMM yyyy}."),
            cancellationToken);

        return new CreateInvitationResult(invitation.Id, invitation.Email, invitation.ExpiresAt);
    }
}
