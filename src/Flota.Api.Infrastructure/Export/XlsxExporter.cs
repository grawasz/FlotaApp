using System.Globalization;
using ClosedXML.Excel;
using Flota.Core.Contracts;

namespace Flota.Api.Infrastructure.Export;

/// <summary>
/// Eksport tabel do XLSX (odpowiednik <c>Flota.Data.Export</c>): nagłówki w wierszu 1, dane od wiersza 2.
/// Wartości liczbowe i daty przekazywane w formacie niezależnym od kultury (liczby z kropką, daty ISO 8601).
/// </summary>
public static class XlsxExporter
{
    public const string Author = "Flota";

    public static byte[] Export(ExportTable table, DateTimeOffset created)
    {
        ArgumentNullException.ThrowIfNull(table);
        using var wb = new XLWorkbook();
        wb.Properties.Author = Author;
        wb.Properties.Created = created.LocalDateTime;

        var ws = wb.Worksheets.Add(SheetName(table.SheetName));
        for (var c = 0; c < table.Columns.Count; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = table.Columns[c].Header;
            cell.Style.Font.Bold = true;
        }

        for (var r = 0; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            for (var c = 0; c < table.Columns.Count && c < row.Count; c++)
                Write(ws.Cell(r + 2, c + 1), table.Columns[c].Kind, row[c]);
        }

        for (var c = 0; c < table.Columns.Count; c++)
        {
            var col = ws.Column(c + 1);
            col.Style.NumberFormat.Format = table.Columns[c].Kind switch
            {
                ExportColumnKind.Number => "#,##0.0#",
                ExportColumnKind.WholeNumber => "#,##0",
                ExportColumnKind.DateTime => "yyyy-mm-dd hh:mm:ss",
                ExportColumnKind.Date => "yyyy-mm-dd",
                _ => col.Style.NumberFormat.Format,
            };
        }
        ws.Row(1).Style.NumberFormat.Format = "@";
        ws.SheetView.FreezeRows(1);
        ws.Columns(1, Math.Max(1, table.Columns.Count)).AdjustToContents(1, Math.Min(table.Rows.Count + 1, 500));

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    static void Write(IXLCell cell, ExportColumnKind kind, string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        switch (kind)
        {
            case ExportColumnKind.Number or ExportColumnKind.WholeNumber
                when double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d):
                cell.Value = d;
                break;
            case ExportColumnKind.DateTime or ExportColumnKind.Date
                when DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt):
                cell.Value = dt;
                break;
            case ExportColumnKind.Boolean when bool.TryParse(value, out var b):
                cell.Value = b;
                break;
            default:
                cell.Value = value;
                break;
        }
    }

    /// <summary>Nazwa arkusza zgodna z ograniczeniami Excela (31 znaków, bez znaków specjalnych).</summary>
    static string SheetName(string name)
    {
        var clean = new string((string.IsNullOrWhiteSpace(name) ? "Arkusz1" : name)
            .Select(ch => ch is ':' or '\\' or '/' or '?' or '*' or '[' or ']' ? '-' : ch).ToArray());
        return clean.Length > 31 ? clean[..31] : clean;
    }
}
