using System.Text.Json;
using HRS.Domain.Templates;

namespace HRS.Application.Features.Templates.TemplateVersions;

public sealed record TemplateVersionDetail(
    Guid Id,
    Guid ReportTypeId,
    int VersionNumber,
    TemplateVersionStatus Status,
    TemplateSchema Schema,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? RetiredAt)
{
    public static TemplateVersionDetail FromEntity(TemplateVersion version) => new(
        version.Id,
        version.ReportTypeId,
        version.VersionNumber,
        version.Status,
        JsonSerializer.Deserialize<TemplateSchema>(version.SchemaJson, JsonSerializerOptions.Web)!,
        version.CreatedAt,
        version.PublishedAt,
        version.RetiredAt);
}
