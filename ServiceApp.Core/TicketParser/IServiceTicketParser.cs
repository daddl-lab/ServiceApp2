using ServiceApp.Core.Models;

namespace ServiceApp.Core.TicketParser;

/// <summary>
/// Liest eine Excel-Datei mit Servicetickets ein und wandelt ihren Inhalt in
/// <see cref="ServiceTicket"/>-Datensätze um. Zentrale Erweiterungsstelle für den
/// Excel-Import: eine alternative Implementierung (z. B. für ein abweichendes
/// Tabellenlayout) kann eingebunden werden, ohne den Rest der Anwendung (Repository,
/// Statistik, UI) zu verändern - analog zu <see cref="PdfParser.IPdfReportParser"/> für
/// den PDF-Import.
/// </summary>
public interface IServiceTicketParser
{
    /// <summary>
    /// Parst die angegebene Excel-Datei.
    /// </summary>
    /// <param name="filePath">Absoluter Pfad zur Excel-Datei.</param>
    /// <returns>Alle erkannten Servicetickets der Datei.</returns>
    /// <exception cref="Exceptions.TicketFileNotFoundException">Die Datei existiert nicht.</exception>
    /// <exception cref="Exceptions.CorruptTicketFileException">Die Datei ist beschädigt oder keine gültige Excel-Datei.</exception>
    /// <exception cref="Exceptions.InvalidTicketFileFormatException">Erwartete Spalten fehlen.</exception>
    /// <exception cref="Exceptions.TicketFileAccessDeniedException">Fehlende Leseberechtigung.</exception>
    IReadOnlyList<ServiceTicket> Parse(string filePath);
}
