namespace ServiceApp.Core.Models;

/// <summary>
/// Ein Punkt im zeitlichen Verlauf der Serviceticket-Auswertung. Je nach Länge des
/// gewählten Zeitraums bündelt ein Punkt entweder einen Kalendertag oder einen
/// Kalendermonat (siehe <see cref="Statistics.ITicketStatisticsService"/>), damit die
/// Diagrammachse bei langen Zeiträumen (z. B. "Jahr") nicht mit hunderten Tagespunkten
/// überladen wird.
/// </summary>
/// <param name="PeriodStart">Erster Tag des Buckets (Tag oder Monatserster).</param>
/// <param name="Label">Für die Diagrammachse aufbereitete Beschriftung.</param>
/// <param name="Count">Anzahl der Tickets in diesem Bucket.</param>
public sealed record TicketTimeSeriesPoint(DateOnly PeriodStart, string Label, int Count);
