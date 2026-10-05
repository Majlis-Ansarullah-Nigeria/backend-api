using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.Login;

public sealed record LoginCommand(
    string Identifier,
    string Password,
    string? IpAddress,
    string? UserAgent) : IRequest<Result<LoginResult>>;

public sealed record LoginResult(Guid UserId, string Name, string Email, AuthTokens Tokens);
