using System.Text.RegularExpressions;
using ServiceApp.Core.Models;

namespace ServiceApp.Core.PdfParser;

/// <summary>
/// Gemeinsame Basislogik für zeilenbasierte, regex-getriebene Report-Interpreter.
/// Kapselt die Erkennung von Uhrzeiten und Gesprächsdauer, die in allen unterstützten
/// Tabellenformaten identisch aufgebaut sind (<c>HH:MM</c> oder <c>HH:MM:SS</c>); nur die
/// Erkennung des Datums und der Status-Schlüsselwörter unterscheidet sich je Format und
/// wird von den abgeleiteten Klassen bereitgestellt.
/// </summary>
/// <remarks>
/// Annahme (da keine realen Musterberichte vorlagen): Eine Zeile enthält nach dem Datum
/// mindestens eine Uhrzeit (Anrufzeitpunkt) und optional eine zweite, kürzere Zeitangabe
/// für die Gesprächsdauer. Taucht in einer Zeile nur eine Zeitangabe auf, wird sie als
/// Anrufzeitpunkt gewertet und keine Dauer erkannt. Diese Heuristik lässt sich in den
/// abgeleiteten Klassen bei Bedarf für ein reales Berichtsformat verfeinern, ohne dass
/// <see cref="PdfPigReportParser"/> angepasst werden muss.
/// </remarks>
public abstract class RegexTableLineInterpreterBase : IReportLineInterpreter
{
    private static readonly Regex TimeRegex = new(
        @"\b([01]?\d|2[0-3]):([0-5]\d)(?::([0-5]\d))?\b",
        RegexOptions.Compiled);

    /// <inheritdoc />
    public abstract string FormatName { get; }

    /// <summary>Versucht, das im jeweiligen Format erwartete Datum aus der Zeile zu extrahieren.</summary>
    protected abstract bool TryExtractDate(string line, out DateOnly date, out int matchLength, out int matchIndex);

    /// <summary>
    /// Ermittelt den Anrufstatus anhand der in der Zeile enthaltenen Schlüsselwörter.
    /// Gibt <c>null</c> zurück, wenn kein bekanntes Status-Schlüsselwort gefunden wurde.
    /// </summary>
    protected abstract CallStatus? DetermineStatus(string line);

    /// <inheritdoc />
    public IReadOnlyList<InterpretedLine> Interpret(string line)
    {
        if (!TryExtractDate(line, out var date, out _, out _))
        {
            return Array.Empty<InterpretedLine>();
        }

        var timeMatches = TimeRegex.Matches(line);
        if (timeMatches.Count == 0)
        {
            return Array.Empty<InterpretedLine>();
        }

        var status = DetermineStatus(line);
        if (status is null)
        {
            return Array.Empty<InterpretedLine>();
        }

        var callTimeMatch = timeMatches[0];
        var callTime = ParseTimeOfDay(callTimeMatch);
        var timestamp = date.ToDateTime(callTime);

        int? durationSeconds = timeMatches.Count > 1
            ? ParseDurationSeconds(timeMatches[1])
            : null;

        return new[] { new InterpretedLine(timestamp, status.Value, durationSeconds) };
    }

    private static TimeOnly ParseTimeOfDay(Match match)
    {
        var hour = int.Parse(match.Groups[1].Value);
        var minute = int.Parse(match.Groups[2].Value);
        var second = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
        return new TimeOnly(hour, minute, second);
    }

    private static int ParseDurationSeconds(Match match)
    {
        var first = int.Parse(match.Groups[1].Value);
        var second = int.Parse(match.Groups[2].Value);
        var third = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : (int?)null;

        return third.HasValue
            ? first * 3600 + second * 60 + third.Value
            : first * 60 + second;
    }
}
