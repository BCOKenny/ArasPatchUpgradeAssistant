namespace ArasPatchUpgradeAssistant.Models;

public sealed record CmdVariableChange(
    string Name,
    string Action,
    string? OldValue,
    string NewValue);
