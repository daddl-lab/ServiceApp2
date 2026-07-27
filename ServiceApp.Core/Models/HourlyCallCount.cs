namespace ServiceApp.Core.Models;

/// <summary>
/// Aggregierte Anrufzahl für eine einzelne Stunde des Tages (0–23), unabhängig vom
/// Kalendertag. Grundlage für die Stoßzeiten-/Tagesverteilungs-Anzeige.
/// </summary>
/// <param name="Hour">Stunde des Tages, 0–23.</param>
/// <param name="TotalCalls">Über den betrachteten Zeitraum aufsummierte Anrufe in dieser Stunde.</param>
public sealed record HourlyCallCount(int Hour, int TotalCalls);
