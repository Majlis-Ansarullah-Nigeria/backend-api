using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Organization.Units;

public sealed record UpdateUnitCommand(
    Guid UserId,
    Guid UnitId,
    string Name,
    string? Address,
    string? ContactPhone,
    string? ContactEmail,
    double? Latitude,
    double? Longitude) : IRequest<Result<UnitSummary>>;
