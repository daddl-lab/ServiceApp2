using System.Text;
using System.Text.RegularExpressions;
using ServiceApp.Core.Exceptions;

namespace ServiceApp.Core.FileArchive;

/// <summary>
/// Übersetzt einen Suchbegriff mit den Platzhaltern <c>*</c> (beliebig lange, auch leere,
/// Zeichenfolge) und <c>?</c> (genau ein beliebiges Zeichen) in ein kompiliertes, volles
/// Muster (<c>^...$</c> - kein "enthält"-Vergleich) und bietet darüber einen schnellen,
/// eindeutig definierten Abgleich. Ein Begriff ganz ohne Platzhalter matcht daher exakt;
/// Teiltreffer erfordern das explizite Muster <c>*Begriff*</c>. Da <c>*</c> und <c>?</c> in
/// echten Windows-Dateinamen ohnehin unzulässige Zeichen sind (siehe
/// <see cref="Path.GetInvalidFileNameChars"/>), gibt es keine reale Kollision mit einem
/// buchstäblich in einem Dateinamen vorkommenden <c>*</c> oder <c>?</c>, die eine
/// Escape-Syntax nötig gemacht hätte.
/// </summary>
public sealed class WildcardPattern
{
    private readonly Regex _regex;

    /// <summary>Der ursprüngliche, unveränderte Suchbegriff.</summary>
    public string Pattern { get; }

    /// <summary>Ob der Abgleich Groß-/Kleinschreibung berücksichtigt.</summary>
    public bool CaseSensitive { get; }

    /// <summary>Ob der Suchbegriff mindestens einen Platzhalter (<c>*</c> oder <c>?</c>) enthält.</summary>
    public bool ContainsWildcards { get; }

    private WildcardPattern(string pattern, bool caseSensitive, bool containsWildcards, Regex regex)
    {
        Pattern = pattern;
        CaseSensitive = caseSensitive;
        ContainsWildcards = containsWildcards;
        _regex = regex;
    }

    /// <summary>
    /// Erstellt ein kompiliertes Suchmuster aus einem Benutzer-Suchbegriff.
    /// </summary>
    /// <exception cref="InvalidSearchTermException">Der Suchbegriff ist leer oder besteht nur aus Leerzeichen.</exception>
    public static WildcardPattern Compile(string pattern, bool caseSensitive = false)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new InvalidSearchTermException("Bitte geben Sie einen Suchbegriff ein.");
        }

        var regexPattern = TranslateToRegexPattern(pattern, out var containsWildcards);
        var options = RegexOptions.CultureInvariant | RegexOptions.Compiled;
        if (!caseSensitive)
        {
            options |= RegexOptions.IgnoreCase;
        }

        return new WildcardPattern(pattern, caseSensitive, containsWildcards, new Regex(regexPattern, options));
    }

    /// <summary>Prüft, ob <paramref name="value"/> (üblicherweise ein Dateiname ohne Endung) vollständig auf das Muster passt.</summary>
    public bool IsMatch(string value) => _regex.IsMatch(value);

    /// <summary>
    /// Übersetzt einen Wildcard-Begriff in ein volles (<c>^...$</c>) Regex-Muster: literale
    /// Abschnitte werden per <see cref="Regex.Escape(string)"/> maskiert (in einem Zug pro
    /// zusammenhängendem Abschnitt, nicht zeichenweise, um unnötige Allokationen zu
    /// vermeiden), <c>*</c> wird zu <c>.*</c>, <c>?</c> wird zu <c>.</c>.
    /// </summary>
    internal static string TranslateToRegexPattern(string pattern, out bool containsWildcards)
    {
        var result = new StringBuilder(pattern.Length + 2).Append('^');
        var literalRun = new StringBuilder();
        containsWildcards = false;

        void FlushLiteralRun()
        {
            if (literalRun.Length == 0)
            {
                return;
            }

            result.Append(Regex.Escape(literalRun.ToString()));
            literalRun.Clear();
        }

        foreach (var c in pattern)
        {
            switch (c)
            {
                case '*':
                    FlushLiteralRun();
                    result.Append(".*");
                    containsWildcards = true;
                    break;
                case '?':
                    FlushLiteralRun();
                    result.Append('.');
                    containsWildcards = true;
                    break;
                default:
                    literalRun.Append(c);
                    break;
            }
        }

        FlushLiteralRun();
        return result.Append('$').ToString();
    }
}
