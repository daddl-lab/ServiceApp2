using Microsoft.Extensions.Logging.Abstractions;
using ServiceApp.Core.Exceptions;
using ServiceApp.Core.FileArchive;
using ServiceApp.Core.Models;
using Xunit;

namespace ServiceApp.Tests.FileSearchTests;

/// <summary>
/// End-to-End-Abnahmetest: verdrahtet exakt dieselben Bausteine wie die produktive
/// Anwendung (<c>ServiceCollectionExtensions</c>) - echter <see cref="LocalFileSystemWalker"/>,
/// echte <see cref="FileArchiveIndexStore"/>-Datenbank, echter Verzeichnisbaum auf Platte -
/// und durchläuft dabei den vollständigen Ablauf, den ein Benutzer erlebt: Archiv mit
/// mehreren Dateitypen und Sonderfällen anlegen, initial durchsuchen (Cold-Start-Live-Suche
/// vor Indexbereitschaft), vollständig indexieren, danach gegen den Index suchen
/// (exakt/Wildcard/Dateityp-Filter/kein Treffer), eine Datei ändern und umbenennen,
/// inkrementell aktualisieren und die Korrektur im Suchergebnis verifizieren.
/// </summary>
public sealed class EndToEndScenarioTests : IDisposable
{
    private readonly string _archiveRoot;
    private readonly string _databasePath;

    public EndToEndScenarioTests()
    {
        _archiveRoot = Path.Combine(Path.GetTempPath(), $"ServiceAppE2EArchive_{Guid.NewGuid()}");
        _databasePath = Path.Combine(Path.GetTempPath(), $"ServiceAppE2EIndex_{Guid.NewGuid()}", "index.db");
        Directory.CreateDirectory(_archiveRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_archiveRoot))
        {
            Directory.Delete(_archiveRoot, recursive: true);
        }

