using ServiceApp.Core.Models;

namespace ServiceApp.Core.FileArchive;

/// <summary>
/// Führt eine Suche im Zeichnungsarchiv aus (siehe <see cref="FileArchiveSearchService"/>).
/// Liefert Treffer als <see cref="IAsyncEnumerable{T}"/>, damit die Oberfläche Ergebnisse
/// anzeigen kann, sobald sie gefunden werden, statt auf den Abschluss der gesamten Suche
/// warten zu müssen.
/// </summary>
public interface IFileArchiveSearchService
{
    /// <summary>
    /// Durchsucht das Archiv gemäß <paramref name="query"/>. Solange der lokale Index noch
    /// nicht vollständig aufgebaut ist, wird ein anhand der Ordnerstruktur eingegrenzter
    /// Live-Zugriff auf <paramref name="archiveRootPath"/> verwendet (siehe
    /// <see cref="ArchivePathResolver"/>); danach läuft die Suche ausschließlich gegen den
    /// Index, ohne das Netzlaufwerk erneut zu belasten.
    /// </summary>
    /// <exception cref="Exceptions.InvalidSearchTermException">Der Suchbegriff ist ungültig (z. B. leer).</exception>
    IAsyncEnumerable<FileArchiveEntry> SearchAsync(
        ArchiveSearchQuery query,
        string archiveRootPath,
        IReadOnlyList<int> levelLengths,
        CancellationToken cancellationToken);
}
