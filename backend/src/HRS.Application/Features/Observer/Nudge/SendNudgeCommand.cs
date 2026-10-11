using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Observer.Nudge;

/// <summary>FR-OBS-003: a reminder to an overdue child unit, rate-limited to once per 24 hours
/// per unit (not per window, not per sender — matches the SRS's literal wording).</summary>
public sealed record SendNudgeCommand(Guid UserId, Guid ChildUnitId, Guid WindowId) : IRequest<Result>;
