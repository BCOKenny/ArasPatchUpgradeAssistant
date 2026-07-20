using ArasPatchUpgradeAssistant.Services;
using ArasPatchUpgradeAssistant.Tests.TestSupport;
using Serilog.Core;

namespace ArasPatchUpgradeAssistant.Tests.Services;

public sealed class IomDllLocatorServiceTests
{
    [Fact]
    public void FindIomDllPath_PrefersConsoleUpgradeOverImport()
    {
        using var temp = new TemporaryDirectory();
        var supportRoot = Path.Combine(temp.Path, "Support");
        temp.CreateFile(Path.Combine(
            "Support",
            "tools",
            "SolutionUpgrade",
            "Import",
            "IOM.dll"));
        var consoleIom = temp.CreateFile(Path.Combine(
            "Support",
            "tools",
            "SolutionUpgrade",
            "consoleUpgrade",
            "IOM.dll"));

        var result = new IomDllLocatorService(Logger.None)
            .FindIomDllPath(supportRoot);

        Assert.Equal(consoleIom, result);
    }

    [Fact]
    public void FindIomDllPath_WhenSolutionUpgradeMissing_ReturnsNull()
    {
        using var temp = new TemporaryDirectory();
        var supportRoot = Path.Combine(temp.Path, "Support");

        var result = new IomDllLocatorService(Logger.None)
            .FindIomDllPath(supportRoot);

        Assert.Null(result);
    }
}
