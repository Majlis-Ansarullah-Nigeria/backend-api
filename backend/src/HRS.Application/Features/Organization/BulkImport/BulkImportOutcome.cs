namespace HRS.Application.Features.Organization.BulkImport;

/// <summary>
/// A row's dry-run classification. Mutually exclusive and in this priority order: a row with a
/// blocking problem is always Error; otherwise a row with no Head Email is always Warning (even
/// if it's also creating or updating a unit); otherwise New or Update depending on whether its
/// Code already exists.
/// </summary>
public enum BulkImportOutcome
{
    New,
    Update,
    Warning,
    Error,
}
