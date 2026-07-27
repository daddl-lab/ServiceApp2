using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ServiceApp.Tests.TestData;

/// <summary>
/// Erzeugt echte PDF-Dateien mit Anrufberichts-Tabellenzeilen für Tests des
/// PDF-Parsers. Die Verwendung realer, generierter PDFs (statt gemockter Textzeilen)
/// stellt sicher, dass der komplette Weg über PdfPig-Textextraktion und
/// Wortpositions-Rekonstruktion tatsächlich funktioniert.
/// </summary>
public static class TestPdfBuilder
{
    static TestPdfBuilder()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Erzeugt ein PDF im deutschen Tabellenformat (TT.MM.JJJJ) unter dem angegebenen Pfad.</summary>
    public static void CreateGermanFormatReport(string filePath, IEnumerable<(DateTime Timestamp, bool Answered, TimeSpan? Duration)> calls)
    {
        var lines = calls.Select(c =>
        {
            var status = c.Answered ? "Angenommen" : "Nicht angenommen";
            var duration = c.Duration.HasValue ? FormatDuration(c.Duration.Value) : string.Empty;
            return $"{c.Timestamp:dd.MM.yyyy}    {c.Timestamp:HH:mm:ss}    {status}    {duration}";
        });

        CreatePdf(filePath, "Anrufbericht", lines);
    }

    /// <summary>Erzeugt ein PDF im ISO-Tabellenformat (JJJJ-MM-TT) unter dem angegebenen Pfad.</summary>
    public static void CreateIsoFormatReport(string filePath, IEnumerable<(DateTime Timestamp, bool Answered, TimeSpan? Duration)> calls)
    {
        var lines = calls.Select(c =>
        {
            var status = c.Answered ? "answered" : "missed";
            var duration = c.Duration.HasValue ? FormatDuration(c.Duration.Value) : string.Empty;
            return $"{c.Timestamp:yyyy-MM-dd}    {c.Timestamp:HH:mm:ss}    {status}    {duration}";
        });

        CreatePdf(filePath, "Call Report", lines);
    }

    /// <summary>Schreibt eine absichtlich ungültige (nicht-PDF) Datei mit der Endung ".pdf", um Fehlerbehandlung zu testen.</summary>
    public static void CreateCorruptPdf(string filePath)
    {
        File.WriteAllBytes(filePath, "Dies ist kein gueltiges PDF"u8.ToArray());
    }

    /// <summary>Erzeugt ein gültiges PDF, das aber kein bekanntes Berichtsformat enthält.</summary>
    public static void CreateUnrecognizedFormatReport(string filePath)
    {
        CreatePdf(filePath, "Unbekanntes Dokument", new[] { "Dies ist ein völlig anderer Dokumenttyp ohne Tabellendaten." });
    }

    private static string FormatDuration(TimeSpan duration)
        => duration.TotalHours >= 1
            ? duration.ToString(@"hh\:mm\:ss")
            : duration.ToString(@"mm\:ss");

    private static void CreatePdf(string filePath, string title, IEnumerable<string> lines)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Content().Column(column =>
                {
                    column.Item().Text(title).FontSize(16).Bold();
                    column.Item().PaddingTop(10);

                    foreach (var line in lines)
                    {
                        column.Item().Text(line);
                    }
                });
            });
        }).GeneratePdf(filePath);
    }
}
