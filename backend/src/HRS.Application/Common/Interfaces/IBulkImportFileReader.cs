using HRS.Domain.Common;

namespace HRS.Application.Common.Interfaces;

/// <summary>
/// Reads an uploaded bulk-import file (.xlsx or .csv, FR-ORG-003) into raw header/value rows.
/// Infrastructure picks the parser (ClosedXML / CsvHelper) by file extension; Application never
/// sees either library directly.
/// </summary>
public interface IBulkImportFileReader
{
    Result<IReadOnlyList<BulkImportRawRow>> ReadRows(byte[] content, string fileName);
}

/// <summary>One data row (header row excluded), 1-based, with cell values keyed by trimmed header text.</summary>
public sealed record BulkImportRawRow(int RowNumber, IReadOnlyDictionary<string, string> Values);
