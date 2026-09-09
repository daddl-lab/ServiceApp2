using Microsoft.Extensions.Logging.Abstractions;
using ServiceApp.Core.Exceptions;
using ServiceApp.Core.FileArchive;
using ServiceApp.Core.Models;
using Xunit;

namespace ServiceApp.Tests.FileSearchTests;

public sealed class FileArchiveSearchServiceTests : IDisposable
{
    private readonly string _databasePath;
    private readonly FileArchiveIndexStore _store;
    private static readonly IReadOnlyList<int> Levels = ArchivePathResolver.DefaultLevelLengths;

    public FileArchiveSearchServiceTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"ServiceAppSearchServiceTest_{Guid.NewGuid()}", "index.db");
        _store = new FileArchiveIndexStore(NullLogger<FileArchiveIndexStore>.Instance, _databasePath);
        _store.Initialize();
    }

    public void Dispose()
    {
        _store.Dispose();
        var directory = Path.GetDirectoryName(_databasePath);
        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static FileArchiveEntry Entry(string fileName, string extension, string level1 = "12", string level2 = "345", string level3 = "6789")
        => new(
            FullPath: $@"\\server\archive\{level1}\{level2}\{level3}\{fileName}.{extension}",
            FileName: $"{fileName}.{extension}",
            FileNameWithoutExtension: fileName,
            Extension: extension,
            DirectoryPath: $@"\\server\archive\{level1}\{level2}\{level3}",
            Level1: level1, Level2: level2, Level3: level3,
            SizeBytes: 1024,
            LastWriteUtc: DateTime.UtcNow);

    private void SeedReadyIndex(params FileArchiveEntry[] entries)
    {
        var stamp = _store.BeginScan();
        foreach (var entry in entries)
        {
            _store.UpsertFile(entry, stamp);
        }

        _store.CompleteScan(stamp, wasFullScan: true, totalFileCount: entries.Length, errorCount: 0, TimeSpan.Zero);
    }

    private FileArchiveSearchService CreateService(IFileSystemWalker? walker = null)
    {
        var indexService = new FileArchiveIndexService(_store, walker ?? new FakeFileSystemWalker(), NullLogger<FileArchiveIndexService>.Instance);
        return new FileArchiveSearchService(_store, indexService, walker ?? new FakeFileSystemWalker(), NullLogger<FileArchiveSearchService>.Instance);
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

    private static ArchiveSearchQuery Query(string term, FileTypeFilter type = FileTypeFilter.All, bool caseSensitive = false, int maxResults = 1000)
        => new(term, type, caseSensitive, maxResults);

    [Fact]
    public async Task SearchAsync_IndexedExactTerm_ReturnsOnlyExactMatch()
    {
        SeedReadyIndex(Entry("123456789", "pdf"), Entry("123456789_A", "pdf"));
        var service = CreateService();

        var results = await CollectAsync(service.SearchAsync(Query("123456789"), @"\\server\archive", Levels, CancellationToken.None));

        var result = Assert.Single(results);
        Assert.Equal("123456789.pdf", result.FileName);
    }

    [Fact]
    public async Task SearchAsync_IndexedWildcardTerm_ReturnsAllMatches()
    {
        SeedReadyIndex(Entry("123456789", "pdf"), Entry("123456789_A", "pdf"), Entry("999999999", "pdf"));
        var service = CreateService();

        var results = await CollectAsync(service.SearchAsync(Query("123*"), @"\\server\archive", Levels, CancellationToken.None));

        Assert.Equal(2, results.Count);
    }

    [Theory]
    [InlineData(FileTypeFilter.Tif, "tif")]
    [InlineData(FileTypeFilter.Pdf, "pdf")]
    [InlineData(FileTypeFilter.Jt, "jt")]
    [InlineData(FileTypeFilter.Dwg, "dwg")]
    [InlineData(FileTypeFilter.Doc, "doc")]
    [InlineData(FileTypeFilter.Docx, "docx")]
    [InlineData(FileTypeFilter.Dxf, "dxf")]
    [InlineData(FileTypeFilter.Xls, "xls")]
    [InlineData(FileTypeFilter.Xlsx, "xlsx")]
    public async Task SearchAsync_EachFileTypeFilter_MatchesOnlyItsOwnExtension(FileTypeFilter filter, string extension)
    {
        SeedReadyIndex(
            Entry("123456789", "pdf"), Entry("123456789", "tif"), Entry("123456789", "jt"),
            Entry("123456789", "dwg"), Entry("123456789", "doc"), Entry("123456789", "docx"),
            Entry("123456789", "dxf"), Entry("123456789", "xls"), Entry("123456789", "xlsx"));
        var service = CreateService();

        var results = await CollectAsync(service.SearchAsync(Query("123456789", filter), @"\\server\archive", Levels, CancellationToken.None));

        var result = Assert.Single(results);
        Assert.Equal(extension, result.Extension);
    }

    [Fact]
    public async Task SearchAsync_AllFileTypesFilter_ReturnsEveryIndexedExtension()
    {
        SeedReadyIndex(Entry("123456789", "pdf"), Entry("123456789", "dwg"), Entry("123456789", "xlsx"));
        var service = CreateService();

        var results = await CollectAsync(service.SearchAsync(Query("123456789", FileTypeFilter.All), @"\\server\archive", Levels, CancellationToken.None));

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task SearchAsync_UppercaseExtensionInIndex_MatchesFilterRegardlessOfCase()
    {
        SeedReadyIndex(Entry("123456789", "PDF"));
        var service = CreateService();

        var results = await CollectAsync(service.SearchAsync(Query("123456789", FileTypeFilter.Pdf), @"\\server\archive", Levels, CancellationToken.None));

        Assert.Single(results);
    }

    [Fact]
    public async Task SearchAsync_NoMatchingFiles_ReturnsEmptyResult()
    {
        SeedReadyIndex(Entry("123456789", "pdf"));
        var service = CreateService();

        var results = await CollectAsync(service.SearchAsync(Query("does-not-exist"), @"\\server\archive", Levels, CancellationToken.None));

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_ManyMatches_RespectsMaxResultsCap()
    {
        var entries = Enumerable.Range(0, 50).Select(i => Entry($"treffer{i:D3}", "pdf")).ToArray();
        SeedReadyIndex(entries);
        var service = CreateService();

        var results = await CollectAsync(service.SearchAsync(Query("treffer*", maxResults: 10), @"\\server\archive", Levels, CancellationToken.None));

        Assert.Equal(10, results.Count);
    }

    [Fact]
    public async Task SearchAsync_EmptySearchTerm_ThrowsInvalidSearchTermException()
    {
        SeedReadyIndex(Entry("123456789", "pdf"));
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidSearchTermException>(
            () => CollectAsync(service.SearchAsync(Query(string.Empty), @"\\server\archive", Levels, CancellationToken.None)));
    }

    [Fact]
    public async Task SearchAsync_VeryShortSearchTerm_MatchesOnlyExactShortFileName()
    {
        SeedReadyIndex(Entry("1", "pdf"), Entry("12", "pdf"));
        var service = CreateService();

        var results = await CollectAsync(service.SearchAsync(Query("1"), @"\\server\archive", Levels, CancellationToken.None));

        var result = Assert.Single(results);
        Assert.Equal("1.pdf", result.FileName);
    }

    [Fact]
    public async Task SearchAsync_SearchTermWithSpecialCharacters_ReturnsNoMatchesWithoutThrowing()
    {
        SeedReadyIndex(Entry("123456789", "pdf"));
        var service = CreateService();

        var results = await CollectAsync(service.SearchAsync(Query("A.B(C)+#@"), @"\\server\archive", Levels, CancellationToken.None));

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_CaseSensitiveTrue_DoesNotMatchDifferentCase()
    {
        SeedReadyIndex(Entry("Zeichnung", "pdf"));
        var service = CreateService();

        var results = await CollectAsync(service.SearchAsync(Query("zeichnung", caseSensitive: true), @"\\server\archive", Levels, CancellationToken.None));

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_CaseInsensitiveByDefault_MatchesDifferentCase()
    {
        SeedReadyIndex(Entry("Zeichnung", "pdf"));
        var service = CreateService();

        var results = await CollectAsync(service.SearchAsync(Query("zeichnung"), @"\\server\archive", Levels, CancellationToken.None));

        Assert.Single(results);
    }

    [Fact]
    public async Task SearchAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        SeedReadyIndex(Entry("123456789", "pdf"));
        var service = CreateService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => CollectAsync(service.SearchAsync(Query("123456789"), @"\\server\archive", Levels, cts.Token)));
    }

    [Fact]
    public async Task SearchAsync_IndexNotYetReady_FallsBackToNarrowedLiveScanOfArchiveRoot()
    {
        // Directory keys are built with Path.Combine (not a hardcoded separator) so they
        // match exactly what FileArchiveSearchService's real path construction produces on
        // whatever platform the test actually runs on (backslash on Windows in production,
        // forward slash here in the Linux test sandbox).
        var root = Path.Combine("archiveroot");
        var level1 = Path.Combine(root, "12");
        var level2 = Path.Combine(level1, "345");
        var level3 = Path.Combine(level2, "6789");
        var walker = new FakeFileSystemWalker();
        walker.AddSubDirectory(root, level1);
        walker.AddSubDirectory(level1, level2);
        walker.AddSubDirectory(level2, level3);
        walker.AddFile(level3, new FileSystemFileInfo(Path.Combine(level3, "123456789.pdf"), "123456789.pdf", 512, DateTime.UtcNow));
        var service = CreateService(walker);
        // Index deliberately left un-scanned (IndexReady stays false).

        var results = await CollectAsync(service.SearchAsync(Query("123456789"), root, Levels, CancellationToken.None));

        var result = Assert.Single(results);
        Assert.Equal("123456789.pdf", result.FileName);
    }

    [Fact]
    public async Task SearchAsync_IndexNotYetReadyAndTermUnresolvable_ReturnsEmptyWithoutFullNetworkWalk()
    {
        var root = Path.Combine("archiveroot");
        var somewhere = Path.Combine(root, "somewhere");
        var walker = new FakeFileSystemWalker();
        walker.AddSubDirectory(root, somewhere);
        walker.AddFile(somewhere, new FileSystemFileInfo(Path.Combine(somewhere, "match.pdf"), "match.pdf", 512, DateTime.UtcNow));
        var service = CreateService(walker);

        // Leading wildcard cannot be narrowed - must not trigger a full recursive walk.
        var results = await CollectAsync(service.SearchAsync(Query("*match"), root, Levels, CancellationToken.None));

        Assert.Empty(results);
    }
}
