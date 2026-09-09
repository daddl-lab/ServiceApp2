namespace ServiceApp.Core.FileArchive;

/// <summary>
/// Pfadsegment-Hilfsfunktionen, die bewusst sowohl <c>\</c> als auch <c>/</c> als
/// Trennzeichen behandeln, statt sich auf <see cref="Path.GetFileName(string)"/> bzw.
/// <see cref="Path.GetDirectoryName(string)"/> zu verlassen. Diese Standard-.NET-APIs
/// werten ausschließlich das trennzeichen der aktuell laufenden Plattform aus - unter
/// Windows also korrekt <c>\</c>, unter Linux (z. B. beim Ausführen der automatisierten
/// Tests dieser Anwendung) dagegen nur <c>/</c>. Da das Archiv ausschließlich über
/// Windows-UNC-Pfade (<c>\\server\freigabe\...</c>) angesprochen wird, die Anwendung aber
/// plattformunabhängig testbar sein soll, wird die Trennzeichen-Erkennung hier bewusst von
/// der Laufzeitplattform entkoppelt.
/// </summary>
internal static class ArchivePathUtilities
{
    private static readonly char[] Separators = { '\\', '/' };

    /// <summary>Letztes Pfadsegment (z. B. der Verzeichnisname eines Unterordners).</summary>
    public static string GetLastSegment(string path)
    {
        var trimmed = path.TrimEnd(Separators);
        var index = trimmed.LastIndexOfAny(Separators);
        return index >= 0 ? trimmed[(index + 1)..] : trimmed;
    }

    /// <summary>Übergeordneter Pfad (z. B. das Verzeichnis, in dem eine Datei liegt).</summary>
    public static string GetParentPath(string path)
    {
        var trimmed = path.TrimEnd(Separators);
        var index = trimmed.LastIndexOfAny(Separators);
        return index >= 0 ? trimmed[..index] : string.Empty;
    }
}
