namespace ServiceApp.Core.Models;

/// <summary>
/// Fasst die Statistiken zweier Servicenummern für die Vergleichsansicht des Dashboards
/// zusammen. Reine Trägerklasse ohne eigene Berechnungslogik – die Werte stammen
/// unverändert aus <see cref="Statistics.IStatisticsService"/>.
/// </summary>
/// <param name="First">Statistik der ersten Servicenummer.</param>
/// <param name="Second">Statistik der zweiten Servicenummer.</param>
public sealed record ServiceNumberComparison(ServiceNumberStatistics First, ServiceNumberStatistics Second);
