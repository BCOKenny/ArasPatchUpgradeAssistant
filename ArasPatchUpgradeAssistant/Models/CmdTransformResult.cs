namespace ArasPatchUpgradeAssistant.Models;

public sealed record CmdTransformResult(
    string Content,
    IReadOnlyList<CmdVariableChange> Changes);
