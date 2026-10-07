using HRS.Domain.Templates;

namespace HRS.Application.Features.Templates.TemplateVersions;

public sealed record TemplateVersionSummary(
    Guid Id,
    Guid ReportTypeId,
    int VersionNumber,
    TemplateVersionStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? RetiredAt)
{
    public static TemplateVersionSummary FromEntity(TemplateVersion version) => new(
        version.Id,
        version.ReportTypeId,
        version.VersionNumber,
        version.Status,
        version.CreatedAt,
        version.PublishedAt,
        version.RetiredAt);
}
