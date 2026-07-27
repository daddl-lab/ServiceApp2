namespace ServiceApp.Desktop.ViewModels;

/// <summary>
/// Kompakte, für die Vergleichsansicht des Dashboards aufbereitete Zusammenfassung
/// einer Servicenummer. Reine Anzeige-Projektion von
/// <see cref="ServiceApp.Core.Models.ServiceNumberStatistics"/> - enthält keine eigene
/// Berechnungslogik.
/// </summary>
public sealed record ServiceNumberSummary(string Name, int Total, int Answered, int Missed, double AnswerRatePercent)
{
    public static ServiceNumberSummary Empty(string name) => new(name, 0, 0, 0, 0);
}
