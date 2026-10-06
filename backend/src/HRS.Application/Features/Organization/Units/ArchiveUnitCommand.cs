using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Organization.Units;

public sealed record ArchiveUnitCommand(Guid UserId, Guid UnitId) : IRequest<Result>;
