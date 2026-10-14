namespace ServiceApp.Core.Models;

/// <summary>
/// Anzahl der Tickets eines Kunden (Excel-Spalte "Adresszeile 1"), für die
/// Kundenauswertung des Ticket-Dashboards (Tabelle "Tickets pro Kunde", analog zu
/// einer Pivot-Tabelle mit Kundenname als Zeilenbeschriftung und Ticketanzahl als
/// Wert). Anders als bei <see cref="TicketCauseCount"/> und
/// <see cref="TicketErrorLocationCount"/> gibt es hier keinen Sammeleintrag
/// "Sonstige": überzählige Kunden werden schlicht nicht angezeigt (siehe
/// <see cref="Statistics.ITicketStatisticsService"/>).
/// </summary>
/// <param name="CustomerName">Inhalt der Spalte "Adresszeile 1" (oder "Nicht angegeben").</param>
/// <param name="Count">Anzahl der Tickets dieses Kunden.</param>
/// <param name="Tickets">Die zu diesem Kunden gehörenden Tickets.</param>
public sealed record TicketCustomerCount(string CustomerName, int Count, IReadOnlyList<ServiceTicket> Tickets);
