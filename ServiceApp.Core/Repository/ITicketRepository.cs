using ServiceApp.Core.Models;

namespace ServiceApp.Core.Repository;

/// <summary>
/// Lädt alle Servicetickets aus der konfigurierten Excel-Datei. Kapselt den
/// Dateizugriff, sodass die aufrufende Schicht (Statistik, ViewModels) nicht direkt mit
/// <see cref="TicketParser.IServiceTicketParser"/> arbeiten muss - analog zu
/// <see cref="ICallRecordRepository"/> für die PDF-Telefonberichte.
/// </summary>
public interface ITicketRepository
{
    /// <summary>Liest alle Servicetickets aus der angegebenen Excel-Datei ein.</summary>
    /// <exception cref="Exceptions.TicketFileNotFoundException">Die Datei existiert nicht.</exception>
    /// <exception cref="Exceptions.CorruptTicketFileException">Die Datei ist beschädigt.</exception>
    /// <exception cref="Exceptions.InvalidTicketFileFormatException">Erwartete Spalten fehlen.</exception>
    /// <exception cref="Exceptions.TicketFileAccessDeniedException">Fehlende Leseberechtigung.</exception>
    IReadOnlyList<ServiceTicket> GetTickets(string filePath);
}
