using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.Invitations;

public sealed record GetInvitationQuery(string Token) : IRequest<Result<InvitationPreview>>;

public sealed record InvitationPreview(string Name, string Email, DateTimeOffset ExpiresAt);
