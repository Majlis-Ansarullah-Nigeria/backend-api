using HRS.Domain.Organization;

namespace HRS.Application.Features.Windows.WindowSchedules;

public sealed record ScheduleDueOffset(UnitLevelCode Level, int DueDayOffset);
