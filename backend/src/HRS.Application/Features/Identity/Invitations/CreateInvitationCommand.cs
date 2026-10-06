using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.Invitations;

public sealed record CreateInvitationCommand(string Email, string Name, Guid? PendingHeadOfUnitId = null)
    : IRequest<Result<CreateInvitationResult>>;

public sealed record CreateInvitationResult(Guid Id, string Email, DateTimeOffset ExpiresAt);
