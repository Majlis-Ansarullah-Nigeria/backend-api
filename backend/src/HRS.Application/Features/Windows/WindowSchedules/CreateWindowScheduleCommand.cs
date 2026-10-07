using HRS.Domain.Common;
using HRS.Domain.Templates;
using Mediator;

namespace HRS.Application.Features.Windows.WindowSchedules;

public sealed record CreateWindowScheduleCommand(
    Guid UserId,
    Guid ReportTypeId,
    ReportTypeFrequency Frequency,
    int OpensDayOffset,
    int GraceDays,
    int ClosesDayOffset,
    IReadOnlyList<ScheduleDueOffset> DueOffsets) : IRequest<Result<CreateWindowScheduleResult>>;

public sealed record CreateWindowScheduleResult(WindowScheduleSummary Schedule, int WindowsGenerated);
