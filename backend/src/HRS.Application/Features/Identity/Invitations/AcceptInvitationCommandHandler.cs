using HRS.Application.Common;
using HRS.Application.Common.Interfaces;
using HRS.Application.Common.Models;
using HRS.Application.Features.Audit;
using HRS.Domain.Access;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Identity.Invitations;

public sealed class AcceptInvitationCommandHandler(
    IAppDbContext db,
    IIdentityService identityService,
    IClock clock,
    IAuditLog auditLog) : IRequestHandler<AcceptInvitationCommand, Result<AuthResult>>
{
    public async ValueTask<Result<AuthResult>> Handle(AcceptInvitationCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = SecureToken.Hash(request.Token);
        var invitation = await db.Invitations.FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

        var now = clock.UtcNow;

        if (invitation is null || !invitation.IsValid(now))
        {
            return Result.Failure<AuthResult>(
                Error.Conflict("INVITE_EXPIRED", "This invitation is no longer valid. Ask your administrator to resend it."));
        }

        var created = await identityService.CreateUserAsync(
            invitation.Email, request.Name, request.Phone, request.Password, cancellationToken);

        if (created.IsFailure)
        {
            return Result.Failure<AuthResult>(created.Error);
        }

        invitation.Accept(now);

        if (invitation.PendingHeadOfUnitId is { } unitId)
        {
            db.RoleAssignments.Add(new RoleAssignment(
                Guid.CreateVersion7(), created.Value.Id, unitId, invitation.PendingRole ?? RoleName.UnitHead,
                invitation.PendingDelegateScope, invitation.PendingStartsAt ?? now, invitation.PendingEndsAt,
                appointedBy: invitation.AppointedBy));
        }

        auditLog.Add(new AuditEntry(
            AuditActions.InvitationAccepted, created.Value.Id, "invitation", invitation.Id.ToString(),
            $"Accepted the invitation for {invitation.Email}", invitation.PendingHeadOfUnitId));
        await db.SaveChangesAsync(cancellationToken);

        var tokens = await identityService.IssueTokensAsync(created.Value.Id, request.IpAddress, request.UserAgent, cancellationToken);

        return new AuthResult(created.Value.Id, created.Value.Name, created.Value.Email, tokens);
    }
}
