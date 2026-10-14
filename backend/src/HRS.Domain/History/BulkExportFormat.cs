namespace HRS.Domain.History;

/// <summary>FR-HIS-004's two formats. CSV isn't in the requirement text and isn't built.</summary>
public enum BulkExportFormat
{
    ZipOfPdfs,
    ExcelWorkbook,
}
