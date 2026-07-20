namespace ArasPatchUpgradeAssistant.Services;

public interface IFileSystem
{
    bool DirectoryExists(string path);

    bool FileExists(string path);

    IEnumerable<string> EnumerateFileSystemEntries(string path);

    void CreateDirectory(string path);
}
