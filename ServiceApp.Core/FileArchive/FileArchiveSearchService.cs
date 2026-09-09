using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using ServiceApp.Core.Models;

namespace ServiceApp.Core.FileArchive;

/// <summary>
/// Standard-Implementierung von <see cref="IFileArchiveSearchService"/>.
///
/// Solange der Index bereit ist (<see cref="IndexStatus.IndexReady"/>), läuft die Suche
/// ausschließlich gegen einen In-Memory-Snapshot des lokalen SQLite-Index (siehe
/// <see cref="IFileArchiveIndexStore.LoadAllEntries"/>) - das Netzlaufwerk wird dabei
/// überhaupt nicht kontaktiert. Der Snapshot wird einmalig geladen und erst neu geladen,
/// wenn <see cref="IFileArchiveIndexService.IndexUpdated"/> signalisiert, dass sich der
/// Index verändert hat; dadurch bleiben wiederholte Suchen (z. B. während der Benutzer
/// tippt) durchgehend schnell.
///
/// Vor Abschluss des allerersten vollständigen Scans (unmittelbar nach dem Programmstart)
/// ist noch kein Index vorhanden. Damit auch die erste Suche nicht unnötig langsam ist,
/// wird in diesem Zeitfenster <see cref="ArchivePathResolver"/> genutzt, um den Suchbereich
/// anhand der bekannten Ordnerstruktur einzugrenzen und nur den so ermittelten (meist sehr
/// kleinen) Teilbaum direkt auf dem Netzlaufwerk zu durchsuchen. Lässt sich der Suchbegriff
/// nicht eingrenzen (siehe <see cref="ArchiveNarrowingKind.Unresolvable"/>), liefert die
/// Live-Suche bewusst keine Treffer, statt das gesamte Netzlaufwerk rekursiv zu durchsuchen -
/// das würde die geforderte geringe Netzlaufwerksbelastung verletzen. Die Oberfläche zeigt
/// in diesem Fall an, dass der Index noch aufgebaut wird.
/// </summary>
public sealed class FileArchiveSearchService : IFileArchiveSearchService
{
    private readonly IFileArchiveIndexStore _store;
    private readonly IFileArchiveIndexService _indexService;
    private readonly IFileSystemWalker _walker;
    private readonly ILogger<FileArchiveSearchService> _logger;
    private readonly object _snapshotLock = new();
    private IReadOnlyList<FileArchiveEntry>? _snapshot;

    public FileArchiveSearchService(
        IFileArchiveIndexStore store,
        IFileArchiveIndexService indexService,
        IFileSystemWalker walker,
        ILogger<FileArchiveSearchService> logger)
    {
        _store = store;
        _indexService = indexService;
        _walker = walker;
        _logger = logger;
        _indexService.IndexUpdated += InvalidateSnapshot;
    }

    private void InvalidateSnapshot()
    {
        lock (_snapshotLock)
        {
            _snapshot = null;
        }
    }

    public async IAsyncEnumerable<FileArchiveEntry> SearchAsync(
        ArchiveSearchQuery query,
        string archiveRootPath,
        IReadOnlyList<int> levelLengths,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var pattern = WildcardPattern.Compile(query.SearchTerm, query.CaseSensitive);
        var status = _indexService.GetStatus();

        var candidates = status.IndexReady
            ? SearchIndexed(pattern, query)
            : SearchLive(pattern, query, archiveRootPath, levelLengths, cancellationToken);

        var returned = 0;
        var processed = 0;
        foreach (var entry in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (returned >= query.MaxResults)
            {
                yield break;
            }

            yield return entry;
            returned++;

            // Regelmäßig die Kontrolle abgeben: hält die Suche kooperativ abbrechbar und
            // verhindert, dass eine lange Live-Suche (viele kleine Await-Punkte) den
            // aufrufenden Thread am Stück blockiert.
            if (++processed % 100 == 0)
            {
                await Task.Yield();
            }
        }
    }

    private IEnumerable<FileArchiveEntry> SearchIndexed(WildcardPattern pattern, ArchiveSearchQuery query)
    {
        foreach (var entry in GetOrLoadSnapshot())
        {
            if (query.FileType.MatchesExtension(entry.Extension) && pattern.IsMatch(entry.FileNameWithoutExtension))
            {
                yield return entry;
            }
        }
    }

    private IReadOnlyList<FileArchiveEntry> GetOrLoadSnapshot()
    {
        lock (_snapshotLock)
        {
            _snapshot ??= _store.LoadAllEntries();
            return _snapshot;
        }
    }

    private IEnumerable<FileArchiveEntry> SearchLive(
        WildcardPattern pattern, ArchiveSearchQuery query, string archiveRootPath, IReadOnlyList<int> levelLengths, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(archiveRootPath))
        {
            _logger.LogWarning("Live-Suche übersprungen: kein Archivpfad konfiguriert.");
            yield break;
        }

        var plan = ArchivePathResolver.Resolve(query.SearchTerm, levelLengths);

        switch (plan.Kind)
        {
            case ArchiveNarrowingKind.Exact:
            {
                var directory = CombinePath(archiveRootPath, plan.Level1!, plan.Level2!, plan.Level3!);
                if (!_walker.DirectoryExists(directory))
                {
                    yield break;
                }

                foreach (var entry in EnumerateFilesRecursively(directory, plan.Level1, plan.Level2, plan.Level3, pattern, query, cancellationToken))
                {
                    yield return entry;
                }

                break;
            }

            case ArchiveNarrowingKind.PartialLevel3:
            {
                var parent = CombinePath(archiveRootPath, plan.Level1!, plan.Level2!);
                foreach (var level3Directory in ResolveWildcardChildren(parent, plan.WildcardLevelNamePattern!, query.CaseSensitive))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var level3Name = ArchivePathUtilities.GetLastSegment(level3Directory);
                    foreach (var entry in EnumerateFilesRecursively(level3Directory, plan.Level1, plan.Level2, level3Name, pattern, query, cancellationToken))
                    {
                        yield return entry;
                    }
                }

                break;
            }

