using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using ServiceApp.Core.Models;

namespace ServiceApp.Core.FileArchive;

/// <summary>
/// SQLite-basierte Implementierung von <see cref="IFileArchiveIndexStore"/>. Verwendet
/// WAL-Journaling (überlebt einen harten Prozessabbruch während eines Scans, ohne die
/// Datenbankdatei zu beschädigen) und prüft beim Start per <c>PRAGMA quick_check</c> die
/// Integrität; schlägt die Prüfung fehl oder lässt sich die Datei gar nicht erst öffnen,
/// wird sie verworfen und leer neu angelegt ("Index beschädigt" aus der Aufgabenstellung
/// wird damit automatisch behoben, statt die Anwendung dauerhaft in einem kaputten Zustand
/// zu belassen).
///
/// Ein Scan (voll oder inkrementell) läuft komplett innerhalb einer einzigen Transaktion
/// (<see cref="BeginScan"/> .. <see cref="CompleteScan"/>) - das macht das Schreiben
/// tausender Dateien um ein Vielfaches schneller als eine Transaktion pro Zeile und stellt
/// sicher, dass ein abgebrochener Scan den Index nicht in einem halb aktualisierten Zustand
/// zurücklässt (Rollback über <see cref="AbortScan"/> bzw. <see cref="Dispose"/>).
/// </summary>
public sealed class FileArchiveIndexStore : IFileArchiveIndexStore
{
    private const int SchemaVersion = 1;

    private readonly ILogger<FileArchiveIndexStore> _logger;
    private readonly string _databasePath;
    private readonly string _connectionString;

    private SqliteConnection? _scanConnection;
    private SqliteTransaction? _scanTransaction;
    private SqliteCommand? _upsertCommand;
    private SqliteCommand? _touchCommand;

    public FileArchiveIndexStore(ILogger<FileArchiveIndexStore> logger, string databasePath)
    {
        _logger = logger;
        _databasePath = databasePath;
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
    }

