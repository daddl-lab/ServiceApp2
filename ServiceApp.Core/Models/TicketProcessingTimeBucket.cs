namespace ServiceApp.Core.Models;

/// <summary>
/// Anzahl abgeschlossener Tickets (Excel-Spalte "Externer Status" = "Abgeschlossen")
/// in einem bestimmten Bearbeitszeit-Bereich (Letzte Änderung − Anlagedatum, in
/// Tagen), für das Bearbeitszeit-Balkendiagramm des Ticket-Dashboards. Anders als
/// bei <see cref="TicketCauseCount"/> und <see cref="TicketErrorLocationCount"/>
/// ist die Reihenfolge der Buckets fest vorgegeben (aufsteigende Bearbeitszeit,
/// siehe <see cref="Statistics.ITicketStatisticsService"/>) statt nach Häufigkeit
/// sortiert.
/// </summary>
/// <param name="Label">Bezeichnung des Bearbeitszeit-Bereichs (z. B. "1 Tag", "2 Wochen", "Rest").</param>
/// <param name="Count">Anzahl der Tickets in diesem Bearbeitszeit-Bereich.</param>
/// <param name="Tickets">
/// Die zu diesem Bearbeitszeit-Bereich gehörenden Tickets, für die Drill-Down-Tabelle
/// beim Anklicken eines Balkens im Ticket-Dashboard.
/// </param>
public sealed record TicketProcessingTimeBucket(string Label, int Count, IReadOnlyList<ServiceTicket> Tickets);
