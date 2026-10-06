using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.Mfa;

public sealed record DisableTotpCommand(Guid UserId) : IRequest<Result>;
