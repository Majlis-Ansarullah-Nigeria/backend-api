using HRS.Domain.Common;
using HRS.Domain.Templates;
using Mediator;

namespace HRS.Application.Features.Windows.WindowSchedules;

public sealed record UpdateWindowScheduleCommand(
    Guid UserId,
    Guid ReportTypeId,
    Guid ScheduleId,
    ReportTypeFrequency Frequency,
    int OpensDayOffset,
    int GraceDays,
    int ClosesDayOffset,
    IReadOnlyList<ScheduleDueOffset> DueOffsets) : IRequest<Result<CreateWindowScheduleResult>>;
