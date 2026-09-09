using Microsoft.Extensions.Logging.Abstractions;
using ServiceApp.Core.FileArchive;
using ServiceApp.Core.Models;
using Xunit;

namespace ServiceApp.Tests.FileSearchTests;

public sealed class FileArchiveIndexStoreTests : IDisposable
{
    private readonly string _databasePath;

    public FileArchiveIndexStoreTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"ServiceAppIndexTest_{Guid.NewGuid()}", "index.db");
    }

    public void Dispose()
    {
        var directory = Path.GetDirectoryName(_databasePath);
        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private FileArchiveIndexStore CreateStore() => new(NullLogger<FileArchiveIndexStore>.Instance, _databasePath);

    private static FileArchiveEntry Entry(string fullPath, string level1 = "12", string level2 = "345", string level3 = "6789")
    {
        var fileName = Path.GetFileName(fullPath);
        return new FileArchiveEntry(
            FullPath: fullPath,
            FileName: fileName,
            FileNameWithoutExtension: Path.GetFileNameWithoutExtension(fileName),
            Extension: Path.GetExtension(fileName).TrimStart('.'),
            DirectoryPath: Path.GetDirectoryName(fullPath) ?? string.Empty,
            Level1: level1,
            Level2: level2,
            Level3: level3,
            SizeBytes: 1024,
            LastWriteUtc: DateTime.UtcNow);
    }

    [Fact]
    public void Initialize_NoExistingFile_CreatesDatabaseAndSchema()
    {
        using var store = CreateStore();

        store.Initialize();

        Assert.True(File.Exists(_databasePath));
    }

    [Fact]
    public void UpsertThenLoadAllEntries_RoundTripsFileData()
    {
        using var store = CreateStore();
        store.Initialize();

        var stamp = store.BeginScan();
        store.UpsertFile(Entry("/archive/12/345/6789/Zeichnung_123456789_A.pdf"), stamp);
        store.CompleteScan(stamp, wasFullScan: true, totalFileCount: 1, errorCount: 0, TimeSpan.FromMilliseconds(5));

        var entries = store.LoadAllEntries();

        var loaded = Assert.Single(entries);
        Assert.Equal("Zeichnung_123456789_A.pdf", loaded.FileName);
        Assert.Equal("pdf", loaded.Extension);
        Assert.Equal("12", loaded.Level1);
    }

    [Fact]
    public void CompleteScan_FullScan_SetsIndexReadyAndFullScanTimestamp()
    {
        using var store = CreateStore();
        store.Initialize();

        var stamp = store.BeginScan();
        store.CompleteScan(stamp, wasFullScan: true, totalFileCount: 0, errorCount: 0, TimeSpan.Zero);

        var status = store.GetStatus();

        Assert.True(status.IndexReady);
        Assert.NotNull(status.LastFullScanCompletedUtc);
    }

    [Fact]
    public void SweepStale_FileNotSeenInLatestScan_IsRemoved()
    {
        using var store = CreateStore();
        store.Initialize();

        var firstStamp = store.BeginScan();
        store.UpsertFile(Entry("/archive/12/345/6789/old.pdf"), firstStamp);
        store.CompleteScan(firstStamp, wasFullScan: true, totalFileCount: 1, errorCount: 0, TimeSpan.Zero);

        var secondStamp = store.BeginScan();
        store.UpsertFile(Entry("/archive/12/345/6789/new.pdf"), secondStamp);
        store.SweepStale("/archive", secondStamp);
        store.CompleteScan(secondStamp, wasFullScan: true, totalFileCount: 1, errorCount: 0, TimeSpan.Zero);

        var entries = store.LoadAllEntries();

        var remaining = Assert.Single(entries);
        Assert.Equal("new.pdf", remaining.FileName);
    }

    [Fact]
    public void SweepStale_ScopedToSubtree_LeavesFilesOutsideScopeUntouched()
    {
        using var store = CreateStore();
        store.Initialize();

        var firstStamp = store.BeginScan();
        store.UpsertFile(Entry("/archive/12/345/6789/inside.pdf"), firstStamp);
        store.UpsertFile(Entry("/archive/99/888/7777/outside.pdf", "99", "888", "7777"), firstStamp);
        store.CompleteScan(firstStamp, wasFullScan: true, totalFileCount: 2, errorCount: 0, TimeSpan.Zero);

        // Rescan only the "/archive/12/345/6789" subtree; the file under "99/888/7777"
        // was not visited in this scan and must survive because the sweep is scoped.
        var secondStamp = store.BeginScan();
        store.SweepStale("/archive/12/345/6789", secondStamp);
        store.CompleteScan(secondStamp, wasFullScan: false, totalFileCount: 1, errorCount: 0, TimeSpan.Zero);

        var entries = store.LoadAllEntries();

        var remaining = Assert.Single(entries);
        Assert.Equal("outside.pdf", remaining.FileName);
    }

    [Fact]
    public void TouchScanStamp_UnchangedFile_SurvivesSweepWithoutRewrite()
    {
        using var store = CreateStore();
        store.Initialize();

        var firstStamp = store.BeginScan();
        store.UpsertFile(Entry("/archive/12/345/6789/unchanged.pdf"), firstStamp);
        store.CompleteScan(firstStamp, wasFullScan: true, totalFileCount: 1, errorCount: 0, TimeSpan.Zero);

        var secondStamp = store.BeginScan();
        store.TouchScanStamp("/archive/12/345/6789/unchanged.pdf", secondStamp);
        store.SweepStale("/archive", secondStamp);
        store.CompleteScan(secondStamp, wasFullScan: false, totalFileCount: 1, errorCount: 0, TimeSpan.Zero);

        var entries = store.LoadAllEntries();

        Assert.Single(entries);
    }

    [Fact]
    public void AbortScan_RollsBackUncommittedChanges()
    {
        using var store = CreateStore();
        store.Initialize();

        var stamp = store.BeginScan();
        store.UpsertFile(Entry("/archive/12/345/6789/never-committed.pdf"), stamp);
        store.AbortScan();

        var entries = store.LoadAllEntries();

        Assert.Empty(entries);
    }

    [Fact]
    public void Initialize_CorruptedDatabaseFile_TransparentlyRebuildsIndex()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
        File.WriteAllText(_databasePath, "this is not a valid sqlite database file");

        using var store = CreateStore();
        store.Initialize();

        // A rebuilt (empty) index should be fully usable afterwards.
        var stamp = store.BeginScan();
        store.UpsertFile(Entry("/archive/12/345/6789/after-rebuild.pdf"), stamp);
        store.CompleteScan(stamp, wasFullScan: true, totalFileCount: 1, errorCount: 0, TimeSpan.Zero);

        var entries = store.LoadAllEntries();
        Assert.Single(entries);
    }

    [Fact]
    public void LoadExistingFileStamps_ReturnsSizeAndLastWriteForEachIndexedFile()
    {
        using var store = CreateStore();
        store.Initialize();
        var lastWrite = new DateTime(2024, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        var entry = Entry("/archive/12/345/6789/stamped.pdf") with { SizeBytes = 4096, LastWriteUtc = lastWrite };

        var stamp = store.BeginScan();
        store.UpsertFile(entry, stamp);
        store.CompleteScan(stamp, wasFullScan: true, totalFileCount: 1, errorCount: 0, TimeSpan.Zero);

        var stamps = store.LoadExistingFileStamps();

        var (sizeBytes, lastWriteUtc) = stamps["/archive/12/345/6789/stamped.pdf"];
        Assert.Equal(4096, sizeBytes);
        Assert.Equal(lastWrite, lastWriteUtc);
    }
}
