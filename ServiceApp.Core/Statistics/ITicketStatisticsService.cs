using ServiceApp.Core.Models;

namespace ServiceApp.Core.Statistics;

/// <summary>
/// Berechnet die im Ticket-Dashboard benötigten Kennzahlen (zeitlicher Verlauf,
/// Fehlerursachen-Verteilung) aus den rohen Servicetickets. Analog zu
/// <see cref="IStatisticsService"/> für die Telefonberichte, aber unabhängig davon, da
/// Servicetickets ein eigenes Datenmodell mit eigenen Auswertungsfragen sind.
/// </summary>
public interface ITicketStatisticsService
{
    /// <summary>
    /// Berechnet die Statistik für den angegebenen Zeitraum. Tickets außerhalb des
    /// Zeitraums (nach <see cref="ServiceTicket.CreatedAt"/>) werden ignoriert.
    /// </summary>
    TicketStatistics Compute(IReadOnlyList<ServiceTicket> tickets, DateRangeFilter range);
}
