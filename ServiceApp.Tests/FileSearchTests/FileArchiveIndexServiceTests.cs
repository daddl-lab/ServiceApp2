using Microsoft.Extensions.Logging.Abstractions;
using ServiceApp.Core.Exceptions;
using ServiceApp.Core.FileArchive;
using Xunit;

namespace ServiceApp.Tests.FileSearchTests;

public sealed class FileArchiveIndexServiceTests : IDisposable
{
    private readonly string _databasePath;
    private readonly FileArchiveIndexStore _store;

    public FileArchiveIndexServiceTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"ServiceAppIndexServiceTest_{Guid.NewGuid()}", "index.db");
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

    private FileArchiveIndexService CreateService(IFileSystemWalker walker)
        => new(_store, walker, NullLogger<FileArchiveIndexService>.Instance);

    private static FileSystemFileInfo File(string name, long size = 1024) => new($"\\\\server\\archive\\{name}", name, size, DateTime.UtcNow);

    [Fact]
    public async Task RunFullScanAsync_ThreeLevelTree_AssignsLevelsFromActualDirectoryDepth()
    {
        var walker = new FakeFileSystemWalker();
        walker.AddSubDirectory(@"\\server\archive", @"\\server\archive\12");
        walker.AddSubDirectory(@"\\server\archive\12", @"\\server\archive\12\345");
        walker.AddSubDirectory(@"\\server\archive\12\345", @"\\server\archive\12\345\6789");
        walker.AddFile(@"\\server\archive\12\345\6789", new FileSystemFileInfo(@"\\server\archive\12\345\6789\Zeichnung_123456789_A.pdf", "Zeichnung_123456789_A.pdf", 2048, DateTime.UtcNow));
        var service = CreateService(walker);

        await service.RunFullScanAsync(@"\\server\archive", progress: null, CancellationToken.None);

        var entries = _store.LoadAllEntries();
        var entry = Assert.Single(entries);
        Assert.Equal("12", entry.Level1);
        Assert.Equal("345", entry.Level2);
        Assert.Equal("6789", entry.Level3);
        Assert.Equal("pdf", entry.Extension);
    }

    [Fact]
    public async Task RunFullScanAsync_UnsupportedFileExtension_IsNotIndexed()
    {
        var walker = new FakeFileSystemWalker();
        walker.AddDirectory(@"\\server\archive");
        walker.AddFile(@"\\server\archive", new FileSystemFileInfo(@"\\server\archive\notes.txt", "notes.txt", 10, DateTime.UtcNow));
        walker.AddFile(@"\\server\archive", File("drawing.pdf"));
        var service = CreateService(walker);

        await service.RunFullScanAsync(@"\\server\archive", progress: null, CancellationToken.None);

        var entries = _store.LoadAllEntries();
        var entry = Assert.Single(entries);
        Assert.Equal("pdf", entry.Extension);
    }

    [Fact]
    public async Task RunFullScanAsync_RootPathDoesNotExist_ThrowsArchiveRootNotFoundException()
    {
        var walker = new FakeFileSystemWalker();
        var service = CreateService(walker);

        await Assert.ThrowsAsync<ArchiveRootNotFoundException>(
            () => service.RunFullScanAsync(@"\\server\does-not-exist", progress: null, CancellationToken.None));
    }

    [Fact]
    public async Task RunFullScanAsync_OneSubdirectoryDeniesAccess_SkipsItButIndexesSiblings()
    {
        var walker = new FakeFileSystemWalker();
        walker.AddSubDirectory(@"\\server\archive", @"\\server\archive\locked");
        walker.AddSubDirectory(@"\\server\archive", @"\\server\archive\ok");
        walker.FailFileEnumeration(@"\\server\archive\locked", new UnauthorizedAccessException("Zugriff verweigert"));
        walker.AddFile(@"\\server\archive\ok", File("accessible.pdf"));
        var service = CreateService(walker);

        await service.RunFullScanAsync(@"\\server\archive", progress: null, CancellationToken.None);

        var entries = _store.LoadAllEntries();
        var entry = Assert.Single(entries);
        Assert.Equal("accessible.pdf", entry.FileName);
    }

    [Fact]
    public async Task RunFullScanAsync_DirectoryListingFailsMidTree_DoesNotAbortWholeScan()
    {
        var walker = new FakeFileSystemWalker();
        walker.AddSubDirectory(@"\\server\archive", @"\\server\archive\broken");
        walker.AddSubDirectory(@"\\server\archive", @"\\server\archive\fine");
        walker.FailDirectoryEnumeration(@"\\server\archive\broken", new IOException("Netzwerkfehler"));
        walker.AddFile(@"\\server\archive\fine", File("survives.pdf"));
        var service = CreateService(walker);

        await service.RunFullScanAsync(@"\\server\archive", progress: null, CancellationToken.None);

        var entries = _store.LoadAllEntries();
        var entry = Assert.Single(entries);
        Assert.Equal("survives.pdf", entry.FileName);

        var status = service.GetStatus();
        Assert.True(status.LastScanErrorCount >= 1);
    }

    [Fact]
    public async Task IndexUpdated_AfterSuccessfulScan_IsRaised()
    {
        var walker = new FakeFileSystemWalker();
        walker.AddDirectory(@"\\server\archive");
        var service = CreateService(walker);
        var raised = false;
        service.IndexUpdated += () => raised = true;

        await service.RunFullScanAsync(@"\\server\archive", progress: null, CancellationToken.None);

        Assert.True(raised);
    }

    [Fact]
    public async Task RunIncrementalScanAsync_FileAddedSinceLastScan_IsIndexed()
    {
        using var tempRoot = new TempDirectory();
        System.IO.File.WriteAllText(Path.Combine(tempRoot.Path, "erster.pdf"), "content");
        var service = new FileArchiveIndexService(_store, new LocalFileSystemWalker(), NullLogger<FileArchiveIndexService>.Instance);
        await service.RunFullScanAsync(tempRoot.Path, progress: null, CancellationToken.None);

        System.IO.File.WriteAllText(Path.Combine(tempRoot.Path, "zweiter.pdf"), "content");
        await service.RunIncrementalScanAsync(tempRoot.Path, progress: null, CancellationToken.None);

        var entries = _store.LoadAllEntries();
        Assert.Equal(2, entries.Count);
    }

    [Fact]
    public async Task RunIncrementalScanAsync_FileDeletedSinceLastScan_IsRemovedFromIndex()
    {
        using var tempRoot = new TempDirectory();
        var filePath = Path.Combine(tempRoot.Path, "wird-geloescht.pdf");
        System.IO.File.WriteAllText(filePath, "content");
        var service = new FileArchiveIndexService(_store, new LocalFileSystemWalker(), NullLogger<FileArchiveIndexService>.Instance);
        await service.RunFullScanAsync(tempRoot.Path, progress: null, CancellationToken.None);

        System.IO.File.Delete(filePath);
        await service.RunIncrementalScanAsync(tempRoot.Path, progress: null, CancellationToken.None);

        Assert.Empty(_store.LoadAllEntries());
    }

    [Fact]
    public async Task RunIncrementalScanAsync_FileRenamedSinceLastScan_OldPathGoneNewPathPresent()
    {
        using var tempRoot = new TempDirectory();
        var oldPath = Path.Combine(tempRoot.Path, "alter-name.pdf");
        var newPath = Path.Combine(tempRoot.Path, "neuer-name.pdf");
        System.IO.File.WriteAllText(oldPath, "content");
        var service = new FileArchiveIndexService(_store, new LocalFileSystemWalker(), NullLogger<FileArchiveIndexService>.Instance);
        await service.RunFullScanAsync(tempRoot.Path, progress: null, CancellationToken.None);

        System.IO.File.Move(oldPath, newPath);
        await service.RunIncrementalScanAsync(tempRoot.Path, progress: null, CancellationToken.None);

        var entry = Assert.Single(_store.LoadAllEntries());
        Assert.Equal("neuer-name.pdf", entry.FileName);
    }

    [Fact]
    public async Task RunIncrementalScanAsync_UnchangedFile_IsNotRewritten()
    {
        using var tempRoot = new TempDirectory();
        System.IO.File.WriteAllText(Path.Combine(tempRoot.Path, "stabil.pdf"), "content");
        var service = new FileArchiveIndexService(_store, new LocalFileSystemWalker(), NullLogger<FileArchiveIndexService>.Instance);
        await service.RunFullScanAsync(tempRoot.Path, progress: null, CancellationToken.None);
        var firstEntry = Assert.Single(_store.LoadAllEntries());

        await service.RunIncrementalScanAsync(tempRoot.Path, progress: null, CancellationToken.None);
        var secondEntry = Assert.Single(_store.LoadAllEntries());

        Assert.Equal(firstEntry.LastWriteUtc, secondEntry.LastWriteUtc);
    }

    /// <summary>Kleines RAII-Hilfsmittel für ein temporäres Verzeichnis, das nach dem Test wieder gelöscht wird.</summary>
    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ServiceAppArchiveScanTest_{Guid.NewGuid()}");

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