            case ArchiveNarrowingKind.PartialLevel2:
            {
                var parent = CombinePath(archiveRootPath, plan.Level1!);
                foreach (var level2Directory in ResolveWildcardChildren(parent, plan.WildcardLevelNamePattern!, query.CaseSensitive))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var level2Name = ArchivePathUtilities.GetLastSegment(level2Directory);
                    foreach (var entry in EnumerateFilesRecursively(level2Directory, plan.Level1, level2Name, null, pattern, query, cancellationToken))
                    {
                        yield return entry;
                    }
                }

                break;
            }

            case ArchiveNarrowingKind.Unresolvable:
            default:
                _logger.LogInformation(
                    "Live-Suche: Suchbegriff \"{Term}\" lässt sich vor Abschluss der Indexierung nicht auf einen Teilbereich eingrenzen; es werden noch keine Treffer geliefert.",
                    query.SearchTerm);
                yield break;
        }
    }

    /// <summary>
    /// Liefert die unmittelbaren Unterverzeichnisse von <paramref name="parentDirectory"/>,
    /// deren Name auf <paramref name="namePattern"/> passt (Verzeichnisnamen-Wildcard, siehe
    /// <see cref="ArchiveNarrowingPlan.WildcardLevelNamePattern"/>).
    /// </summary>
    private IEnumerable<string> ResolveWildcardChildren(string parentDirectory, string namePattern, bool caseSensitive)
    {
        if (!_walker.DirectoryExists(parentDirectory))
        {
            yield break;
        }

        IReadOnlyList<string> children;
        try
        {
            children = _walker.EnumerateDirectories(parentDirectory).ToList();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _logger.LogWarning(ex, "Live-Suche: Unterordner von \"{Directory}\" konnten nicht gelesen werden.", parentDirectory);
            yield break;
        }

        var childPattern = WildcardPattern.Compile(namePattern, caseSensitive);
        foreach (var child in children)
        {
            if (childPattern.IsMatch(ArchivePathUtilities.GetLastSegment(child)))
            {
                yield return child;
            }
        }
    }

    /// <summary>
    /// Durchsucht einen (durch die Ordnerstruktur-Eingrenzung typischerweise kleinen)
    /// Verzeichnisbaum rekursiv und live, mit derselben Fehlerisolation pro Ordner wie beim
    /// Index-Scan (siehe <see cref="FileArchiveIndexService"/>) - ein nicht lesbarer
    /// Unterordner überspringt nur sich selbst, nicht die restliche Live-Suche.
    /// </summary>
    private IEnumerable<FileArchiveEntry> EnumerateFilesRecursively(
        string directoryPath, string? level1, string? level2, string? level3,
        WildcardPattern filePattern, ArchiveSearchQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<FileSystemFileInfo> files;
        try
        {
            files = _walker.EnumerateFiles(directoryPath).ToList();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _logger.LogWarning(ex, "Live-Suche: Dateien in \"{Directory}\" konnten nicht gelesen werden.", directoryPath);
            files = Array.Empty<FileSystemFileInfo>();
        }

        foreach (var file in files)
        {
            var extension = Path.GetExtension(file.Name).TrimStart('.');
            if (!query.FileType.MatchesExtension(extension))
            {
                continue;
            }

            var nameWithoutExtension = Path.GetFileNameWithoutExtension(file.Name);
            if (!filePattern.IsMatch(nameWithoutExtension))
            {
                continue;
            }

            yield return new FileArchiveEntry(
                file.FullPath, file.Name, nameWithoutExtension, extension.ToLowerInvariant(),
                directoryPath, level1, level2, level3, file.SizeBytes, file.LastWriteUtc);
        }

        IReadOnlyList<string> subDirectories;
        try
        {
            subDirectories = _walker.EnumerateDirectories(directoryPath).ToList();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _logger.LogWarning(ex, "Live-Suche: Unterordner von \"{Directory}\" konnten nicht gelesen werden.", directoryPath);
            subDirectories = Array.Empty<string>();
        }

        foreach (var subDirectory in subDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();

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

            foreach (var entry in EnumerateFilesRecursively(subDirectory, nextLevel1, nextLevel2, nextLevel3, filePattern, query, cancellationToken))
            {
                yield return entry;
            }
        }
    }

    /// <summary>
    /// Fügt Archivwurzel und Ebenen-Namen zu einem echten Dateisystempfad zusammen. Nutzt
    /// bewusst <see cref="Path.Combine(string[])"/> statt eines fest verdrahteten
    /// Trennzeichens: Für den produktiven Windows-/UNC-Einsatz erzeugt das identische
    /// Backslash-getrennte Pfade, macht die Live-Suche aber gleichzeitig auf jeder
    /// Plattform korrekt, auf der <see cref="IFileSystemWalker"/> tatsächlich das
    /// Dateisystem abfragt (z. B. in den automatisierten Tests dieser Anwendung unter Linux).
    /// </summary>
    private static string CombinePath(string root, params string[] segments)
    {
        var all = new string[segments.Length + 1];
        all[0] = root;
        Array.Copy(segments, 0, all, 1, segments.Length);
        return Path.Combine(all);
    }
}
