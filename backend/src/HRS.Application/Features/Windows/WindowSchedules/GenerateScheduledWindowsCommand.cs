using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Windows.WindowSchedules;

/// <summary>On-demand top-up — the same generation Create/Update already trigger, exposed
/// standalone so the horizon can be refreshed without editing anything (e.g. a "Regenerate"
/// action, or a future scheduled job built on top of this same idempotent command).</summary>
public sealed record GenerateScheduledWindowsCommand(Guid UserId, Guid ReportTypeId, Guid ScheduleId) : IRequest<Result<int>>;
