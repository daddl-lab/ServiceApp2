using ServiceApp.Core.Models;

namespace ServiceApp.Core.FileArchive;

/// <summary>
/// Persistiert den Zeichnungsarchiv-Suchindex lokal (siehe <see cref="FileArchiveIndexStore"/>
/// für die SQLite-Implementierung inkl. Mark-and-Sweep-Aktualisierung und Selbstheilung bei
/// Beschädigung). <see cref="FileArchive.FileArchiveIndexService"/> orchestriert damit
/// Voll- und inkrementelle Scans, <see cref="FileArchive.FileArchiveSearchService"/> lädt
/// darüber den durchsuchbaren Snapshot.
/// </summary>
public interface IFileArchiveIndexStore : IDisposable
{
    /// <summary>
    /// Stellt sicher, dass Schema und Datenbankdatei existieren und lesbar sind. Bei
    /// Beschädigung (fehlerhafte Datei, fehlgeschlagener Integritätscheck) wird die
    /// Datenbank automatisch verworfen und leer neu angelegt.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Lädt für alle aktuell indexierten Dateien Größe und Änderungszeitpunkt, damit ein
    /// inkrementeller Scan unveränderte Dateien erkennen kann, ohne für jede Datei einzeln
    /// die Datenbank abzufragen.
    /// </summary>
    IReadOnlyDictionary<string, (long SizeBytes, DateTime LastWriteUtc)> LoadExistingFileStamps();

    /// <summary>
    /// Beginnt einen neuen Scan-Vorgang (voll oder inkrementell): vergibt eine neue,
    /// monoton steigende Scan-Kennung ("Generation") und öffnet eine Transaktion, in der
    /// alle nachfolgenden <see cref="UpsertFile"/>/<see cref="TouchScanStamp"/>/
    /// <see cref="SweepStale"/>-Aufrufe gebündelt werden, bis <see cref="CompleteScan"/>
    /// sie committet.
    /// </summary>
    long BeginScan();

    /// <summary>Fügt eine Datei ein oder aktualisiert sie, markiert mit der aktuellen Scan-Kennung.</summary>
    void UpsertFile(FileArchiveEntry entry, long scanStamp);

    /// <summary>
    /// Markiert eine unverändert gebliebene, bereits bekannte Datei als in diesem Scan
    /// gesehen, ohne ihre übrigen Spalten neu zu schreiben - reduziert das Schreibvolumen
    /// bei inkrementellen Aktualisierungen erheblich.
    /// </summary>
    void TouchScanStamp(string fullPath, long scanStamp);

    /// <summary>
    /// Entfernt alle Dateien unterhalb von <paramref name="scopeRootPath"/>, die im
    /// aktuellen Scan (<paramref name="scanStamp"/>) nicht mehr gesehen wurden - erkennt
    /// damit gelöschte und umbenannte Dateien (eine Umbenennung erscheint als Löschung des
    /// alten und Hinzufügen des neuen Pfads). Der Geltungsbereich verhindert, dass ein auf
    /// einen Teilbaum begrenzter Scan fälschlich Zeilen außerhalb seines Bereichs löscht.
    /// </summary>
    /// <returns>Anzahl der entfernten Zeilen.</returns>
    int SweepStale(string scopeRootPath, long scanStamp);

    /// <summary>Schließt den Scan ab: committet die Transaktion und aktualisiert die Index-Statistik.</summary>
    void CompleteScan(long scanStamp, bool wasFullScan, long totalFileCount, long errorCount, TimeSpan duration);

    /// <summary>Verwirft einen begonnenen, nicht abgeschlossenen Scan (Rollback), z. B. nach einem fatalen Fehler.</summary>
    void AbortScan();

    /// <summary>Lädt den gesamten Index als Snapshot für die In-Memory-Suche.</summary>
    IReadOnlyList<FileArchiveEntry> LoadAllEntries();

    /// <summary>Aktueller Status des Index (siehe <see cref="IndexStatus"/>).</summary>
    IndexStatus GetStatus();
}
