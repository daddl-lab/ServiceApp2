namespace ServiceApp.Core.FileArchive;

/// <summary>
/// Standard-Implementierung von <see cref="IFileSystemWalker"/> über die
/// <see cref="System.IO.Directory"/>-API. Funktioniert unverändert für lokale Pfade wie für
/// UNC-Netzwerkpfade (<c>\\SERVER\Freigabe\...</c>) - .NET behandelt beide gleich.
///
/// Die Enumerationsmethoden werten das Ergebnis bewusst sofort vollständig aus
/// (<c>ToArray</c>) statt lazy zurückzugeben: <see cref="Directory.EnumerateFiles(string)"/>
/// wirft Fehler (z. B. bei einem Netzwerkabbruch während der Aufzählung) sonst erst beim
/// nächsten <c>MoveNext</c> im Aufrufer, außerhalb von dessen try/catch um den eigentlichen
/// Aufruf - das würde die pro-Verzeichnis-Fehlerisolation der Aufrufer (siehe
/// <see cref="FileArchiveIndexService"/>) durchbrechen.
/// </summary>
public sealed class LocalFileSystemWalker : IFileSystemWalker
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IEnumerable<string> EnumerateDirectories(string path)
        => Directory.EnumerateDirectories(path).ToArray();

    public IEnumerable<FileSystemFileInfo> EnumerateFiles(string path)
        => new DirectoryInfo(path)
            .EnumerateFiles()
            .Select(f => new FileSystemFileInfo(f.FullName, f.Name, f.Length, f.LastWriteTimeUtc))
            .ToArray();
}
