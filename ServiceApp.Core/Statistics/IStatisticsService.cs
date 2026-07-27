using ServiceApp.Core.Models;

namespace ServiceApp.Core.Statistics;

/// <summary>
/// Berechnet alle im Dashboard benötigten Kennzahlen aus den rohen Anrufdatensätzen
/// einer Servicenummer. Trennt die reine Berechnungslogik vollständig von PDF-Import und
/// Darstellung, damit Kennzahlen unabhängig von der Datenquelle und ohne UI getestet
/// werden können.
/// </summary>
public interface IStatisticsService
{
    /// <summary>
    /// Berechnet die vollständige Statistik einer Servicenummer für den angegebenen
    /// Zeitraum. Datensätze außerhalb des Zeitraums werden ignoriert.
    /// </summary>
    ServiceNumberStatistics Compute(
        string serviceNumberId,
        string serviceNumberName,
        IReadOnlyList<CallRecord> records,
        DateRangeFilter range);

    /// <summary>Stellt zwei bereits berechnete Statistiken für die Vergleichsansicht nebeneinander.</summary>
    ServiceNumberComparison Compare(ServiceNumberStatistics first, ServiceNumberStatistics second);
}