    public void Initialize()
    {
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!TryOpenAndVerify())
        {
            _logger.LogWarning("Der Suchindex \"{Path}\" ist beschädigt oder unlesbar und wird neu aufgebaut.", _databasePath);
            DeleteDatabaseFiles();

            if (!TryOpenAndVerify())
            {
                throw new Exceptions.IndexCorruptedException(_databasePath, new InvalidOperationException("Index konnte auch nach Neuanlage nicht geöffnet werden."));
            }
        }
    }

    /// <summary>Öffnet die Datenbank, legt das Schema bei Bedarf an und prüft die Integrität. Gibt <c>false</c> zurück, statt zu werfen, wenn das fehlschlägt.</summary>
    private bool TryOpenAndVerify()
    {
        try
        {
            using var connection = OpenConnection();
            EnsureSchema(connection);

            using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = "PRAGMA quick_check;";
            var result = checkCommand.ExecuteScalar() as string;
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Integritätsprüfung des Suchindex ergab: {Result}", result);
                return false;
            }

            return true;
        }
        catch (SqliteException ex)
        {
            _logger.LogWarning(ex, "Suchindex \"{Path}\" konnte nicht geöffnet werden.", _databasePath);
            return false;
        }
    }

    private void DeleteDatabaseFiles()
    {
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm", "-journal" })
        {
            var file = _databasePath + suffix;
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Beschädigte Indexdatei \"{File}\" konnte nicht gelöscht werden.", file);
            }
        }
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var pragmaCommand = connection.CreateCommand();
        pragmaCommand.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA foreign_keys=ON;";
        pragmaCommand.ExecuteNonQuery();
        return connection;
    }

    private static void EnsureSchema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Files (
                FullPath      TEXT NOT NULL COLLATE NOCASE PRIMARY KEY,
                FileName      TEXT NOT NULL COLLATE NOCASE,
                FileNameNoExt TEXT NOT NULL COLLATE NOCASE,
                Extension     TEXT NOT NULL COLLATE NOCASE,
                DirectoryPath TEXT NOT NULL COLLATE NOCASE,
                Level1        TEXT NULL,
                Level2        TEXT NULL,
                Level3        TEXT NULL,
                SizeBytes     INTEGER NOT NULL,
                LastWriteUtc  TEXT NOT NULL,
                ScanStamp     INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Files_Levels ON Files(Level1, Level2, Level3);
            CREATE INDEX IF NOT EXISTS IX_Files_DirectoryPath ON Files(DirectoryPath);
            CREATE INDEX IF NOT EXISTS IX_Files_Extension ON Files(Extension);

            CREATE TABLE IF NOT EXISTS IndexMeta (
                Id                              INTEGER PRIMARY KEY CHECK (Id = 1),
                SchemaVersion                   INTEGER NOT NULL,
                CurrentScanStamp                INTEGER NOT NULL DEFAULT 0,
                LastFullScanStartedUtc          TEXT NULL,
                LastFullScanCompletedUtc        TEXT NULL,
                LastIncrementalScanCompletedUtc TEXT NULL,
                IndexReady                      INTEGER NOT NULL DEFAULT 0,
                TotalFileCount                  INTEGER NOT NULL DEFAULT 0,
                LastScanErrorCount              INTEGER NOT NULL DEFAULT 0
            );
            INSERT OR IGNORE INTO IndexMeta (Id, SchemaVersion, CurrentScanStamp, IndexReady)
                VALUES (1, $schemaVersion, 0, 0);
            """;
        command.Parameters.AddWithValue("$schemaVersion", SchemaVersion);
        command.ExecuteNonQuery();
    }

    public IReadOnlyDictionary<string, (long SizeBytes, DateTime LastWriteUtc)> LoadExistingFileStamps()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT FullPath, SizeBytes, LastWriteUtc FROM Files;";

        var result = new Dictionary<string, (long, DateTime)>(StringComparer.OrdinalIgnoreCase);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result[reader.GetString(0)] = (reader.GetInt64(1), DateTime.Parse(reader.GetString(2)).ToUniversalTime());
        }

        return result;
    }

    public long BeginScan()
    {
        if (_scanConnection is not null)
        {
            throw new InvalidOperationException("Es läuft bereits ein Scan auf diesem Store.");
        }

        _scanConnection = new SqliteConnection(_connectionString);
        _scanConnection.Open();
        using (var pragmaCommand = _scanConnection.CreateCommand())
        {
            pragmaCommand.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
            pragmaCommand.ExecuteNonQuery();
        }

        _scanTransaction = _scanConnection.BeginTransaction();

        long scanStamp;
        using (var incrementCommand = _scanConnection.CreateCommand())
        {
            incrementCommand.Transaction = _scanTransaction;
            incrementCommand.CommandText = "UPDATE IndexMeta SET CurrentScanStamp = CurrentScanStamp + 1 WHERE Id = 1 RETURNING CurrentScanStamp;";
            scanStamp = (long)incrementCommand.ExecuteScalar()!;
        }

        _upsertCommand = _scanConnection.CreateCommand();
        _upsertCommand.Transaction = _scanTransaction;
        _upsertCommand.CommandText = """
            INSERT INTO Files (FullPath, FileName, FileNameNoExt, Extension, DirectoryPath, Level1, Level2, Level3, SizeBytes, LastWriteUtc, ScanStamp)
            VALUES ($fullPath, $fileName, $fileNameNoExt, $extension, $directoryPath, $level1, $level2, $level3, $sizeBytes, $lastWriteUtc, $scanStamp)
            ON CONFLICT(FullPath) DO UPDATE SET
                FileName = excluded.FileName, FileNameNoExt = excluded.FileNameNoExt, Extension = excluded.Extension,
                DirectoryPath = excluded.DirectoryPath, Level1 = excluded.Level1, Level2 = excluded.Level2, Level3 = excluded.Level3,
                SizeBytes = excluded.SizeBytes, LastWriteUtc = excluded.LastWriteUtc, ScanStamp = excluded.ScanStamp;
            """;
        foreach (var name in new[] { "$fullPath", "$fileName", "$fileNameNoExt", "$extension", "$directoryPath", "$level1", "$level2", "$level3", "$sizeBytes", "$lastWriteUtc", "$scanStamp" })
        {
            _upsertCommand.Parameters.Add(new SqliteParameter { ParameterName = name });
        }

        _touchCommand = _scanConnection.CreateCommand();
        _touchCommand.Transaction = _scanTransaction;
        _touchCommand.CommandText = "UPDATE Files SET ScanStamp = $scanStamp WHERE FullPath = $fullPath;";
        _touchCommand.Parameters.Add(new SqliteParameter { ParameterName = "$scanStamp" });
        _touchCommand.Parameters.Add(new SqliteParameter { ParameterName = "$fullPath" });

        return scanStamp;
    }

    public void UpsertFile(FileArchiveEntry entry, long scanStamp)
    {
        if (_upsertCommand is null)
        {
            throw new InvalidOperationException("BeginScan muss vor UpsertFile aufgerufen werden.");
        }

        _upsertCommand.Parameters["$fullPath"].Value = entry.FullPath;
        _upsertCommand.Parameters["$fileName"].Value = entry.FileName;
        _upsertCommand.Parameters["$fileNameNoExt"].Value = entry.FileNameWithoutExtension;
        _upsertCommand.Parameters["$extension"].Value = entry.Extension;
        _upsertCommand.Parameters["$directoryPath"].Value = entry.DirectoryPath;
        _upsertCommand.Parameters["$level1"].Value = (object?)entry.Level1 ?? DBNull.Value;
        _upsertCommand.Parameters["$level2"].Value = (object?)entry.Level2 ?? DBNull.Value;
        _upsertCommand.Parameters["$level3"].Value = (object?)entry.Level3 ?? DBNull.Value;
        _upsertCommand.Parameters["$sizeBytes"].Value = entry.SizeBytes;
        _upsertCommand.Parameters["$lastWriteUtc"].Value = entry.LastWriteUtc.ToString("O");
        _upsertCommand.Parameters["$scanStamp"].Value = scanStamp;
        _upsertCommand.ExecuteNonQuery();
    }

    public void TouchScanStamp(string fullPath, long scanStamp)
    {
        if (_touchCommand is null)
        {
            throw new InvalidOperationException("BeginScan muss vor TouchScanStamp aufgerufen werden.");
        }

        _touchCommand.Parameters["$scanStamp"].Value = scanStamp;
        _touchCommand.Parameters["$fullPath"].Value = fullPath;
        _touchCommand.ExecuteNonQuery();
    }

    public int SweepStale(string scopeRootPath, long scanStamp)
    {
        if (_scanConnection is null || _scanTransaction is null)
        {
            throw new InvalidOperationException("BeginScan muss vor SweepStale aufgerufen werden.");
        }

        using var command = _scanConnection.CreateCommand();
        command.Transaction = _scanTransaction;
        command.CommandText = "DELETE FROM Files WHERE (DirectoryPath = $root OR DirectoryPath LIKE $rootPrefix ESCAPE '\\') AND ScanStamp <> $scanStamp;";
        command.Parameters.AddWithValue("$root", scopeRootPath);
        command.Parameters.AddWithValue("$rootPrefix", EscapeLikePattern(scopeRootPath) + "%");
        command.Parameters.AddWithValue("$scanStamp", scanStamp);
        return command.ExecuteNonQuery();
    }

    private static string EscapeLikePattern(string value)
        => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    public void CompleteScan(long scanStamp, bool wasFullScan, long totalFileCount, long errorCount, TimeSpan duration)
    {
        if (_scanConnection is null || _scanTransaction is null)
        {
            throw new InvalidOperationException("BeginScan muss vor CompleteScan aufgerufen werden.");
        }

        using (var command = _scanConnection.CreateCommand())
        {
            command.Transaction = _scanTransaction;
            command.CommandText = wasFullScan
                ? """
                    UPDATE IndexMeta SET
                        LastFullScanCompletedUtc = $now, IndexReady = 1,
                        TotalFileCount = $totalFileCount, LastScanErrorCount = $errorCount
                    WHERE Id = 1;
                    """
                : """
                    UPDATE IndexMeta SET
                        LastIncrementalScanCompletedUtc = $now,
                        TotalFileCount = $totalFileCount, LastScanErrorCount = $errorCount
                    WHERE Id = 1;
                    """;
            command.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$totalFileCount", totalFileCount);
            command.Parameters.AddWithValue("$errorCount", errorCount);
            command.ExecuteNonQuery();
        }

        _scanTransaction.Commit();
        _logger.LogInformation(
            "Scan abgeschlossen ({Kind}): {TotalFileCount} Dateien im Index, {ErrorCount} übersprungene Elemente, Dauer {DurationMs} ms.",
            wasFullScan ? "vollständig" : "inkrementell", totalFileCount, errorCount, (long)duration.TotalMilliseconds);

        DisposeScanResources();
    }

    public void AbortScan()
    {
        try
        {
            _scanTransaction?.Rollback();
        }
        catch (SqliteException ex)
        {
            _logger.LogWarning(ex, "Rollback des abgebrochenen Scans schlug fehl.");
        }
        finally
        {
            DisposeScanResources();
        }
    }

    private void DisposeScanResources()
    {
        _upsertCommand?.Dispose();
        _upsertCommand = null;
        _touchCommand?.Dispose();
        _touchCommand = null;
        _scanTransaction?.Dispose();
        _scanTransaction = null;
        _scanConnection?.Dispose();
        _scanConnection = null;
    }

    public IReadOnlyList<FileArchiveEntry> LoadAllEntries()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT FullPath, FileName, FileNameNoExt, Extension, DirectoryPath, Level1, Level2, Level3, SizeBytes, LastWriteUtc FROM Files;";

        var results = new List<FileArchiveEntry>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new FileArchiveEntry(
                FullPath: reader.GetString(0),
                FileName: reader.GetString(1),
                FileNameWithoutExtension: reader.GetString(2),
                Extension: reader.GetString(3),
                DirectoryPath: reader.GetString(4),
                Level1: reader.IsDBNull(5) ? null : reader.GetString(5),
                Level2: reader.IsDBNull(6) ? null : reader.GetString(6),
                Level3: reader.IsDBNull(7) ? null : reader.GetString(7),
                SizeBytes: reader.GetInt64(8),
                LastWriteUtc: DateTime.Parse(reader.GetString(9)).ToUniversalTime()));
        }

        return results;
    }

    public IndexStatus GetStatus()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT IndexReady, LastFullScanCompletedUtc, LastIncrementalScanCompletedUtc, TotalFileCount, LastScanErrorCount
            FROM IndexMeta WHERE Id = 1;
            """;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return IndexStatus.Empty;
        }

        return new IndexStatus(
            IndexReady: reader.GetInt64(0) != 0,
            IsScanning: _scanConnection is not null,
            LastFullScanCompletedUtc: reader.IsDBNull(1) ? null : DateTime.Parse(reader.GetString(1)).ToUniversalTime(),
            LastIncrementalScanCompletedUtc: reader.IsDBNull(2) ? null : DateTime.Parse(reader.GetString(2)).ToUniversalTime(),
            TotalFileCount: reader.GetInt64(3),
            LastScanErrorCount: reader.GetInt64(4),
            LastScanDuration: TimeSpan.Zero);
    }

    public void Dispose()
    {
        if (_scanConnection is not null)
        {
            AbortScan();
        }
    }
}
