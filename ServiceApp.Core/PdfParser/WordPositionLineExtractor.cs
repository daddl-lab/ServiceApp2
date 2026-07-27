using System.Text;
using UglyToad.PdfPig.Content;

namespace ServiceApp.Core.PdfParser;

/// <summary>
/// Rekonstruiert lesbare Textzeilen aus den Wortpositionen einer PDF-Seite, statt sich
/// auf PdfPigs reine Text-Extraktionsreihenfolge zu verlassen. Wörter werden anhand ihrer
/// vertikalen Position (Y-Koordinate) zu Zeilen gruppiert und innerhalb einer Zeile nach
/// horizontaler Position (X-Koordinate) sortiert. Das macht die Zeilenerkennung robust
/// gegenüber Tabellenlayouts, bei denen Spalten unterschiedlich breit sind oder PdfPig
/// Wörter nicht in der visuellen Lesereihenfolge liefert.
/// </summary>
public sealed class WordPositionLineExtractor
{
    /// <summary>
    /// Maximale Y-Abweichung (in PDF-Punkten), innerhalb derer zwei Wörter noch als
    /// derselben Zeile zugehörig gelten. Toleriert leichte Grundlinien-Schwankungen
    /// innerhalb einer Tabellenzeile.
    /// </summary>
    private const double LineYTolerance = 3.0;

    /// <summary>
    /// Horizontale Lücke (in PDF-Punkten), ab der zwei benachbarte Wörter als
    /// unterschiedliche Tabellenspalten statt als Teil desselben Textes gewertet werden.
    /// </summary>
    private const double ColumnGapThreshold = 8.0;

    /// <summary>
    /// Extrahiert alle Zeilen einer Seite, oben nach unten sortiert. Wörter derselben
    /// Zeile werden mit einem Leerzeichen verbunden, größere Spaltenlücken durch einen
    /// Tabulator markiert, damit nachgelagerte Interpreter Spaltenstrukturen erkennen
    /// können, ohne von exakten Koordinaten abhängig zu sein.
    /// </summary>
    public IReadOnlyList<string> ExtractLines(Page page)
    {
        var words = page.GetWords()
            .Select(w => new PositionedWord(w.Text, w.BoundingBox.Left, w.BoundingBox.Bottom, w.BoundingBox.Right))
            .OrderByDescending(w => w.Bottom)
            .ToList();

        var lines = new List<List<PositionedWord>>();
        foreach (var word in words)
        {
            var line = lines.Find(l => Math.Abs(l[0].Bottom - word.Bottom) <= LineYTolerance);
            if (line is null)
            {
                line = new List<PositionedWord>();
                lines.Add(line);
            }

            line.Add(word);
        }

        return lines.Select(BuildLineText).ToList();
    }

    private static string BuildLineText(List<PositionedWord> line)
    {
        var ordered = line.OrderBy(w => w.Left).ToList();
        var builder = new StringBuilder();

        for (var i = 0; i < ordered.Count; i++)
        {
            if (i > 0)
            {
                var gap = ordered[i].Left - ordered[i - 1].Right;
                builder.Append(gap > ColumnGapThreshold ? '\t' : ' ');
            }

            builder.Append(ordered[i].Text);
        }

        return builder.ToString();
    }

    private readonly record struct PositionedWord(string Text, double Left, double Bottom, double Right);
}
