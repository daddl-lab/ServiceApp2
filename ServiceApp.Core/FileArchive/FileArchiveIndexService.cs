using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ServiceApp.Core.Exceptions;
using ServiceApp.Core.Models;

namespace ServiceApp.Core.FileArchive;

/// <summary>
/// Standard-Implementierung von <see cref="IFileArchiveIndexService"/>. Läuft rekursiv über
/// die Ordnerstruktur des Archivs (unabhängig von der konfigurierten Ebenen-Länge - die
/// Ebenen-1/2/3-Zuordnung ergibt sich einfach aus der tatsächlichen Verzeichnistiefe, nicht
/// aus geparsten Zeichen) und isoliert Fehler pro Datei bzw. Ordner analog zum bestehenden
/// Muster in <see cref="Repository.PdfCallRecordRepository"/>: ein nicht lesbarer Unterordner
/// wird protokolliert und übersprungen, der Scan der übrigen Struktur läuft weiter.
///
/// Sowohl der volle als auch der inkrementelle Scan durchlaufen dieselbe rekursive
/// Verzeichnisauflistung - ein <see cref="System.IO.FileSystemWatcher"/> auf dem
/// Netzlaufwerk wird bewusst nicht verwendet, da SMB-Freigaben Änderungsereignisse
/// nachweislich unzuverlässig melden (stille Pufferüberläufe, inkonsistentes Verhalten je
/// nach Server) und ein scheinbarer Nahezu-Echtzeit-Abgleich, der gelegentlich lautlos
/// Änderungen verpasst, schlimmer wäre als ein einfacher periodischer Abgleich. Der
/// Unterschied zwischen "voll" und "inkrementell" liegt daher auf Datenbankebene: ein
/// inkrementeller Scan erkennt unveränderte Dateien (gleiche Größe und Änderungszeitpunkt)
/// anhand eines vorab geladenen Snapshots und schreibt sie nicht neu
/// (<see cref="IFileArchiveIndexStore.TouchScanStamp"/>), was das Schreibvolumen bei
/// üblicherweise wenigen tatsächlichen Änderungen drastisch reduziert. Das Netzlaufwerk
/// selbst wird dabei nur mit reinen Verzeichnis-/Metadaten-Abfragen belastet (nie mit dem
/// Lesen von Dateiinhalten) und läuft standardmäßig nur einmal pro konfiguriertem Intervall
/// (Default: stündlich), nicht bei jeder Benutzersuche.
/// </summary>
public sealed class FileArchiveIndexService : IFileArchiveIndexService
{
    private readonly IFileArchiveIndexStore _store;
    private readonly IFileSystemWalker _walker;
    private readonly ILogger<FileArchiveIndexService> _logger;
    private readonly SemaphoreSlim _scanLock = new(1, 1);

    public event Action? IndexUpdated;

    public FileArchiveIndexService(IFileArchiveIndexStore store, IFileSystemWalker walker, ILogger<FileArchiveIndexService> logger)
    {
        _store = store;
        _walker = walker;
        _logger = logger;
    }

    public Task RunFullScanAsync(string archiveRootPath, IProgress<ArchiveScanStatus>? progress, CancellationToken cancellationToken)
        => RunScanAsync(archiveRootPath, wasFullScan: true, progress, cancellationToken);

    public Task RunIncrementalScanAsync(string archiveRootPath, IProgress<ArchiveScanStatus>? progress, CancellationToken cancellationToken)
        => RunScanAsync(archiveRootPath, wasFullScan: false, progress, cancellationToken);

    public IndexStatus GetStatus() => _store.GetStatus();

    private async Task RunScanAsync(string archiveRootPath, bool wasFullScan, IProgress<ArchiveScanStatus>? progress, CancellationToken cancellationToken)
    {
        await _scanLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Run(() => ScanCore(archiveRootPath, wasFullScan, progress, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _scanLock.Release();
        }
    }

    private void ScanCore(string archiveRootPath, bool wasFullScan, IProgress<ArchiveScanStatus>? progress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(archiveRootPath))
        {
            throw new ArchiveRootNotFoundException(archiveRootPath);
        }

        bool rootExists;
        try
        {
            rootExists = _walker.DirectoryExists(archiveRootPath);
        }
        catch (IOException ex)
        {
            throw new ArchiveUnreachableException(archiveRootPath, ex);
        }

        if (!rootExists)
        {
            throw new ArchiveRootNotFoundException(archiveRootPath);
        }

        var stopwatch = Stopwatch.StartNew();
        var existingStamps = _store.LoadExistingFileStamps();
        var scanStamp = _store.BeginScan();
        var context = new ScanContext(scanStamp, existingStamps, progress, cancellationToken);

        try
        {
            WalkDirectory(archiveRootPath, null, null, null, context);
            cancellationToken.ThrowIfCancellationRequested();

            var removedCount = _store.SweepStale(archiveRootPath, scanStamp);
            stopwatch.Stop();
            _store.CompleteScan(scanStamp, wasFullScan, context.RelevantFilesSeen, context.ErrorCount, stopwatch.Elapsed);

            _logger.LogInformation(
                "{Kind}-Scan von \"{Root}\" abgeschlossen: {Indexed} Dateien im Index, {Removed} entfernt, {Errors} übersprungene Elemente, Dauer {DurationMs} ms.",
                wasFullScan ? "Voll" : "Inkrementell", archiveRootPath, context.RelevantFilesSeen, removedCount, context.ErrorCount, (long)stopwatch.Elapsed.TotalMilliseconds);

            IndexUpdated?.Invoke();
        }
        catch (OperationCanceledException)
        {
            _store.AbortScan();
            _logger.LogInformation("{Kind}-Scan von \"{Root}\" wurde abgebrochen.", wasFullScan ? "Voll" : "Inkrementell", archiveRootPath);
            throw;
        }
        catch (Exception ex)
        {
            _store.AbortScan();
            _logger.LogError(ex, "{Kind}-Scan von \"{Root}\" ist fehlgeschlagen.", wasFullScan ? "Voll" : "Inkrementell", archiveRootPath);
            throw;
        }
    }

