namespace HRS.Domain.Templates;

/// <summary>Draft → Published → Retired (FR-TPL-007). At most one Draft and one Published
/// version exist per ReportType at a time; Retired versions stay forever for history.</summary>
public enum TemplateVersionStatus
{
    Draft,
    Published,
    Retired,
}
