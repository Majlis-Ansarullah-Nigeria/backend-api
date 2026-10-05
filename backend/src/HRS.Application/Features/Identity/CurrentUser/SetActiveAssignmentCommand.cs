using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.CurrentUser;

public sealed record SetActiveAssignmentCommand(Guid UserId, Guid RoleAssignmentId) : IRequest<Result>;
