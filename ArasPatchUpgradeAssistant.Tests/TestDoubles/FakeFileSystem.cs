using ArasPatchUpgradeAssistant.Services;

namespace ArasPatchUpgradeAssistant.Tests.TestDoubles;

public sealed class FakeFileSystem : IFileSystem
{
    private readonly HashSet<string> _directories =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _files =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<string>> _entries =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Exception> _directoryErrors =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Exception> _fileErrors =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> CreatedDirectories => _createdDirectories;
    private readonly List<string> _createdDirectories = [];

    public void AddDirectory(string path, params string[] entries)
    {
        _directories.Add(path);
        _entries[path] = entries;
    }

    public void AddFile(string path) => _files.Add(path);

    public void ThrowWhenCheckingDirectory(string path, Exception exception) =>
        _directoryErrors[path] = exception;

    public void ThrowWhenCheckingFile(string path, Exception exception) =>
        _fileErrors[path] = exception;

    public bool DirectoryExists(string path)
    {
        if (_directoryErrors.TryGetValue(path, out var exception))
        {
            throw exception;
        }

        return _directories.Contains(path);
    }

    public bool FileExists(string path)
    {
        if (_fileErrors.TryGetValue(path, out var exception))
        {
            throw exception;
        }

        return _files.Contains(path);
    }

    public IEnumerable<string> EnumerateFileSystemEntries(string path) =>
        _entries.TryGetValue(path, out var entries) ? entries : [];

    public void CreateDirectory(string path)
    {
        _createdDirectories.Add(path);
        AddDirectory(path);
    }
}
