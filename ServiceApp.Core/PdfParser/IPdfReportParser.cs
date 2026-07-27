using ServiceApp.Core.Models;

namespace ServiceApp.Core.PdfParser;

/// <summary>
/// Liest eine einzelne Telefonbericht-PDF-Datei und wandelt ihren Inhalt in
/// <see cref="CallRecord"/>-Datensätze um. Dies ist die zentrale Erweiterungsstelle
/// für den PDF-Import: eine alternative oder zusätzliche Implementierung (z. B. für ein
/// komplett anderes Berichtsformat oder einen anderen PDF-Anbieter) kann eingebunden
/// werden, ohne den Rest der Anwendung (Repository, Statistik, UI) zu verändern.
/// </summary>
public interface IPdfReportParser
{
    /// <summary>
    /// Parst die angegebene PDF-Datei.
    /// </summary>
    /// <param name="filePath">Absoluter Pfad zur PDF-Datei.</param>
    /// <param name="serviceNumberId">
    /// Kennung der Servicenummer, der diese Datei zugeordnet ist. Wird in jeden erzeugten
    /// <see cref="CallRecord"/> übernommen, damit spätere Auswertungen die Herkunft kennen.
    /// </param>
    /// <returns>Alle erkannten Anrufdatensätze der Datei, in beliebiger Reihenfolge.</returns>
    /// <exception cref="Exceptions.PdfNotFoundException">Die Datei existiert nicht.</exception>
    /// <exception cref="Exceptions.CorruptPdfException">Die Datei ist beschädigt oder kein gültiges PDF.</exception>
    /// <exception cref="Exceptions.UnrecognizedReportFormatException">Kein registrierter Interpreter erkennt das Format.</exception>
    /// <exception cref="Exceptions.PdfAccessDeniedException">Fehlende Leseberechtigung.</exception>
    IReadOnlyList<CallRecord> Parse(string filePath, string serviceNumberId);
}
