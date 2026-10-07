namespace HRS.Domain.Templates;

/// <summary>
/// One versioned snapshot of a ReportType's template (FR-TPL-007, FR-TPL-008). SchemaJson holds
/// the serialized sections/fields shape — the structured DTO and its validation live in
/// Application (Features/Templates/TemplateVersions), since Domain stays free of any JSON or
/// serialization concern. Published versions are immutable: UpdateDraftSchema only works while
/// Status is Draft, which is also how "editing a published template forks a new draft" (FR-TPL-008)
/// is enforced — the handler that calls this either updates an existing Draft or creates a new one.
/// </summary>
public sealed class TemplateVersion
{
    private TemplateVersion()
    {
    }

    private TemplateVersion(Guid id, Guid reportTypeId, int versionNumber, string schemaJson, DateTimeOffset now)
    {
        Id = id;
        ReportTypeId = reportTypeId;
        VersionNumber = versionNumber;
        Status = TemplateVersionStatus.Draft;
        SchemaJson = schemaJson;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid ReportTypeId { get; private set; }

    public int VersionNumber { get; private set; }

    public TemplateVersionStatus Status { get; private set; }

    public string SchemaJson { get; private set; } = "{}";

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset? RetiredAt { get; private set; }

    public static TemplateVersion CreateDraft(Guid id, Guid reportTypeId, int versionNumber, string schemaJson, DateTimeOffset now) =>
        new(id, reportTypeId, versionNumber, schemaJson, now);

    public void UpdateDraftSchema(string schemaJson)
    {
        if (Status != TemplateVersionStatus.Draft)
        {
            throw new InvalidOperationException("Only a draft version's schema can be updated.");
        }

        SchemaJson = schemaJson;
    }

    public void Publish(DateTimeOffset now)
    {
        if (Status != TemplateVersionStatus.Draft)
        {
            throw new InvalidOperationException("Only a draft version can be published.");
        }

        Status = TemplateVersionStatus.Published;
        PublishedAt = now;
    }

    public void Retire(DateTimeOffset now)
    {
        if (Status != TemplateVersionStatus.Published)
        {
            throw new InvalidOperationException("Only a published version can be retired.");
        }

        Status = TemplateVersionStatus.Retired;
        RetiredAt = now;
    }
}
