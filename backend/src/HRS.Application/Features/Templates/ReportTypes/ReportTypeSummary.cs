using HRS.Domain.Organization;
using HRS.Domain.Templates;

namespace HRS.Application.Features.Templates.ReportTypes;

public sealed record ReportTypeSummary(
    Guid Id,
    string Name,
    string? Description,
    ReportTypeCategory Category,
    ReportTypeFrequency Frequency,
    IReadOnlyList<UnitLevelCode> ApplicableLevels,
    UnitLevelCode RecipientLevel,
    int ObserverDepth,
    bool AllowAttachments,
    bool AllowNilReport,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ArchivedAt)
{
    public static ReportTypeSummary FromEntity(ReportType reportType) => new(
        reportType.Id,
        reportType.Name,
        reportType.Description,
        reportType.Category,
        reportType.Frequency,
        reportType.ApplicableLevels,
        reportType.RecipientLevel,
        reportType.ObserverDepth,
        reportType.AllowAttachments,
        reportType.AllowNilReport,
        reportType.CreatedAt,
        reportType.ArchivedAt);
}
