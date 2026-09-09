using ServiceApp.Core.FileArchive;

namespace ServiceApp.Tests.FileSearchTests;

/// <summary>
/// In-Memory-Testdouble für <see cref="IFileSystemWalker"/>, mit dem sich Dateisystemfehler
/// (fehlende Berechtigung, nicht erreichbarer Ordner) gezielt für einzelne Pfade simulieren
/// lassen, ohne echte Betriebssystem-Berechtigungen manipulieren zu müssen.
/// </summary>
internal sealed class FakeFileSystemWalker : IFileSystemWalker
{
    private readonly HashSet<string> _existingDirectories = new();
    private readonly Dictionary<string, List<string>> _subDirectories = new();
    private readonly Dictionary<string, List<FileSystemFileInfo>> _files = new();
    private readonly Dictionary<string, Exception> _directoryEnumerationFailures = new();
    private readonly Dictionary<string, Exception> _fileEnumerationFailures = new();

    public void AddDirectory(string path)
    {
        _existingDirectories.Add(path);
        _subDirectories.TryAdd(path, new List<string>());
    }

    public void AddSubDirectory(string parent, string child)
    {
        AddDirectory(parent);
        AddDirectory(child);
        _subDirectories[parent].Add(child);
    }

    public void AddFile(string directory, FileSystemFileInfo file)
    {
        AddDirectory(directory);
        _files.TryAdd(directory, new List<FileSystemFileInfo>());
        _files[directory].Add(file);
    }

    public void FailDirectoryEnumeration(string path, Exception exception) => _directoryEnumerationFailures[path] = exception;

    public void FailFileEnumeration(string path, Exception exception) => _fileEnumerationFailures[path] = exception;

    public bool DirectoryExists(string path) => _existingDirectories.Contains(path);

    public IEnumerable<string> EnumerateDirectories(string path)
    {
        if (_directoryEnumerationFailures.TryGetValue(path, out var exception))
        {
            throw exception;
        }

        return _subDirectories.TryGetValue(path, out var list) ? list : Enumerable.Empty<string>();
    }

    public IEnumerable<FileSystemFileInfo> EnumerateFiles(string path)
    {
        if (_fileEnumerationFailures.TryGetValue(path, out var exception))
        {
            throw exception;
        }

        return _files.TryGetValue(path, out var list) ? list : Enumerable.Empty<FileSystemFileInfo>();
    }
}
