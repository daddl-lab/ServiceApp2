using ClosedXML.Excel;

namespace ServiceApp.Tests.TestData;

/// <summary>
/// Erzeugt echte Excel-Dateien mit Servicetickets für Tests des Excel-Parsers, analog
/// zu <see cref="TestPdfBuilder"/> für PDF-Telefonberichte.
/// </summary>
public static class TestExcelBuilder
{
    /// <summary>
    /// Erzeugt eine Ticket-Tabelle mit den Spalten Ticketnummer, Anlagedatum, Typ und
    /// Fehlercode Ursache (in dieser Reihenfolge vertauscht mit einer zusätzlichen,
    /// nicht ausgewerteten Spalte, um die spaltennamenbasierte Erkennung zu testen).
    /// </summary>
    public static void CreateTicketWorkbook(string filePath, IEnumerable<(int TicketNumber, DateTime CreatedAt, string? Cause, string? Type)> tickets)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Procedure");

        worksheet.Cell(1, 1).Value = "Titel";
        worksheet.Cell(1, 2).Value = "Ticketnummer";
        worksheet.Cell(1, 3).Value = "Fehlercode Ursache";
        worksheet.Cell(1, 4).Value = "Anlagedatum";
        worksheet.Cell(1, 5).Value = "Typ";

        var row = 2;
        foreach (var (ticketNumber, createdAt, cause, type) in tickets)
        {
            worksheet.Cell(row, 1).Value = $"Ticket {ticketNumber}";
            worksheet.Cell(row, 2).Value = ticketNumber;
            worksheet.Cell(row, 3).Value = cause;
            worksheet.Cell(row, 4).Value = createdAt;
            worksheet.Cell(row, 5).Value = type;
            row++;
        }

        workbook.SaveAs(filePath);
    }

    /// <summary>Erzeugt eine Excel-Datei ohne die Spalte "Anlagedatum", um Formatfehler zu testen.</summary>
    public static void CreateWorkbookMissingRequiredColumns(string filePath)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Procedure");
        worksheet.Cell(1, 1).Value = "Titel";
        worksheet.Cell(1, 2).Value = "Ticketnummer";
        worksheet.Cell(2, 1).Value = "Ohne Datum";
        worksheet.Cell(2, 2).Value = 1;
        workbook.SaveAs(filePath);
    }

    /// <summary>Schreibt eine absichtlich ungültige (keine Excel-) Datei mit der Endung ".xlsx".</summary>
    public static void CreateCorruptWorkbook(string filePath)
    {
        File.WriteAllBytes(filePath, "Dies ist keine gueltige Excel-Datei"u8.ToArray());
    }
}
