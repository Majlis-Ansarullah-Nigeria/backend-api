using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Organization.Units;

public sealed record CreateUnitCommand(
    Guid UserId,
    string Code,
    string Name,
    Guid LevelId,
    Guid ParentId,
    string? Address,
    string? ContactPhone,
    string? ContactEmail,
    double? Latitude,
    double? Longitude) : IRequest<Result<UnitSummary>>;
