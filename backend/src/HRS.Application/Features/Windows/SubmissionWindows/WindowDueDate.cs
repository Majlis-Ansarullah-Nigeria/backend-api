using HRS.Domain.Organization;

namespace HRS.Application.Features.Windows.SubmissionWindows;

public sealed record WindowDueDate(UnitLevelCode Level, DateTimeOffset DueAt);
