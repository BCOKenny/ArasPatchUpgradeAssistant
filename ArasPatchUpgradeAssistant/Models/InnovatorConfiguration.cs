namespace ArasPatchUpgradeAssistant.Models;

public sealed record InnovatorConfiguration(
    string ConfigPath,
    string ApServerRoot,
    IReadOnlyList<DatabaseConnectionOption> Connections,
    string ServerPrefix,
    string VaultConfigPath = "",
    string InnovatorServerUrl = "");
