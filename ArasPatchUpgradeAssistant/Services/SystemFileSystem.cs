using System.IO;

namespace ArasPatchUpgradeAssistant.Services;

public sealed class SystemFileSystem : IFileSystem
{
    public bool DirectoryExists(string path)
    {
        var attributes = GetAttributes(path);
        return attributes?.HasFlag(FileAttributes.Directory) == true;
    }

    public bool FileExists(string path)
    {
        var attributes = GetAttributes(path);
        return attributes is not null &&
               !attributes.Value.HasFlag(FileAttributes.Directory);
    }

    public IEnumerable<string> EnumerateFileSystemEntries(string path) =>
        Directory.EnumerateFileSystemEntries(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    private static FileAttributes? GetAttributes(string path)
    {
        _ = Path.GetFullPath(path);
        try
        {
            return File.GetAttributes(path);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }
}
