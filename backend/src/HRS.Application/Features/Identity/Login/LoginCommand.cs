using HRS.Application.Common.Models;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.Login;

public sealed record LoginCommand(
    string Identifier,
    string Password,
    string? IpAddress,
    string? UserAgent) : IRequest<Result<AuthResult>>;
