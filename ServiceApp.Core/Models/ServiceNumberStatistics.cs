namespace ServiceApp.Core.Models;

/// <summary>
/// Vollständig berechnetes Kennzahlen-Paket für eine Servicenummer über einen
/// bestimmten Zeitraum (siehe <see cref="Statistics.DateRangeFilter"/>). Wird von
/// <see cref="Statistics.IStatisticsService"/> erzeugt und ist die Datengrundlage für
/// das gesamte Dashboard einer einzelnen Servicenummer.
/// </summary>
public sealed class ServiceNumberStatistics
{
    /// <summary>Kennung der Servicenummer, für die diese Statistik gilt.</summary>
    public required string ServiceNumberId { get; init; }

    /// <summary>Anzeigename der Servicenummer.</summary>
    public required string ServiceNumberName { get; init; }

    /// <summary>Gesamtzahl aller Anrufe im betrachteten Zeitraum.</summary>
    public int TotalCalls { get; init; }

    /// <summary>Anzahl angenommener Anrufe im betrachteten Zeitraum.</summary>
    public int AnsweredCalls { get; init; }

    /// <summary>Anzahl verpasster Anrufe im betrachteten Zeitraum.</summary>
    public int MissedCalls { get; init; }

    /// <summary>Anteil angenommener Anrufe in Prozent.</summary>
    public double AnsweredPercent => TotalCalls == 0 ? 0 : AnsweredCalls * 100.0 / TotalCalls;

    /// <summary>Anteil verpasster Anrufe in Prozent.</summary>
    public double MissedPercent => TotalCalls == 0 ? 0 : MissedCalls * 100.0 / TotalCalls;

    /// <summary>Anrufzahlen je Kalendertag, aufsteigend nach Datum sortiert.</summary>
    public IReadOnlyList<DailyCallCount> DailyCounts { get; init; } = Array.Empty<DailyCallCount>();

    /// <summary>Anrufzahlen je Stunde (0–23), über den gesamten Zeitraum aufsummiert.</summary>
    public IReadOnlyList<HourlyCallCount> HourlyDistribution { get; init; } = Array.Empty<HourlyCallCount>();

    /// <summary>Durchschnittliche Anzahl Anrufe pro Tag (nur Tage mit mindestens einem Anruf werden gezählt).</summary>
    public double AverageCallsPerDay { get; init; }

    /// <summary>Der Tag mit den meisten Anrufen im Zeitraum, sofern Daten vorhanden sind.</summary>
    public DailyCallCount? BestDay { get; init; }

    /// <summary>Der Tag mit den wenigsten Anrufen im Zeitraum (aber mindestens einem Anruf), sofern Daten vorhanden sind.</summary>
    public DailyCallCount? WorstDay { get; init; }

    /// <summary>
    /// Durchschnittliche Annahmequote im Zeitraum, in Prozent. Nach Anrufvolumen
    /// gewichtet (entspricht <see cref="AnsweredPercent"/>) statt als ungewichteter
    /// Mittelwert der Tagesquoten, damit die Kennzahl bei schwankendem Anrufaufkommen
    /// nicht von der tatsächlichen Gesamtquote abweicht.
    /// </summary>
    public double AverageAnswerRatePercent => AnsweredPercent;
}
