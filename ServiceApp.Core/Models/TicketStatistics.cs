namespace ServiceApp.Core.Models;

/// <summary>
/// Vollständig berechnetes Kennzahlen-Paket der Serviceticket-Auswertung für einen
/// bestimmten Zeitraum. Wird von <see cref="Statistics.ITicketStatisticsService"/>
/// erzeugt und ist die Datengrundlage für das Ticket-Dashboard.
/// </summary>
public sealed class TicketStatistics
{
    /// <summary>Gesamtzahl der Tickets im betrachteten Zeitraum.</summary>
    public int TotalTickets { get; init; }

    /// <summary>
    /// Zeitlicher Verlauf der Ticketanzahl (siehe <see cref="TicketTimeSeriesPoint"/> zur
    /// Erklärung der adaptiven Tages-/Monatsbündelung), aufsteigend sortiert.
    /// </summary>
    public IReadOnlyList<TicketTimeSeriesPoint> TimeSeries { get; init; } = Array.Empty<TicketTimeSeriesPoint>();

    /// <summary>
    /// Verteilung der Tickets nach Fehlerursache, absteigend nach Häufigkeit sortiert
    /// (Sammeleintrag "Sonstige" steht am Ende).
    /// </summary>
    public IReadOnlyList<TicketCauseCount> CauseBreakdown { get; init; } = Array.Empty<TicketCauseCount>();
}
