using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Windows.WindowSchedules;

public sealed record ActivateWindowScheduleCommand(Guid UserId, Guid ReportTypeId, Guid ScheduleId) : IRequest<Result<CreateWindowScheduleResult>>;
