namespace ServiceApp.Desktop.ViewModels;

/// <summary>
/// Steuert, welche der beiden Ticket-Verteilungen (Fehlerursachen oder Störungsorte)
/// im gemeinsamen Kuchendiagramm des Ticket-Dashboards angezeigt wird (siehe
/// <see cref="TicketDashboardViewModel"/>).
/// </summary>
public enum TicketBreakdownViewMode
{
    /// <summary>Verteilung nach Fehlerursache (Standard).</summary>
    Cause,

    /// <summary>Verteilung nach Störungsort.</summary>
    ErrorLocation
}
