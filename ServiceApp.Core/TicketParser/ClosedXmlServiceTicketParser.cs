using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using ServiceApp.Core.Exceptions;
using ServiceApp.Core.Models;

namespace ServiceApp.Core.TicketParser;

/// <summary>
/// Standard-Implementierung von <see cref="IServiceTicketParser"/> auf Basis der
/// ClosedXML-Bibliothek. Findet die benötigten Spalten anhand ihrer Kopfzeilen-
/// Beschriftung ("Anlagedatum", "Fehlercode Ursache", optional "Ticketnummer" und
/// "Typ"), unabhängig von deren Reihenfolge oder zusätzlichen, nicht ausgewerteten
/// Spalten - das macht den Import robust gegenüber Tabellen, die "ähnlich" der
/// Beispieldatei, aber nicht spaltengleich sind.
/// </summary>
public sealed class ClosedXmlServiceTicketParser : IServiceTicketParser
{
    private const string TicketNumberColumn = "Ticketnummer";
    private const string CreatedAtColumn = "Anlagedatum";
    private const string CauseColumn = "Fehlercode Ursache";
    private const string TypeColumn = "Typ";
    private const string AddressLineColumn = "Adresszeile 1";
    private const string ErrorLocationColumn = "Fehlercode Ort";
    private const string ErrorFixColumn = "Fehlercode Behebung";
    private const string InternalStatusColumn = "Interner Status";
    private const string ResponsibleColumn = "Verantwortlich";

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
            // "Typ" ist bewusst optional: Dateien ohne diese Spalte sollen weiterhin
            // importierbar sein, der Typ-Filter zeigt dann schlicht keine Optionen an.
            var typeColumnIndex = columnIndexes.GetValueOrDefault(TypeColumn, -1);
            // Die folgenden Spalten werden nur für die Drill-Down-Tabelle der
            // Fehlerursachen benötigt und sind daher ebenfalls optional.
            var addressLineColumnIndex = columnIndexes.GetValueOrDefault(AddressLineColumn, -1);
            var errorLocationColumnIndex = columnIndexes.GetValueOrDefault(ErrorLocationColumn, -1);
            var errorFixColumnIndex = columnIndexes.GetValueOrDefault(ErrorFixColumn, -1);
            var internalStatusColumnIndex = columnIndexes.GetValueOrDefault(InternalStatusColumn, -1);
            var responsibleColumnIndex = columnIndexes.GetValueOrDefault(ResponsibleColumn, -1);

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

                string? type = null;
                if (typeColumnIndex > 0)
                {
                    var typeValue = row.Cell(typeColumnIndex).GetString().Trim();
                    type = string.IsNullOrWhiteSpace(typeValue) ? null : typeValue;
                }

                var addressLine = ReadOptionalString(row, addressLineColumnIndex);
                var errorLocation = ReadOptionalString(row, errorLocationColumnIndex);
                var errorFix = ReadOptionalString(row, errorFixColumnIndex);
                var internalStatus = ReadOptionalString(row, internalStatusColumnIndex);
                var responsible = ReadOptionalString(row, responsibleColumnIndex);

                tickets.Add(new ServiceTicket(
                    ticketNumber,
                    createdAt,
                    string.IsNullOrWhiteSpace(cause) ? null : cause,
                    type,
                    addressLine,
                    errorLocation,
                    errorFix,
                    internalStatus,
                    responsible));
            }

            _logger.LogInformation(
                "Excel-Datei {File} geladen: {Count} Tickets erkannt ({Skipped} Zeilen ohne gültiges Anlagedatum übersprungen).",
                Path.GetFileName(filePath), tickets.Count, skippedRows);

            return tickets;
        }
    }

    /// <summary>
    /// Liest eine optionale Textspalte; liefert <c>null</c>, wenn die Spalte in der
    /// Datei fehlt (columnIndex &lt;= 0) oder die Zelle leer ist.
    /// </summary>
    private static string? ReadOptionalString(IXLRow row, int columnIndex)
    {
        if (columnIndex <= 0)
        {
            return null;
        }

        var value = row.Cell(columnIndex).GetString().Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
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
