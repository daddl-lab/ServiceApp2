using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using ServiceApp.Core.FileArchive;
using ServiceApp.Core.Models;
using Xunit;
using Xunit.Abstractions;

namespace ServiceApp.Tests.FileSearchTests;

/// <summary>
/// Performance-Messungen gegen einen synthetischen, aber realistisch großen
/// Verzeichnisbaum (1.000 Blattordner à 8 Dateien = 8.000 Dateien, verteilt über die
/// 2/3/4-Ebenen-Ordnerstruktur des Archivs). Miss Vollscan, inkrementellen Scan, Suche
/// gegen den warmen Index sowie die eingegrenzte Live-Suche vor Indexbereitschaft.
///
/// Bewusst keine strikten Zeit-Assertions (nur sehr großzügige Ober­grenzen als
/// Regressions-Wächter) - Laufzeiten hängen stark von der zugrunde liegenden
/// Test-Hardware/dem Dateisystem ab; strikte Grenzwerte würden die CI unnötig flaky
/// machen. Die gemessenen Werte werden über <see cref="ITestOutputHelper"/> ausgegeben und
/// dienen als Grundlage für die in der Dokumentation festgehaltenen Performance-Angaben.
/// </summary>
[Trait("Category", "Performance")]
public sealed class PerformanceTests : IClassFixture<PerformanceTests.SyntheticArchiveFixture>, IDisposable
{
    private readonly SyntheticArchiveFixture _archive;
    private readonly ITestOutputHelper _output;
    private readonly string _databasePath;

    public PerformanceTests(SyntheticArchiveFixture archive, ITestOutputHelper output)
    {
        _archive = archive;
        _output = output;
        _databasePath = Path.Combine(Path.GetTempPath(), $"ServiceAppPerfIndex_{Guid.NewGuid()}", "index.db");
    }

