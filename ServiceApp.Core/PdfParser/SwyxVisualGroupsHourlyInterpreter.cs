using System.Globalization;
using System.Text.RegularExpressions;
using ServiceApp.Core.Models;

namespace ServiceApp.Core.PdfParser;

/// <summary>
/// Interpretiert die "Zeitspanne"-Tabelle des Swyx-VisualGroups-Wochenberichts
/// ("Nutzung wöchentlich"). Anders als die übrigen Interpreter verarbeitet dieses
/// Format keine Einzelanruf-Zeilen, sondern **stundenweise aggregierte Zählwerte**,
/// z. B.:
/// <c>20-07-2026 | 06:00-07:00    3    0    3    0:00 min    0:00 min    0 %</c>
/// (Datum | Stundenintervall, Alle Anrufe, Angenommene Anrufe, Verpasste Anrufe,
/// Ø Sprechzeit, Ø Wartezeit, Servicelevel).
/// </summary>
/// <remarks>
/// Da pro Stunde nur Zählwerte und eine mittlere Sprechzeit vorliegen, keine
/// Einzelanrufe, werden für jede Zeile so viele <see cref="InterpretedLine"/>-Einträge
/// synthetisiert, wie die Spalten "Angenommene Anrufe" und "Verpasste Anrufe" angeben.
/// Alle synthetisierten Anrufe einer Zeile erhalten denselben Zeitstempel (Beginn des
/// Stundenintervalls) und - bei angenommenen Anrufen - dieselbe, aus "Ø Sprechzeit"
/// abgeleitete Näherungsdauer. Für alle im Dashboard geforderten Kennzahlen (Anzahl pro
/// Tag/Woche/Monat, Stoßzeiten nach Stunde, Annahmequote) ist das exakt, da diese
/// ausschließlich auf Stunde/Tag und Status aggregieren; nur die genaue Minute
/// innerhalb der Stunde und individuelle Gesprächsdauern sind aus dieser Quelle nicht
/// rekonstruierbar.
/// </remarks>
public sealed class SwyxVisualGroupsHourlyInterpreter : IReportLineInterpreter
{
    private static readonly Regex RowRegex = new(
        @"(\d{2})-(\d{2})-(\d{4})\s*\|\s*(\d{1,2}):(\d{2})-\d{1,2}:\d{2}\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+):(\d{2})\s*min",
        RegexOptions.Compiled);

    public string FormatName => "Swyx VisualGroups Stundenbericht (TT-MM-JJJJ, Stundenintervalle)";

    /// <inheritdoc />
    public IReadOnlyList<InterpretedLine> Interpret(string line)
    {
        var match = RowRegex.Match(line);
        if (!match.Success)
        {
            return Array.Empty<InterpretedLine>();
        }

        var day = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var month = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var year = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
        var hour = int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);
        var minute = int.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture);

        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month) || hour > 23 || minute > 59)
        {
            return Array.Empty<InterpretedLine>();
        }

        var answeredCalls = int.Parse(match.Groups[7].Value, CultureInfo.InvariantCulture);
        var missedCalls = int.Parse(match.Groups[8].Value, CultureInfo.InvariantCulture);

        if (answeredCalls == 0 && missedCalls == 0)
        {
            return Array.Empty<InterpretedLine>();
        }

        var timestamp = new DateTime(year, month, day, hour, minute, 0);

        var avgTalkMinutes = int.Parse(match.Groups[9].Value, CultureInfo.InvariantCulture);
        var avgTalkSeconds = int.Parse(match.Groups[10].Value, CultureInfo.InvariantCulture);
        var avgDurationSeconds = avgTalkMinutes * 60 + avgTalkSeconds;
        int? answeredDuration = avgDurationSeconds > 0 ? avgDurationSeconds : null;

        var results = new List<InterpretedLine>(answeredCalls + missedCalls);

        for (var i = 0; i < answeredCalls; i++)
        {
            results.Add(new InterpretedLine(timestamp, CallStatus.Answered, answeredDuration));
        }

        for (var i = 0; i < missedCalls; i++)
        {
            results.Add(new InterpretedLine(timestamp, CallStatus.Missed, null));
        }

        return results;
    }
}
