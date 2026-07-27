using Microsoft.Extensions.Logging;
using ServiceApp.Core.Exceptions;
using ServiceApp.Core.Models;
using ServiceApp.Core.PdfParser;

namespace ServiceApp.Core.Repository;

/// <summary>
/// Standard-Implementierung von <see cref="ICallRecordRepository"/>: liest alle
/// <c>*.pdf</c>-Dateien im konfigurierten Ordner ein und aggregiert deren
/// Anrufdatensätze. Fehler in einer einzelnen Datei (beschädigt, unbekanntes Format)
/// unterbrechen nicht den Import der übrigen Dateien - sie werden stattdessen als
/// <see cref="PdfImportWarning"/> gesammelt und zurückgegeben.
/// </summary>
public sealed class PdfCallRecordRepository : ICallRecordRepository
{
    private readonly IPdfReportParser _parser;
    private readonly ILogger<PdfCallRecordRepository> _logger;

    public PdfCallRecordRepository(IPdfReportParser parser, ILogger<PdfCallRecordRepository> logger)
    {
        _parser = parser;
        _logger = logger;
    }

    /// <inheritdoc />
    public CallRecordImportResult GetCallRecords(ServiceNumberSettings serviceNumber)
    {
        if (string.IsNullOrWhiteSpace(serviceNumber.PdfFolderPath) || !Directory.Exists(serviceNumber.PdfFolderPath))
        {
            throw new PdfFolderNotFoundException(serviceNumber.PdfFolderPath);
        }

        string[] pdfFiles;
        try
        {
            pdfFiles = Directory.GetFiles(serviceNumber.PdfFolderPath, "*.pdf", SearchOption.TopDirectoryOnly);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new PdfAccessDeniedException(serviceNumber.PdfFolderPath, ex);
        }

        if (pdfFiles.Length == 0)
        {
            _logger.LogWarning(
                "Keine PDF-Dateien im Ordner {Folder} für Servicenummer {ServiceNumber} gefunden.",
                serviceNumber.PdfFolderPath, serviceNumber.Name);
            return new CallRecordImportResult(Array.Empty<CallRecord>(), Array.Empty<PdfImportWarning>());
        }

        var records = new List<CallRecord>();
        var warnings = new List<PdfImportWarning>();

        foreach (var file in pdfFiles.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                records.AddRange(_parser.Parse(file, serviceNumber.Id));
            }
            catch (PdfImportException ex)
            {
                _logger.LogError(ex, "Fehler beim Einlesen von {File}: {Message}", file, ex.Message);
                warnings.Add(new PdfImportWarning(Path.GetFileName(file), ex.Message));
            }
        }

        return new CallRecordImportResult(records, warnings);
    }
}