    public void Dispose()
    {
        var directory = Path.GetDirectoryName(_databasePath);
        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private (FileArchiveIndexStore Store, FileArchiveIndexService IndexService) CreateServices()
    {
        var store = new FileArchiveIndexStore(NullLogger<FileArchiveIndexStore>.Instance, _databasePath);
        store.Initialize();
        var indexService = new FileArchiveIndexService(store, new LocalFileSystemWalker(), NullLogger<FileArchiveIndexService>.Instance);
        return (store, indexService);
    }

    [Fact]
    public async Task FullScan_EightThousandFileSyntheticArchive_CompletesWithinGenerousBound()
    {
        var (store, indexService) = CreateServices();
        using var _ = store;

        var stopwatch = Stopwatch.StartNew();
        await indexService.RunFullScanAsync(_archive.RootPath, progress: null, CancellationToken.None);
        stopwatch.Stop();

        var status = indexService.GetStatus();
        var filesPerSecond = status.TotalFileCount / Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
        _output.WriteLine($"Vollscan: {status.TotalFileCount} Dateien in {stopwatch.ElapsedMilliseconds} ms ({filesPerSecond:F0} Dateien/s).");

        Assert.Equal(_archive.TotalFiles, status.TotalFileCount);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMinutes(2), $"Vollscan von {_archive.TotalFiles} Dateien hat ungewöhnlich lange gedauert: {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task IncrementalScan_NoChangesSinceFullScan_IsFasterThanFullScan()
    {
        var (store, indexService) = CreateServices();
        using var _ = store;

        var fullScanStopwatch = Stopwatch.StartNew();
        await indexService.RunFullScanAsync(_archive.RootPath, progress: null, CancellationToken.None);
        fullScanStopwatch.Stop();

        var incrementalStopwatch = Stopwatch.StartNew();
        await indexService.RunIncrementalScanAsync(_archive.RootPath, progress: null, CancellationToken.None);
        incrementalStopwatch.Stop();

        _output.WriteLine($"Vollscan: {fullScanStopwatch.ElapsedMilliseconds} ms; unveränderter Inkrementalscan: {incrementalStopwatch.ElapsedMilliseconds} ms.");

        Assert.Equal(_archive.TotalFiles, indexService.GetStatus().TotalFileCount);
        Assert.True(incrementalStopwatch.Elapsed < TimeSpan.FromMinutes(2), $"Inkrementalscan hat ungewöhnlich lange gedauert: {incrementalStopwatch.Elapsed}.");
    }

    [Fact]
    public async Task Search_WarmIndex_ExactAndWildcardQueriesCompleteWithinGenerousBound()
    {
        var (store, indexService) = CreateServices();
        using var _ = store;
        await indexService.RunFullScanAsync(_archive.RootPath, progress: null, CancellationToken.None);
        var searchService = new FileArchiveSearchService(store, indexService, new LocalFileSystemWalker(), NullLogger<FileArchiveSearchService>.Instance);

        var exactQuery = new ArchiveSearchQuery("000000000_0", FileTypeFilter.All, CaseSensitive: false, MaxResults: 100);
        var exactStopwatch = Stopwatch.StartNew();
        var exactResults = await CollectAsync(searchService.SearchAsync(exactQuery, _archive.RootPath, ArchivePathResolver.DefaultLevelLengths, CancellationToken.None));
        exactStopwatch.Stop();

        var wildcardQuery = new ArchiveSearchQuery("*_0", FileTypeFilter.All, CaseSensitive: false, MaxResults: 5000);
        var wildcardStopwatch = Stopwatch.StartNew();
        var wildcardResults = await CollectAsync(searchService.SearchAsync(wildcardQuery, _archive.RootPath, ArchivePathResolver.DefaultLevelLengths, CancellationToken.None));
        wildcardStopwatch.Stop();

        _output.WriteLine($"Exakte Suche (1 Treffer erwartet): {exactStopwatch.ElapsedMilliseconds} ms, {exactResults.Count} Treffer.");
        _output.WriteLine($"Wildcard-Suche \"*_0\" ({_archive.LeafDirectoryCount} Treffer erwartet): {wildcardStopwatch.ElapsedMilliseconds} ms, {wildcardResults.Count} Treffer.");

        Assert.Single(exactResults);
        Assert.Equal(_archive.LeafDirectoryCount, wildcardResults.Count);
        Assert.True(exactStopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Exakte Suche gegen den warmen Index war ungewöhnlich langsam: {exactStopwatch.Elapsed}.");
        Assert.True(wildcardStopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Wildcard-Suche gegen den warmen Index war ungewöhnlich langsam: {wildcardStopwatch.Elapsed}.");
    }

    [Fact]
    public async Task Search_BeforeIndexReady_NarrowedLiveScanIsFastDespiteLargeArchive()
    {
        var (store, indexService) = CreateServices();
        using var _ = store;
        // Bewusst KEIN Scan vor der Suche - der Index ist noch nicht bereit, die Suche muss
        // über die Ordnerstruktur-Eingrenzung direkt auf einen einzelnen Blattordner
        // zugreifen, statt alle 8.000 Dateien des Archivs zu durchlaufen.
        var searchService = new FileArchiveSearchService(store, indexService, new LocalFileSystemWalker(), NullLogger<FileArchiveSearchService>.Instance);

        var query = new ArchiveSearchQuery("000000000_0", FileTypeFilter.All, CaseSensitive: false, MaxResults: 100);
        var stopwatch = Stopwatch.StartNew();
        var results = await CollectAsync(searchService.SearchAsync(query, _archive.RootPath, ArchivePathResolver.DefaultLevelLengths, CancellationToken.None));
        stopwatch.Stop();

        _output.WriteLine($"Eingegrenzte Live-Suche vor Indexbereitschaft: {stopwatch.ElapsedMilliseconds} ms, {results.Count} Treffer.");

        Assert.Single(results);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Die eingegrenzte Live-Suche sollte dank Ordnerstruktur-Nutzung deutlich schneller sein als ein voller Scan des Archivs, war aber ungewöhnlich langsam: {stopwatch.Elapsed}.");
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

    /// <summary>
    /// Baut einmalig (geteilt über alle Testmethoden dieser Klasse) einen synthetischen
    /// Verzeichnisbaum mit 10×10×10 Blattordnern (Ebenen 1/2/3, Standard-Zeichenlängen
    /// 2/3/4) à 8 Dateien = 8.000 Dateien auf einem echten temporären Verzeichnis auf.
    /// </summary>
    public sealed class SyntheticArchiveFixture : IDisposable
    {
        private static readonly string[] Extensions = { "pdf", "tif", "dxf", "docx" };

        public string RootPath { get; }
        public int TotalFiles { get; }
        public int LeafDirectoryCount { get; }

        public SyntheticArchiveFixture()
        {
            RootPath = Path.Combine(Path.GetTempPath(), $"ServiceAppPerfArchive_{Guid.NewGuid()}");
            Directory.CreateDirectory(RootPath);

            var fileCount = 0;
            var leafCount = 0;
            for (var i = 0; i < 10; i++)
            {
                var level1 = i.ToString("D2");
                for (var j = 0; j < 10; j++)
                {
                    var level2 = j.ToString("D3");
                    for (var k = 0; k < 10; k++)
                    {
                        var level3 = k.ToString("D4");
                        var directory = Path.Combine(RootPath, level1, level2, level3);
                        Directory.CreateDirectory(directory);
                        leafCount++;

                        for (var f = 0; f < 8; f++)
                        {
                            var extension = Extensions[f % Extensions.Length];
                            var fileName = $"{level1}{level2}{level3}_{f}.{extension}";
                            File.WriteAllText(Path.Combine(directory, fileName), "synthetic content");
                            fileCount++;
                        }
                    }
                }
            }

            TotalFiles = fileCount;
            LeafDirectoryCount = leafCount;
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
