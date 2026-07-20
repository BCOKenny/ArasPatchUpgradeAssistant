using ArasPatchUpgradeAssistant.Services;
using ArasPatchUpgradeAssistant.Tests.TestSupport;

namespace ArasPatchUpgradeAssistant.Tests.Services;

public sealed class SystemFileSystemTests
{
    [Fact]
    public void ExistenceChecks_DistinguishFilesDirectoriesAndMissingPaths()
    {
        using var temp = new TemporaryDirectory();
        var directory = Path.Combine(temp.Path, "folder");
        Directory.CreateDirectory(directory);
        var file = temp.CreateFile("file.txt");
        var fileSystem = new SystemFileSystem();

        Assert.True(fileSystem.DirectoryExists(directory));
        Assert.False(fileSystem.DirectoryExists(file));
        Assert.True(fileSystem.FileExists(file));
        Assert.False(fileSystem.FileExists(directory));
        Assert.False(fileSystem.DirectoryExists(Path.Combine(temp.Path, "missing")));
        Assert.False(fileSystem.FileExists(Path.Combine(temp.Path, "missing.txt")));
    }

    [Fact]
    public void DirectoryExists_InvalidPath_DoesNotSilentlyReportMissing()
    {
        var fileSystem = new SystemFileSystem();

        Assert.Throws<ArgumentException>(() => fileSystem.DirectoryExists("\0"));
    }
}
