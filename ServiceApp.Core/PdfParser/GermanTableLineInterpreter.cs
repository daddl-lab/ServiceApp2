using System.Globalization;
using System.Text.RegularExpressions;
using ServiceApp.Core.Models;

namespace ServiceApp.Core.PdfParser;

/// <summary>
/// Interpretiert Berichtszeilen im deutschen Tabellenformat mit Datum als
/// <c>TT.MM.JJJJ</c>, z. B.:
/// <c>27.07.2026  09:15:32  Angenommen  00:03:12</c>.
/// Status-Erkennung basiert auf deutschen Schlüsselwörtern; "nicht angenommen" wird vor
/// "angenommen" geprüft, da Letzteres als Teilstring in Ersterem enthalten ist.
/// </summary>
public sealed class GermanTableLineInterpreter : RegexTableLineInterpreterBase
{
    private static readonly Regex DateRegex = new(@"\b(\d{2})\.(\d{2})\.(\d{4})\b", RegexOptions.Compiled);

    private static readonly string[] MissedKeywords =
    {
        "nicht angenommen", "verpasst", "besetzt", "abgebrochen", "aufgelegt", "keine antwort"
    };

    private static readonly string[] AnsweredKeywords =
    {
        "angenommen", "beantwortet", "erledigt"
    };

    public override string FormatName => "Deutsches Tabellenformat (TT.MM.JJJJ)";

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

        var day = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var month = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var year = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);

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
