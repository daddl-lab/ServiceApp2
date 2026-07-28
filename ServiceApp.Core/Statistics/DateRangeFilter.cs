namespace ServiceApp.Core.Statistics;

/// <summary>Voreingestellter Zeitraumtyp, wie er im Dashboard-Filter auswählbar ist.</summary>
public enum DateRangePreset
{
    Today,
    ThisWeek,
    ThisMonth,
    ThisYear,
    Custom
}

/// <summary>
/// Kapselt einen Zeitraum (inklusive Start- und Enddatum), über den Anrufstatistiken
/// berechnet werden. Bündelt die Logik zur Ermittlung von "Heute"/"Diese Woche"/"Dieser
/// Monat" an einer einzigen Stelle, damit diese Berechnung nicht mehrfach im Code
/// dupliziert wird (u. a. in ViewModels).
/// </summary>
/// <param name="Start">Erster Tag des Zeitraums (inklusive).</param>
/// <param name="End">Letzter Tag des Zeitraums (inklusive).</param>
/// <param name="Preset">Der zugrunde liegende Filtertyp.</param>
public sealed record DateRangeFilter(DateOnly Start, DateOnly End, DateRangePreset Preset)
{
    /// <summary>Der aktuelle Kalendertag.</summary>
    public static DateRangeFilter Today(DateOnly? referenceDate = null)
    {
        var today = referenceDate ?? DateOnly.FromDateTime(DateTime.Now);
        return new DateRangeFilter(today, today, DateRangePreset.Today);
    }

    /// <summary>
    /// Die aktuelle Kalenderwoche, beginnend mit dem übergebenen Wochenstart
    /// (standardmäßig Montag, wie im deutschsprachigen Raum üblich).
    /// </summary>
    public static DateRangeFilter ThisWeek(DateOnly? referenceDate = null, DayOfWeek firstDayOfWeek = DayOfWeek.Monday)
    {
        var today = referenceDate ?? DateOnly.FromDateTime(DateTime.Now);
        var offset = ((int)today.DayOfWeek - (int)firstDayOfWeek + 7) % 7;
        var start = today.AddDays(-offset);
        var end = start.AddDays(6);
        return new DateRangeFilter(start, end, DateRangePreset.ThisWeek);
    }

    /// <summary>Der aktuelle Kalendermonat, vom ersten bis zum letzten Tag.</summary>
    public static DateRangeFilter ThisMonth(DateOnly? referenceDate = null)
    {
        var today = referenceDate ?? DateOnly.FromDateTime(DateTime.Now);
        var start = new DateOnly(today.Year, today.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        return new DateRangeFilter(start, end, DateRangePreset.ThisMonth);
    }

    /// <summary>Das aktuelle Kalenderjahr, vom 1. Januar bis zum 31. Dezember.</summary>
    public static DateRangeFilter ThisYear(DateOnly? referenceDate = null)
    {
        var today = referenceDate ?? DateOnly.FromDateTime(DateTime.Now);
        var start = new DateOnly(today.Year, 1, 1);
        var end = new DateOnly(today.Year, 12, 31);
        return new DateRangeFilter(start, end, DateRangePreset.ThisYear);
    }

    /// <summary>Ein vom Benutzer frei gewählter Zeitraum.</summary>
    public static DateRangeFilter CustomRange(DateOnly start, DateOnly end)
    {
        if (end < start)
        {
            (start, end) = (end, start);
        }

        return new DateRangeFilter(start, end, DateRangePreset.Custom);
    }

    /// <summary>Prüft, ob das übergebene Datum innerhalb dieses Zeitraums liegt.</summary>
    public bool Contains(DateOnly date) => date >= Start && date <= End;

    /// <summary>Alle Kalendertage dieses Zeitraums, aufsteigend sortiert.</summary>
    public IEnumerable<DateOnly> EnumerateDays()
    {
        for (var day = Start; day <= End; day = day.AddDays(1))
        {
            yield return day;
        }
    }
}
