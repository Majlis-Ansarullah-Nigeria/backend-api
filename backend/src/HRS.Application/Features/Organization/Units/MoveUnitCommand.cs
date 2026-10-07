using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Organization.Units;

public sealed record MoveUnitCommand(Guid UserId, Guid UnitId, Guid NewParentId) : IRequest<Result<UnitSummary>>;
