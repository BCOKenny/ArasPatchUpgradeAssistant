namespace ArasPatchUpgradeAssistant.Models;

public sealed record CmdVariablePreview(
    string Name,
    string Value,
    string OriginalValue = "",
    string Status = "");
