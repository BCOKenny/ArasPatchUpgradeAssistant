namespace ArasPatchUpgradeAssistant.Models;

public sealed record CmdGenerationResult(
    string TargetPath,
    IReadOnlyList<CmdVariableChange> Changes);
