using HRS.Application.Common.Models;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.Invitations;

public sealed record AcceptInvitationCommand(
    string Token,
    string Name,
    string? Phone,
    string Password,
    bool AgreedToPrivacyNotice,
    string? IpAddress,
    string? UserAgent) : IRequest<Result<AuthResult>>;
