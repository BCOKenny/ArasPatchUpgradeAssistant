using System.IO;
using Serilog;

namespace ArasPatchUpgradeAssistant.Services;

public sealed class IomDllLocatorService(ILogger logger) : IIomDllLocatorService
{
    public string? FindIomDllPath(string supportRoot)
    {
        logger.Information("TARGET_IOM_DLL search started");

        var searchRoot = Path.Combine(
            supportRoot,
            "tools",
            "SolutionUpgrade");
        logger.Information("SolutionUpgrade search root {SearchRoot}", searchRoot);

        try
        {
            if (!Directory.Exists(searchRoot))
            {
                logger.Information("IOM.dll found count {Count}", 0);
                logger.Warning("IOM.dll not found {SearchRoot}", searchRoot);
                return null;
            }

            var matches = Directory
                .EnumerateFiles(searchRoot, "IOM.dll", SearchOption.AllDirectories)
                .ToList();
            logger.Information("IOM.dll found count {Count}", matches.Count);

            if (matches.Count == 0)
            {
                logger.Warning("IOM.dll not found {SearchRoot}", searchRoot);
                return null;
            }

            var selected = SelectPreferredIomDll(matches);
            logger.Information("selected IOM.dll path {IomDllPath}", selected);
            return selected;
        }
        catch (Exception exception)
        {
            logger.Error(
                exception,
                "IOM.dll search failed exception {SearchRoot}",
                searchRoot);
            return null;
        }
    }

    private static string SelectPreferredIomDll(IReadOnlyList<string> matches) =>
        matches.FirstOrDefault(IsConsoleUpgradeIomDll) ??
        matches.FirstOrDefault(IsImportIomDll) ??
        matches[0];

    private static bool IsConsoleUpgradeIomDll(string path) =>
        path.Replace('/', '\\')
            .Contains(
                "\\consoleUpgrade\\IOM.dll",
                StringComparison.OrdinalIgnoreCase);

    private static bool IsImportIomDll(string path) =>
        path.Replace('/', '\\')
            .Contains(
                "\\Import\\IOM.dll",
                StringComparison.OrdinalIgnoreCase);
}
