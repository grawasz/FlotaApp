using ClosedXML.Excel;
using Flota.Api.Infrastructure.Export;
using Flota.Core.Contracts;

namespace Flota.Api.Tests;

public class XlsxExporterTests
{
    static XLWorkbook Open(byte[] bytes) => new(new MemoryStream(bytes));

    [Fact]
    public void Export_WritesHeadersAndTypedValues()
    {
        var table = new ExportTable("Raport: trasa [1]",
            [new("Obiekt"), new("Dystans [km]", ExportColumnKind.Number), new("Data", ExportColumnKind.DateTime), new("Aktywny", ExportColumnKind.Boolean)],
            [["WX 12345", "250.5", "2026-09-25T06:00:00", "true"], ["Radio 2", "abc", null, "x"]]);

        using var wb = Open(XlsxExporter.Export(table, DateTimeOffset.UnixEpoch));
        var ws = wb.Worksheets.Single();
        Assert.Equal("Raport- trasa -1-", ws.Name);
        Assert.Equal("Dystans [km]", ws.Cell(1, 2).GetString());
        Assert.True(ws.Cell(1, 1).Style.Font.Bold);
        Assert.Equal(250.5, ws.Cell(2, 2).GetDouble());
        Assert.Equal(new DateTime(2026, 9, 25, 6, 0, 0), ws.Cell(2, 3).GetDateTime());
        Assert.True(ws.Cell(2, 4).GetBoolean());
        // Wartości niepoprawne dla typu kolumny zapisywane jako tekst
        Assert.Equal("abc", ws.Cell(3, 2).GetString());
        Assert.True(ws.Cell(3, 3).IsEmpty());
        Assert.Equal(XlsxExporter.Author, wb.Properties.Author);
    }

    [Fact]
    public void Export_LongSheetName_IsTruncated()
    {
        var table = new ExportTable(new string('a', 40), [new("A")], []);
        using var wb = Open(XlsxExporter.Export(table, DateTimeOffset.UnixEpoch));
        Assert.Equal(31, wb.Worksheets.Single().Name.Length);
    }
}
