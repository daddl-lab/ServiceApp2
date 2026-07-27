using Microsoft.Extensions.Logging;
using ServiceApp.Core.Exceptions;
using ServiceApp.Core.Models;
using UglyToad.PdfPig;

namespace ServiceApp.Core.PdfParser;

/// <summary>
/// Standard-Implementierung von <see cref="IPdfReportParser"/> auf Basis der
/// PdfPig-Bibliothek. Der eigentliche Text wird über <see cref="WordPositionLineExtractor"/>
/// positionsbasiert in Zeilen zerlegt; anschließend wird unter allen registrierten
/// <see cref="IReportLineInterpreter"/>-Implementierungen automatisch diejenige gewählt,
/// die die meisten Zeilen der Datei erfolgreich erkennt. Dadurch muss der Aufrufer das
/// konkrete Berichtsformat nicht kennen, und neue Formate lassen sich durch einfaches
/// Registrieren eines weiteren Interpreters unterstützen (siehe <see cref="IReportLineInterpreter"/>).
/// </summary>
public sealed class PdfPigReportParser : IPdfReportParser
{
    private readonly IReadOnlyList<IReportLineInterpreter> _interpreters;
    private readonly WordPositionLineExtractor _lineExtractor = new();
    private readonly ILogger<PdfPigReportParser> _logger;

    public PdfPigReportParser(IEnumerable<IReportLineInterpreter> interpreters, ILogger<PdfPigReportParser> logger)
    {
        _interpreters = interpreters.ToList();
        _logger = logger;

        if (_interpreters.Count == 0)
        {
            throw new ArgumentException("Mindestens ein IReportLineInterpreter muss registriert sein.", nameof(interpreters));
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<CallRecord> Parse(string filePath, string serviceNumberId)
    {
        if (!File.Exists(filePath))
        {
            throw new PdfNotFoundException(filePath);
        }

        IReadOnlyList<string> lines;
        try
        {
            lines = ExtractAllLines(filePath);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new PdfAccessDeniedException(filePath, ex);
        }
        catch (Exception ex) when (ex is not PdfImportException)
        {
            // PdfPig wirft je nach Defekt unterschiedliche Exception-Typen (ungültiger
            // Header, defekte Cross-Reference-Tabelle, ...) - all das zählt fachlich als
            // "beschädigtes PDF" und wird einheitlich gemeldet.
            throw new CorruptPdfException(filePath, ex);
        }

        var bestInterpreter = SelectBestInterpreter(lines);
        if (bestInterpreter is null)
        {
            throw new UnrecognizedReportFormatException(filePath);
        }

        var records = new List<CallRecord>();
        var fileName = Path.GetFileName(filePath);

        foreach (var line in lines)
        {
            foreach (var interpreted in bestInterpreter.Interpret(line))
            {
                records.Add(new CallRecord(
                    serviceNumberId,
                    interpreted.Timestamp,
                    interpreted.Status,
                    interpreted.DurationSeconds,
                    fileName));
            }
        }

        _logger.LogInformation(
            "PDF {File} geladen: {Count} Anrufe erkannt (Format: {Format}).",
            fileName, records.Count, bestInterpreter.FormatName);

        return records;
    }

    private List<string> ExtractAllLines(string filePath)
    {
        using var document = PdfDocument.Open(filePath);
        var allLines = new List<string>();

        foreach (var page in document.GetPages())
        {
            allLines.AddRange(_lineExtractor.ExtractLines(page));
        }

        return allLines;
    }

    /// <summary>
    /// Wählt aus allen registrierten Interpretern denjenigen, der die meisten Zeilen der
    /// Datei erfolgreich als Anrufdatensatz erkennt. Dieser "beste Treffer"-Ansatz macht
    /// die Formaterkennung robust: Kopf-/Fußzeilen oder vereinzelte Störzeilen, die von
    /// keinem Interpreter erkannt werden, verhindern nicht die Erkennung des restlichen
    /// Dokuments.
    /// </summary>
    private IReportLineInterpreter? SelectBestInterpreter(IReadOnlyList<string> lines)
    {
        IReportLineInterpreter? best = null;
        var bestMatchCount = 0;

        foreach (var interpreter in _interpreters)
        {
            var matchCount = lines.Count(line => interpreter.Interpret(line).Count > 0);
            if (matchCount > bestMatchCount)
            {
                bestMatchCount = matchCount;
                best = interpreter;
            }
        }

        return bestMatchCount > 0 ? best : null;
    }
}
