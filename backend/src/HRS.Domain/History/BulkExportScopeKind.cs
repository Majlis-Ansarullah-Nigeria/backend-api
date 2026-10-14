namespace HRS.Domain.History;

/// <summary>Where a BulkExport's instance list comes from. InstanceIds: the caller picked
/// specific rows on /history (re-validated against their own scope at render time — never
/// trusted blindly). OrganizationFilter: the Secretariat's /secretariat/exports composer, which
/// has no observer/head scope restriction — Secretariat can act on everything.</summary>
public enum BulkExportScopeKind
{
    InstanceIds,
    OrganizationFilter,
}
