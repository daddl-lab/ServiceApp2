using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using ServiceApp.Core.Exceptions;
using ServiceApp.Core.Models;

namespace ServiceApp.Core.TicketParser;

/// <summary>
/// Standard-Implementierung von <see cref="IServiceTicketParser"/> auf Basis der
/// ClosedXML-Bibliothek. Findet die benötigten Spalten anhand ihrer Kopfzeilen-
/// Beschriftung ("Anlagedatum", "Fehlercode Ursache", optional "Ticketnummer"),
/// unabhängig von deren Reihenfolge oder zusätzlichen, nicht ausgewerteten Spalten -
/// das macht den Import robust gegenüber Tabellen, die "ähnlich" der Beispieldatei,
/// aber nicht spaltengleich sind.
/// </summary>
public sealed class ClosedXmlServiceTicketParser : IServiceTicketParser
{
    private const string TicketNumberColumn = "Ticketnummer";
    private const string CreatedAtColumn = "Anlagedatum";
    private const string CauseColumn = "Fehlercode Ursache";

    private readonly ILogger<ClosedXmlServiceTicketParser> _logger;

    public ClosedXmlServiceTicketParser(ILogger<ClosedXmlServiceTicketParser> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<ServiceTicket> Parse(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new TicketFileNotFoundException(filePath);
        }

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(filePath);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new TicketFileAccessDeniedException(filePath, ex);
        }
        catch (Exception ex) when (ex is not TicketImportException)
        {
            // ClosedXML wirft je nach Defekt unterschiedliche Exception-Typen (kein
            // gültiges ZIP-Archiv, defektes OpenXML-Paket, ...) - all das zählt
            // fachlich als "beschädigte Excel-Datei" und wird einheitlich gemeldet.
            throw new CorruptTicketFileException(filePath, ex);
        }

        using (workbook)
        {
            var worksheet = workbook.Worksheets.FirstOrDefault();
            var headerRow = worksheet?.FirstRowUsed();

            if (worksheet is null || headerRow is null)
            {
                throw new InvalidTicketFileFormatException(filePath, new[] { CreatedAtColumn, CauseColumn });
            }

            var columnIndexes = MapHeaderColumns(headerRow);

            var missingColumns = new List<string>();
            if (!columnIndexes.ContainsKey(CreatedAtColumn))
            {
                missingColumns.Add(CreatedAtColumn);
            }

            if (!columnIndexes.ContainsKey(CauseColumn))
            {
                missingColumns.Add(CauseColumn);
            }

            if (missingColumns.Count > 0)
            {
                throw new InvalidTicketFileFormatException(filePath, missingColumns);
            }

            var createdAtColumnIndex = columnIndexes[CreatedAtColumn];
            var causeColumnIndex = columnIndexes[CauseColumn];
            var ticketNumberColumnIndex = columnIndexes.GetValueOrDefault(TicketNumberColumn, -1);

            var tickets = new List<ServiceTicket>();
            var skippedRows = 0;

            foreach (var row in worksheet.RowsUsed().Skip(1))
            {
                if (!row.Cell(createdAtColumnIndex).TryGetValue(out DateTime createdAt))
                {
                    // Zeilen ohne auswertbares Anlagedatum können nicht in den
                    // zeitlichen Verlauf einsortiert werden und werden übersprungen,
                    // statt den gesamten Import abzubrechen.
                    skippedRows++;
                    continue;
                }

                var cause = row.Cell(causeColumnIndex).GetString().Trim();

                var ticketNumber = 0;
                if (ticketNumberColumnIndex > 0)
                {
                    row.Cell(ticketNumberColumnIndex).TryGetValue(out ticketNumber);
                }

                tickets.Add(new ServiceTicket(ticketNumber, createdAt, string.IsNullOrWhiteSpace(cause) ? null : cause));
            }

            _logger.LogInformation(
                "Excel-Datei {File} geladen: {Count} Tickets erkannt ({Skipped} Zeilen ohne gültiges Anlagedatum übersprungen).",
                Path.GetFileName(filePath), tickets.Count, skippedRows);

            return tickets;
        }
    }

    /// <summary>Bildet Spaltenbeschriftungen der Kopfzeile auf 1-basierte Spaltennummern ab.</summary>
    private static Dictionary<string, int> MapHeaderColumns(IXLRow headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var cell in headerRow.CellsUsed())
        {
            var header = cell.GetString().Trim();
            if (!string.IsNullOrEmpty(header) && !map.ContainsKey(header))
            {
                map[header] = cell.Address.ColumnNumber;
            }
        }

        return map;
    }
}
