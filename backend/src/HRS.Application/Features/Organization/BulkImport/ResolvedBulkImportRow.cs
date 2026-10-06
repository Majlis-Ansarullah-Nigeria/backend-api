using HRS.Domain.Organization;

namespace HRS.Application.Features.Organization.BulkImport;

/// <summary>
/// A row after resolving its level/parent/existing-unit references — internal working state
/// shared by preview and commit, never exposed over the API. <see cref="ShouldSetHead"/>
/// encodes the one business rule from the M2.3 plan: bulk import only ever sets a head when
/// creating a unit or filling a vacancy, never replacing an existing active head.
/// </summary>
internal sealed record ResolvedBulkImportRow(
    int RowNumber,
    BulkImportOutcome Outcome,
    string LevelInput,
    string Code,
    string LocalCode,
    string Name,
    string ParentCodeInput,
    string? Address,
    string? ContactPhone,
    string? ContactEmail,
    string HeadName,
    string HeadEmail,
    string? Issue,
    UnitLevel? Level,
    Unit? Parent,
    Unit? ExistingUnit,
    Unit? NewUnit,
    bool ShouldSetHead)
{
    /// <summary>The unit this row resolves to — the existing one being updated, or the one to insert. Null only for Error rows.</summary>
    public Unit? ResolvedUnit => ExistingUnit ?? NewUnit;

    public BulkImportRow ToPreviewRow() => new(RowNumber, Outcome, LevelInput, Code, Name, ParentCodeInput, HeadEmail, Issue);
}
