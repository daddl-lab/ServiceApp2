namespace ServiceApp.Core.Models;

/// <summary>
/// Anzahl der Tickets mit einem bestimmten Störungsort (Excel-Spalte "Fehlercode
/// Ort"), für das zweite Kuchendiagramm der Serviceticket-Auswertung. Analog zu
/// <see cref="TicketCauseCount"/>, aber mit einer Top-X- statt einer
/// Prozent-Schwelle für die Zusammenfassung zu "Sonstige" (siehe
/// <see cref="Statistics.ITicketStatisticsService"/>), da die Anzahl der einzeln
/// gezeigten Störungsorte in den Einstellungen konfigurierbar ist.
/// </summary>
/// <param name="Location">Bezeichnung des Störungsorts (oder "Sonstige"/"Nicht angegeben").</param>
/// <param name="Count">Anzahl der Tickets mit diesem Störungsort.</param>
/// <param name="Tickets">
/// Die zu diesem Störungsort-Eintrag gehörenden Tickets, für die Drill-Down-Tabelle
/// beim Anklicken eines Kuchenstücks im Ticket-Dashboard.
/// </param>
public sealed record TicketErrorLocationCount(string Location, int Count, IReadOnlyList<ServiceTicket> Tickets);
