namespace ServiceApp.Core.Models;

/// <summary>
/// Anzahl der Tickets mit einer bestimmten Fehlerursache, für das Kuchendiagramm der
/// Serviceticket-Auswertung. Seltene Ursachen werden von
/// <see cref="Statistics.ITicketStatisticsService"/> zu einem Sammeleintrag "Sonstige"
/// zusammengefasst, damit das Diagramm bei vielen unterschiedlichen Ursachen (in der
/// Beispieldatei über 70) lesbar bleibt.
/// </summary>
/// <param name="Cause">Bezeichnung der Fehlerursache (oder "Sonstige"/"Nicht angegeben").</param>
/// <param name="Count">Anzahl der Tickets mit dieser Ursache.</param>
/// <param name="Tickets">
/// Die zu diesem Ursachen-Eintrag gehörenden Tickets, für die Drill-Down-Tabelle beim
/// Anklicken eines Kuchenstücks im Ticket-Dashboard.
/// </param>
public sealed record TicketCauseCount(string Cause, int Count, IReadOnlyList<ServiceTicket> Tickets);
