using System.Globalization;
using System.Text.RegularExpressions;
using ServiceApp.Core.Models;

namespace ServiceApp.Core.PdfParser;

/// <summary>
/// Interpretiert Berichtszeilen im ISO-Tabellenformat mit Datum als <c>JJJJ-MM-TT</c>,
/// z. B.: <c>2026-07-27 09:15:32 answered 00:03:12</c>. Status-Erkennung basiert auf
/// englischen Schlüsselwörtern, wie sie viele Telefonanlagen-Exporte verwenden.
/// </summary>
public sealed class IsoTableLineInterpreter : RegexTableLineInterpreterBase
{
    private static readonly Regex DateRegex = new(@"\b(\d{4})-(\d{2})-(\d{2})\b", RegexOptions.Compiled);

    private static readonly string[] MissedKeywords =
    {
        "missed", "no answer", "not answered", "busy", "abandoned", "unanswered"
    };

    private static readonly string[] AnsweredKeywords =
    {
        "answered", "completed", "connected"
    };

    public override string FormatName => "ISO-Tabellenformat (JJJJ-MM-TT)";

    protected override bool TryExtractDate(string line, out DateOnly date, out int matchLength, out int matchIndex)
    {
        var match = DateRegex.Match(line);
        if (!match.Success)
        {
            date = default;
            matchLength = 0;
            matchIndex = -1;
            return false;
        }

        var year = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var month = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var day = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);

        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            date = default;
            matchLength = 0;
            matchIndex = -1;
            return false;
        }

        date = new DateOnly(year, month, day);
        matchLength = match.Length;
        matchIndex = match.Index;
        return true;
    }

    protected override CallStatus? DetermineStatus(string line)
    {
        var lower = line.ToLowerInvariant();

        foreach (var keyword in MissedKeywords)
        {
            if (lower.Contains(keyword))
            {
                return CallStatus.Missed;
            }
        }

        foreach (var keyword in AnsweredKeywords)
        {
            if (lower.Contains(keyword))
            {
                return CallStatus.Answered;
            }
        }

        return null;
    }
}