    /// <summary>
    /// Rekursive Verzeichnisauflistung. <paramref name="level1"/>/<paramref name="level2"/>/
    /// <paramref name="level3"/> sind die Namen der ersten drei tatsächlichen
    /// Verzeichnisebenen relativ zur Archivwurzel (nicht deren Zeichenlänge) - bleiben
    /// <c>null</c>, solange die entsprechende Tiefe noch nicht erreicht ist, und werden ab
    /// Ebene 4 nicht mehr vertieft (eine Datei tiefer als drei Ebenen behält die Werte ihrer
    /// obersten drei Vorfahren-Ordner).
    /// </summary>
    private void WalkDirectory(string directoryPath, string? level1, string? level2, string? level3, ScanContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<FileSystemFileInfo> files;
        try
        {
            files = _walker.EnumerateFiles(directoryPath).ToList();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _logger.LogWarning(ex, "Dateien in Ordner \"{Directory}\" konnten nicht gelesen werden und werden übersprungen.", directoryPath);
            context.ErrorCount++;
            files = Array.Empty<FileSystemFileInfo>();
        }

        foreach (var file in files)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            ProcessFile(file, level1, level2, level3, context);
        }

        IReadOnlyList<string> subDirectories;
        try
        {
            subDirectories = _walker.EnumerateDirectories(directoryPath).ToList();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _logger.LogWarning(ex, "Unterordner von \"{Directory}\" konnten nicht gelesen werden und werden übersprungen.", directoryPath);
            context.ErrorCount++;
            subDirectories = Array.Empty<string>();
        }

        foreach (var subDirectory in subDirectories)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            var name = ArchivePathUtilities.GetLastSegment(subDirectory);
            string? nextLevel1 = level1;
            string? nextLevel2 = level2;
            string? nextLevel3 = level3;
            if (level1 is null)
            {
                nextLevel1 = name;
            }
            else if (level2 is null)
            {
                nextLevel2 = name;
            }
            else if (level3 is null)
            {
                nextLevel3 = name;
            }

            context.ItemsProcessed++;
            if (context.ItemsProcessed % 200 == 0)
            {
                context.Progress?.Report(new ArchiveScanStatus(subDirectory, context.ItemsProcessed));
            }

            WalkDirectory(subDirectory, nextLevel1, nextLevel2, nextLevel3, context);
        }
    }

    private void ProcessFile(FileSystemFileInfo file, string? level1, string? level2, string? level3, ScanContext context)
    {
        var extension = Path.GetExtension(file.Name).TrimStart('.');
        if (!FileTypeFilterExtensions.AllSupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        context.RelevantFilesSeen++;

        if (context.ExistingStamps.TryGetValue(file.FullPath, out var existing)
            && existing.SizeBytes == file.SizeBytes
            && existing.LastWriteUtc == file.LastWriteUtc)
        {
            _store.TouchScanStamp(file.FullPath, context.ScanStamp);
            return;
        }

        try
        {
            var entry = new FileArchiveEntry(
                FullPath: file.FullPath,
                FileName: file.Name,
                FileNameWithoutExtension: Path.GetFileNameWithoutExtension(file.Name),
                Extension: extension.ToLowerInvariant(),
                DirectoryPath: ArchivePathUtilities.GetParentPath(file.FullPath),
                Level1: level1,
                Level2: level2,
                Level3: level3,
                SizeBytes: file.SizeBytes,
                LastWriteUtc: file.LastWriteUtc);

            _store.UpsertFile(entry, context.ScanStamp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Datei \"{File}\" konnte nicht in den Index aufgenommen werden.", file.FullPath);
            context.ErrorCount++;
        }
    }

    private sealed class ScanContext
    {
        public ScanContext(long scanStamp, IReadOnlyDictionary<string, (long SizeBytes, DateTime LastWriteUtc)> existingStamps,
            IProgress<ArchiveScanStatus>? progress, CancellationToken cancellationToken)
        {
            ScanStamp = scanStamp;
            ExistingStamps = existingStamps;
            Progress = progress;
            CancellationToken = cancellationToken;
        }

        public long ScanStamp { get; }
        public IReadOnlyDictionary<string, (long SizeBytes, DateTime LastWriteUtc)> ExistingStamps { get; }
        public IProgress<ArchiveScanStatus>? Progress { get; }
        public CancellationToken CancellationToken { get; }
        public long ItemsProcessed { get; set; }
        public long RelevantFilesSeen { get; set; }
        public long ErrorCount { get; set; }
    }
}
