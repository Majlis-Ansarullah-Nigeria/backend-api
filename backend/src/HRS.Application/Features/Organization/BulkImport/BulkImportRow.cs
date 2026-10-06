namespace HRS.Application.Features.Organization.BulkImport;

/// <summary>One row of the dry-run preview, as shown in the "Check rows" wizard step.</summary>
public sealed record BulkImportRow(
    int RowNumber,
    BulkImportOutcome Outcome,
    string Level,
    string Code,
    string Name,
    string ParentCode,
    string HeadEmail,
    string? Issue);

public sealed record BulkImportSummary(
    int TotalRows,
    int NewCount,
    int UpdateCount,
    int WarningCount,
    int ErrorCount,
    IReadOnlyList<BulkImportRow> Rows)
{
    public static BulkImportSummary FromRows(IReadOnlyList<BulkImportRow> rows) => new(
        rows.Count,
        rows.Count(r => r.Outcome == BulkImportOutcome.New),
        rows.Count(r => r.Outcome == BulkImportOutcome.Update),
        rows.Count(r => r.Outcome == BulkImportOutcome.Warning),
        rows.Count(r => r.Outcome == BulkImportOutcome.Error),
        rows);
}
