using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;

namespace HRS.Infrastructure.Organization;

/// <summary>Reads .xlsx (ClosedXML) and .csv (CsvHelper) bulk-import uploads (FR-ORG-003).</summary>
public sealed class BulkImportFileReader : IBulkImportFileReader
{
    public Result<IReadOnlyList<BulkImportRawRow>> ReadRows(byte[] content, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        try
        {
            return extension switch
            {
                ".xlsx" => ReadExcel(content),
                ".csv" => ReadCsv(content),
                _ => Result.Failure<IReadOnlyList<BulkImportRawRow>>(
                    Error.Validation("UNSUPPORTED_FILE_TYPE", "Upload an .xlsx or .csv file.")),
            };
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException)
        {
            return Result.Failure<IReadOnlyList<BulkImportRawRow>>(
                Error.Validation("UNREADABLE_FILE", "Couldn't read this file. Make sure it isn't corrupted or password-protected."));
        }
    }

    private static Result<IReadOnlyList<BulkImportRawRow>> ReadExcel(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.First();
        var usedRange = sheet.RangeUsed();

        if (usedRange is null)
        {
            return Result.Success<IReadOnlyList<BulkImportRawRow>>([]);
        }

        var rows = usedRange.RowsUsed().ToList();
        var headerRow = rows[0];
        var headers = headerRow.Cells().Select(cell => cell.GetString().Trim()).ToList();

        var dataRows = new List<BulkImportRawRow>();
        for (var i = 1; i < rows.Count; i++)
        {
            var cells = rows[i].Cells(1, headers.Count).ToList();
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var column = 0; column < headers.Count; column++)
            {
                values[headers[column]] = cells[column].GetString().Trim();
            }

            dataRows.Add(new BulkImportRawRow(i, values));
        }

        return Result.Success<IReadOnlyList<BulkImportRawRow>>(dataRows);
    }

    private static Result<IReadOnlyList<BulkImportRawRow>> ReadCsv(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var streamReader = new StreamReader(stream);
        using var csv = new CsvReader(streamReader, new CsvConfiguration(CultureInfo.InvariantCulture) { MissingFieldFound = null });

        if (!csv.Read() || !csv.ReadHeader() || csv.HeaderRecord is null)
        {
            return Result.Success<IReadOnlyList<BulkImportRawRow>>([]);
        }

        var headers = csv.HeaderRecord.Select(h => h.Trim()).ToList();
        var dataRows = new List<BulkImportRawRow>();
        var rowNumber = 1;

        while (csv.Read())
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in headers)
            {
                values[header] = (csv.GetField(header) ?? string.Empty).Trim();
            }

            dataRows.Add(new BulkImportRawRow(rowNumber, values));
            rowNumber++;
        }

        return Result.Success<IReadOnlyList<BulkImportRawRow>>(dataRows);
    }
}
