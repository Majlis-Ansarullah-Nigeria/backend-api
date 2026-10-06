using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.Mfa;

public sealed record ConfirmTotpEnrollmentCommand(Guid UserId, string Code) : IRequest<Result<IReadOnlyList<string>>>;
