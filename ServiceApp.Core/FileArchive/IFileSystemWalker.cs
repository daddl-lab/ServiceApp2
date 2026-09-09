namespace ServiceApp.Core.FileArchive;

/// <summary>Leichtgewichtige Metadaten einer einzelnen im Archiv gefundenen Datei.</summary>
/// <param name="FullPath">Vollständiger Pfad der Datei.</param>
/// <param name="Name">Dateiname inklusive Endung.</param>
/// <param name="SizeBytes">Dateigröße in Bytes.</param>
/// <param name="LastWriteUtc">Letzter Änderungszeitpunkt (UTC).</param>
public sealed record FileSystemFileInfo(string FullPath, string Name, long SizeBytes, DateTime LastWriteUtc);

/// <summary>
/// Abstrahiert den Zugriff auf das Dateisystem für Indexierung und Live-Suche im
/// Zeichnungsarchiv. Durch dieses Interface können <see cref="FileArchiveIndexService"/>
/// und die Live-Suche in <see cref="FileArchiveSearchService"/> unabhängig von echten
/// UNC-Pfaden getestet werden (Fehlerinjektion über ein Testdouble, siehe
/// <c>FileArchiveIndexServiceTests</c>). Implementierungen geben Fehler beim Zugriff auf
/// ein einzelnes Verzeichnis oder eine einzelne Datei unverändert an den Aufrufer weiter -
/// die Isolation einzelner Fehler (ein defekter Ordner darf die restliche Suche/Indexierung
/// nicht abbrechen) liegt bewusst beim Aufrufer, analog zum bestehenden Muster in
/// <see cref="Repository.PdfCallRecordRepository"/>.
/// </summary>
public interface IFileSystemWalker
{
    /// <summary>Prüft, ob ein Verzeichnis existiert und erreichbar ist.</summary>
    bool DirectoryExists(string path);

    /// <summary>
    /// Liefert die unmittelbaren Unterverzeichnisse von <paramref name="path"/> (nicht rekursiv).
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">Fehlende Berechtigung für <paramref name="path"/>.</exception>
    /// <exception cref="IOException">Das Verzeichnis ist nicht erreichbar (z. B. Netzwerkfehler) oder ein anderer E/A-Fehler ist aufgetreten.</exception>
    IEnumerable<string> EnumerateDirectories(string path);

    /// <summary>
    /// Liefert die unmittelbar in <paramref name="path"/> enthaltenen Dateien (nicht rekursiv).
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">Fehlende Berechtigung für <paramref name="path"/>.</exception>
    /// <exception cref="IOException">Das Verzeichnis ist nicht erreichbar (z. B. Netzwerkfehler) oder ein anderer E/A-Fehler ist aufgetreten.</exception>
    IEnumerable<FileSystemFileInfo> EnumerateFiles(string path);
}
