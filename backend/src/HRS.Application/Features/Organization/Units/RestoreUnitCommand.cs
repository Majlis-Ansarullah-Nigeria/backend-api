using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Organization.Units;

public sealed record RestoreUnitCommand(Guid UserId, Guid UnitId) : IRequest<Result>;
