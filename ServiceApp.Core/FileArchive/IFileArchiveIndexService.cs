using ServiceApp.Core.Models;

namespace ServiceApp.Core.FileArchive;

/// <summary>
/// Orchestriert die Indexierung des Zeichnungsarchivs (siehe <see cref="FileArchiveIndexService"/>).
/// Voll- und inkrementelle Scans laufen seriell (nie überlappend); ein Fehler in einer
/// einzelnen Datei oder einem einzelnen Ordner bricht den Scan nicht ab, sondern wird
/// protokolliert und in der Fehlerzahl des Scan-Ergebnisses gezählt.
/// </summary>
public interface IFileArchiveIndexService
{
    /// <summary>Wird nach jedem erfolgreich abgeschlossenen Scan ausgelöst, damit z. B. die Suche ihren In-Memory-Snapshot aktualisieren kann.</summary>
    event Action? IndexUpdated;

    /// <summary>
    /// Durchsucht das gesamte Archiv von Grund auf und baut den Index vollständig neu auf.
    /// </summary>
    /// <exception cref="Exceptions.ArchiveRootNotFoundException">Der Archivpfad existiert nicht.</exception>
    /// <exception cref="Exceptions.ArchiveUnreachableException">Das Netzlaufwerk ist nicht erreichbar.</exception>
    Task RunFullScanAsync(string archiveRootPath, IProgress<ArchiveScanStatus>? progress, CancellationToken cancellationToken);

    /// <summary>
    /// Aktualisiert den Index: neue Dateien werden aufgenommen, geänderte aktualisiert,
    /// nicht mehr vorhandene entfernt. Unveränderte Dateien werden erkannt und nicht neu
    /// geschrieben (siehe <see cref="IFileArchiveIndexStore.TouchScanStamp"/>).
    /// </summary>
    /// <exception cref="Exceptions.ArchiveRootNotFoundException">Der Archivpfad existiert nicht.</exception>
    /// <exception cref="Exceptions.ArchiveUnreachableException">Das Netzlaufwerk ist nicht erreichbar.</exception>
    Task RunIncrementalScanAsync(string archiveRootPath, IProgress<ArchiveScanStatus>? progress, CancellationToken cancellationToken);

    /// <summary>Aktueller Status des Index.</summary>
    IndexStatus GetStatus();
}
