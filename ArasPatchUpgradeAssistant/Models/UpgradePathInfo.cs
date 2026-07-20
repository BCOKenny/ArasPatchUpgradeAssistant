namespace ArasPatchUpgradeAssistant.Models;

public sealed record UpgradePathInfo(
    string SetupCmdPath,
    string CommandFolder,
    string SupportRoot,
    string UpgradeRoot,
    string Version,
    string VersionCode);
