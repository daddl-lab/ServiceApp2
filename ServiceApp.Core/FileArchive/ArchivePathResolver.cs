using ServiceApp.Core.Models;

namespace ServiceApp.Core.FileArchive;

/// <summary>
/// Rein funktionale Logik, die aus einem Suchbegriff ableitet, wie weit sich die Suche
/// anhand der bekannten Ordnerstruktur des Archivs eingrenzen lässt - ohne selbst auf das
/// Dateisystem zuzugreifen (siehe <see cref="ArchiveNarrowingPlan"/>, dadurch ohne
/// temporäre Verzeichnisse testbar). Die ersten drei Verzeichnisebenen des Archivs
/// entsprechen dem Anfang des Suchbegriffs, standardmäßig 2/3/4 Zeichen
/// (z. B. "123456789" → "12\345\6789"); die tatsächlichen Ebenen-Längen sind über
/// <see cref="AppSettings.FileArchiveLevelLengths"/> konfigurierbar, falls die reale
/// Archivstruktur davon abweicht.
///
/// Strategie: Der literale Präfix des Suchbegriffs (der Teil vor dem ersten <c>*</c> oder
/// <c>?</c>) wird anhand der Ebenen-Längen zerlegt. Reicht er über alle drei Ebenen, ist
/// das Zielverzeichnis exakt bekannt. Fällt der erste Platzhalter in eine Ebene, wird für
/// genau diese Ebene ein Verzeichnisnamen-Muster gebildet (das der Aufrufer gegen die
/// tatsächlichen Unterordner abgleicht), tiefere Ebenen bleiben offen. Reicht der literale
/// Präfix nicht einmal bis zum Ende von Ebene 1 (z. B. führender Platzhalter, zu kurzer
/// Begriff, Sonderzeichen, die in Verzeichnisnamen ohnehin unzulässig wären), ist keine
/// Eingrenzung möglich.
/// </summary>
public static class ArchivePathResolver
{
    /// <summary>Standard-Ebenen-Längen, entsprechend dem in der Aufgabenstellung beschriebenen Beispiel (2/3/4 Zeichen).</summary>
    public static readonly IReadOnlyList<int> DefaultLevelLengths = new[] { 2, 3, 4 };

    /// <summary>
    /// In Windows-Verzeichnisnamen unzulässige Zeichen (Leerzeichen bleibt bewusst
    /// erlaubt, da Ordner- und Dateinamen im Archiv laut Aufgabenstellung Leerzeichen
    /// enthalten dürfen). Bewusst fest hinterlegt statt <see cref="Path.GetInvalidFileNameChars"/>
    /// zu verwenden: Diese .NET-API liefert plattformabhängig unterschiedliche
    /// Zeichensätze, die Zeichenarchiv-Anwendung läuft aber ausschließlich gegen
    /// Windows-UNC-Pfade - die Prüfung muss daher unabhängig davon greifen, auf welcher
    /// Plattform gebaut oder getestet wird. <c>*</c> und <c>?</c> sind bewusst nicht
    /// enthalten, da sie als Platzhalter ohnehin nie Teil des literalen Präfixes sind.
    /// </summary>
    private static readonly char[] WindowsInvalidPathSegmentChars = BuildInvalidPathSegmentChars();

    private static char[] BuildInvalidPathSegmentChars()
    {
        var chars = new List<char>();
        for (var c = (char)0; c < (char)32; c++)
        {
            chars.Add(c);
        }

        chars.AddRange(new[] { '"', '<', '>', '|', ':', '\\', '/' });
        return chars.ToArray();
    }

    /// <summary>
    /// Bestimmt anhand von <paramref name="searchTerm"/>, welcher Teil der 3-Ebenen-Ordnerstruktur
    /// bereits literal bekannt ist.
    /// </summary>
    /// <param name="searchTerm">Der vom Benutzer eingegebene Suchbegriff (mit oder ohne Platzhalter).</param>
    /// <param name="levelLengths">Zeichenlängen der drei Ordnerstruktur-Ebenen, in dieser Reihenfolge.</param>
    public static ArchiveNarrowingPlan Resolve(string searchTerm, IReadOnlyList<int> levelLengths)
    {
        if (levelLengths.Count != 3 || levelLengths.Any(length => length <= 0))
        {
            throw new ArgumentException("Es werden genau drei positive Ebenen-Längen erwartet.", nameof(levelLengths));
        }

        if (string.IsNullOrEmpty(searchTerm))
        {
            return ArchiveNarrowingPlan.Unresolvable;
        }

        var literalPrefixLength = 0;
        while (literalPrefixLength < searchTerm.Length
               && searchTerm[literalPrefixLength] != '*'
               && searchTerm[literalPrefixLength] != '?')
        {
            literalPrefixLength++;
        }

        for (var i = 0; i < literalPrefixLength; i++)
        {
            if (Array.IndexOf(WindowsInvalidPathSegmentChars, searchTerm[i]) >= 0)
            {
                return ArchiveNarrowingPlan.Unresolvable;
            }
        }

        var level1Length = levelLengths[0];
        var level2Length = levelLengths[1];
        var level3Length = levelLengths[2];
        var cumulativeLevel1 = level1Length;
        var cumulativeLevel2 = cumulativeLevel1 + level2Length;
        var cumulativeLevel3 = cumulativeLevel2 + level3Length;

        if (literalPrefixLength >= cumulativeLevel3)
        {
            var level1 = searchTerm.Substring(0, level1Length);
            var level2 = searchTerm.Substring(cumulativeLevel1, level2Length);
            var level3 = searchTerm.Substring(cumulativeLevel2, level3Length);
            return new ArchiveNarrowingPlan(ArchiveNarrowingKind.Exact, level1, level2, level3, null);
        }

        if (literalPrefixLength >= cumulativeLevel2)
        {
            var level1 = searchTerm.Substring(0, level1Length);
            var level2 = searchTerm.Substring(cumulativeLevel1, level2Length);
            var level3Pattern = BuildLevelNamePattern(searchTerm, cumulativeLevel2, level3Length, cumulativeLevel3);
            return new ArchiveNarrowingPlan(ArchiveNarrowingKind.PartialLevel3, level1, level2, null, level3Pattern);
        }

        if (literalPrefixLength >= cumulativeLevel1)
        {
            var level1 = searchTerm.Substring(0, level1Length);
            var level2Pattern = BuildLevelNamePattern(searchTerm, cumulativeLevel1, level2Length, cumulativeLevel2);
            return new ArchiveNarrowingPlan(ArchiveNarrowingKind.PartialLevel2, level1, null, null, level2Pattern);
        }

        return ArchiveNarrowingPlan.Unresolvable;
    }

    /// <summary>
    /// Baut aus dem Rest des Suchbegriffs ab <paramref name="start"/> ein Verzeichnisnamen-Muster
    /// für eine Ebene der Länge <paramref name="levelLength"/>. Reicht der Suchbegriff nicht bis
    /// zum Ende der Ebene, wird ein trailing <c>*</c> ergänzt (fehlende Zeichen gelten als
    /// "unbekannt", nicht als "leer").
    /// </summary>
    private static string BuildLevelNamePattern(string searchTerm, int start, int levelLength, int cumulativeEnd)
    {
        var availableLength = Math.Min(levelLength, Math.Max(0, searchTerm.Length - start));
        var slice = availableLength > 0 ? searchTerm.Substring(start, availableLength) : string.Empty;

        if (searchTerm.Length < cumulativeEnd && !slice.EndsWith('*'))
        {
            slice += "*";
        }

        return slice;
    }
}
