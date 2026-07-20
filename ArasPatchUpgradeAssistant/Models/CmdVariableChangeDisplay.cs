namespace ArasPatchUpgradeAssistant.Models;

public sealed record CmdVariableChangeDisplay(
    string Name,
    string Action,
    string OldValue,
    string NewValue);
