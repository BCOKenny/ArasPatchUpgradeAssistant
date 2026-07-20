namespace ArasPatchUpgradeAssistant.Models;

public sealed record DatabaseConnectionOption(
    string Label,
    string Database,
    string Server = "");
