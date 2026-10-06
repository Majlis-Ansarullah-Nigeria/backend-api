using ClosedXML.Excel;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Organization.BulkImport;

namespace HRS.Infrastructure.Organization;

public sealed class BulkImportTemplateGenerator : IBulkImportTemplateGenerator
{
    public byte[] Generate()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Units and heads");

        for (var column = 0; column < BulkImportColumns.All.Count; column++)
        {
            var cell = sheet.Cell(1, column + 1);
            cell.Value = BulkImportColumns.All[column];
            cell.Style.Font.Bold = true;
        }

        // One worked example row, matching the mockup's own sample data.
        var example = new[] { "Zone", "SW", "South West Zone", "", "", "", "", "", "" };
        for (var column = 0; column < example.Length; column++)
        {
            sheet.Cell(2, column + 1).Value = example[column];
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
