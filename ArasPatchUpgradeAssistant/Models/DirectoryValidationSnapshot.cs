namespace ArasPatchUpgradeAssistant.Models;

public sealed record DirectoryValidationSnapshot(
    DateTimeOffset CheckedAt,
    IReadOnlyList<DirectoryValidationItem> Items);
