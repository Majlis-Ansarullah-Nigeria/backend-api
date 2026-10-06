using HRS.Application.Common.Models;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.Mfa;

public sealed record VerifyMfaCommand(
    string ChallengeToken,
    string Code,
    string? IpAddress,
    string? UserAgent) : IRequest<Result<AuthResult>>;