        var databaseDirectory = Path.GetDirectoryName(_databasePath);
        if (databaseDirectory is not null && Directory.Exists(databaseDirectory))
        {
            Directory.Delete(databaseDirectory, recursive: true);
        }
    }

    private static void CreateDrawing(string directory, string fileName)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, fileName), "synthetic drawing content");
    }

    private static async Task<List<FileArchiveEntry>> CollectAsync(IAsyncEnumerable<FileArchiveEntry> source)
    {
        var results = new List<FileArchiveEntry>();
        await foreach (var entry in source)
        {
            results.Add(entry);
        }

        return results;
    }

    [Fact]
    public async Task FullLifecycle_FromEmptyArchiveToIndexedIncrementalUpdate_BehavesCorrectlyEndToEnd()
    {
        // 1. Archiv mit typischer 2/3/4-Ebenen-Struktur, mehreren Dateitypen, einer Datei
        //    mit Sonderzeichen/Leerzeichen im Namen und einer für den Filter irrelevanten
        //    Datei (falsche Endung) anlegen.
        var leaf = Path.Combine(_archiveRoot, "12", "345", "6789");
        CreateDrawing(leaf, "123456789_A.pdf");
        CreateDrawing(leaf, "123456789_A.tif");
        CreateDrawing(leaf, "123456789_B (Revision 2).dxf");
        CreateDrawing(leaf, "notes.txt"); // nicht unterstützte Endung - darf nicht indexiert werden
        var otherLeaf = Path.Combine(_archiveRoot, "99", "888", "7777");
        CreateDrawing(otherLeaf, "999888777.pdf");

        var store = new FileArchiveIndexStore(NullLogger<FileArchiveIndexStore>.Instance, _databasePath);
        store.Initialize();
        var walker = new LocalFileSystemWalker();
        var indexService = new FileArchiveIndexService(store, walker, NullLogger<FileArchiveIndexService>.Instance);
        var searchService = new FileArchiveSearchService(store, indexService, walker, NullLogger<FileArchiveSearchService>.Instance);
        var levels = ArchivePathResolver.DefaultLevelLengths;

        // 2. Vor jeglicher Indexierung: eine exakte Suche muss dank Ordnerstruktur-Eingrenzung
        //    trotzdem sofort den richtigen Treffer liefern (Cold-Start-Fallback).
        var coldStartResults = await CollectAsync(searchService.SearchAsync(
            new ArchiveSearchQuery("123456789_A", FileTypeFilter.Pdf, CaseSensitive: false, MaxResults: 100),
            _archiveRoot, levels, CancellationToken.None));
        Assert.Single(coldStartResults);

        // 3. Vollständige Indexierung.
        await indexService.RunFullScanAsync(_archiveRoot, progress: null, CancellationToken.None);
        var status = indexService.GetStatus();
        Assert.True(status.IndexReady);
        Assert.Equal(4, status.TotalFileCount); // 3 unterstützte + 1 in anderem Ast, "notes.txt" zählt nicht mit

        // 4. Wildcard-Suche über den gesamten Index, alle Dateitypen.
        var wildcardResults = await CollectAsync(searchService.SearchAsync(
            new ArchiveSearchQuery("123456789*", FileTypeFilter.All, CaseSensitive: false, MaxResults: 100),
            _archiveRoot, levels, CancellationToken.None));
        Assert.Equal(3, wildcardResults.Count);

        // 5. Dateityp-Filter grenzt auf die DXF-Datei mit Sonderzeichen/Leerzeichen im Namen ein.
        var dxfResults = await CollectAsync(searchService.SearchAsync(
            new ArchiveSearchQuery("123456789_B*", FileTypeFilter.Dxf, CaseSensitive: false, MaxResults: 100),
            _archiveRoot, levels, CancellationToken.None));
        var dxfEntry = Assert.Single(dxfResults);
        Assert.Equal("123456789_B (Revision 2).dxf", dxfEntry.FileName);

        // 6. Suche ohne Treffer liefert eine leere, aber fehlerfreie Ergebnisliste.
        var noResults = await CollectAsync(searchService.SearchAsync(
            new ArchiveSearchQuery("does-not-exist-anywhere", FileTypeFilter.All, CaseSensitive: false, MaxResults: 100),
            _archiveRoot, levels, CancellationToken.None));
        Assert.Empty(noResults);

        // 7. Eine Datei umbenennen, eine weitere hinzufügen, dann inkrementell aktualisieren.
        File.Move(Path.Combine(leaf, "123456789_A.tif"), Path.Combine(leaf, "123456789_A_rev2.tif"));
        CreateDrawing(leaf, "123456789_C.xlsx");
        await indexService.RunIncrementalScanAsync(_archiveRoot, progress: null, CancellationToken.None);

        var afterUpdate = await CollectAsync(searchService.SearchAsync(
            new ArchiveSearchQuery("123456789*", FileTypeFilter.All, CaseSensitive: false, MaxResults: 100),
            _archiveRoot, levels, CancellationToken.None));
        Assert.Equal(4, afterUpdate.Count); // A.pdf, A_rev2.tif (umbenannt), B (Revision 2).dxf, C.xlsx
        Assert.Contains(afterUpdate, e => e.FileName == "123456789_A_rev2.tif");
        Assert.DoesNotContain(afterUpdate, e => e.FileName == "123456789_A.tif"); // alter Name verschwunden

        // 8. Ein nicht erreichbarer Archivpfad wird als solcher erkannt, statt eine leere
        //    Ergebnisliste vorzutäuschen.
        await Assert.ThrowsAsync<ArchiveRootNotFoundException>(
            () => indexService.RunFullScanAsync(Path.Combine(_archiveRoot, "existiert-nicht"), progress: null, CancellationToken.None));

        store.Dispose();
    }

    [Fact]
    public async Task CorruptedIndexFile_IsTransparentlyRebuiltAndRemainsFullyFunctional()
    {
        CreateDrawing(Path.Combine(_archiveRoot, "12", "345", "6789"), "123456789.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
        await File.WriteAllTextAsync(_databasePath, "dies ist keine gueltige sqlite datenbank");

        var store = new FileArchiveIndexStore(NullLogger<FileArchiveIndexStore>.Instance, _databasePath);
        store.Initialize(); // muss die defekte Datei selbst erkennen, verwerfen und neu aufbauen

        var walker = new LocalFileSystemWalker();
        var indexService = new FileArchiveIndexService(store, walker, NullLogger<FileArchiveIndexService>.Instance);
        var searchService = new FileArchiveSearchService(store, indexService, walker, NullLogger<FileArchiveSearchService>.Instance);

        await indexService.RunFullScanAsync(_archiveRoot, progress: null, CancellationToken.None);
        var results = await CollectAsync(searchService.SearchAsync(
            new ArchiveSearchQuery("123456789", FileTypeFilter.All, CaseSensitive: false, MaxResults: 10),
            _archiveRoot, ArchivePathResolver.DefaultLevelLengths, CancellationToken.None));

        Assert.Single(results);
        store.Dispose();
    }
}
