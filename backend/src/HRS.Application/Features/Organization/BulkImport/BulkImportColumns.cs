namespace HRS.Application.Features.Organization.BulkImport;

/// <summary>
/// The bulk-import template's column headers. Code/Parent Code are the compound display codes
/// (e.g. "SW-IBD-AGD", blank for Parent Code meaning National) — the same identifier M2.1/M2.2
/// already show on screen, and the natural globally-unique key for "idempotent re-import,
/// matched by unit code" (FR-ORG-003). A bare per-parent Code wouldn't be unique enough.
/// </summary>
public static class BulkImportColumns
{
    public const string Level = "Level";
    public const string Code = "Code";
    public const string Name = "Name";
    public const string ParentCode = "Parent Code";
    public const string Address = "Address";
    public const string ContactPhone = "Contact Phone";
    public const string ContactEmail = "Contact Email";
    public const string HeadName = "Head Name";
    public const string HeadEmail = "Head Email";

    public static readonly IReadOnlyList<string> All =
        [Level, Code, Name, ParentCode, Address, ContactPhone, ContactEmail, HeadName, HeadEmail];

    public static readonly IReadOnlyList<string> Required = [Level, Code, Name, ParentCode];
}
