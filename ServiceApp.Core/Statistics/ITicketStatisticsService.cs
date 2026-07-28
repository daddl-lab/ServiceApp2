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
    /// <param name="tickets">Alle geladenen Tickets.</param>
    /// <param name="range">Zeitraum, auf den gefiltert wird.</param>
    /// <param name="selectedTypes">
    /// Werte der Spalte "Typ", auf die zusätzlich gefiltert werden soll (Mehrfachauswahl).
    /// <c>null</c> oder eine leere Menge bedeutet "kein Filter, alle Typen anzeigen".
    /// </param>
    TicketStatistics Compute(IReadOnlyList<ServiceTicket> tickets, DateRangeFilter range, IReadOnlyCollection<string>? selectedTypes = null);

    /// <summary>
    /// Ermittelt alle im Datenbestand vorkommenden Typ-Werte (Spalte "Typ"), alphabetisch
    /// sortiert, als Grundlage für den Mehrfachauswahl-Filter. Tickets ohne Typ-Angabe
    /// werden als "Nicht angegeben" geführt.
    /// </summary>
    IReadOnlyList<string> GetDistinctTypes(IReadOnlyList<ServiceTicket> tickets);
}
