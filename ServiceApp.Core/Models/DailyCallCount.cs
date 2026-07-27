namespace ServiceApp.Core.Models;

/// <summary>
/// Aggregierte Anrufzahlen für einen einzelnen Kalendertag. Grundbaustein für die
/// Zeitverlaufs-Diagramme (Tag/Woche/Monat werden aus dieser Tagesauflösung hochgerechnet).
/// </summary>
/// <param name="Date">Der Kalendertag (ohne Uhrzeitanteil).</param>
/// <param name="TotalCalls">Gesamtzahl aller Anrufe an diesem Tag.</param>
/// <param name="AnsweredCalls">Anzahl der angenommenen Anrufe an diesem Tag.</param>
/// <param name="MissedCalls">Anzahl der verpassten Anrufe an diesem Tag.</param>
public sealed record DailyCallCount(DateOnly Date, int TotalCalls, int AnsweredCalls, int MissedCalls)
{
    /// <summary>Annahmequote dieses Tages in Prozent (0, wenn keine Anrufe vorlagen).</summary>
    public double AnswerRatePercent => TotalCalls == 0 ? 0 : AnsweredCalls * 100.0 / TotalCalls;
}
