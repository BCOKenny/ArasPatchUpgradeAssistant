namespace ArasPatchUpgradeAssistant.Models;

public sealed class PatchXmlAnalysisResult
{
    public string Description { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public bool HasBody { get; init; }

    public bool HasData { get; init; }

    public int BodyLineCount { get; init; }

    public bool ContainsSql { get; init; }

    public bool ContainsAml { get; init; }

    public bool IsCSharp { get; init; }

    public IReadOnlyList<string> SqlKeywords { get; init; } = [];

    public IReadOnlyList<string> PossibleItemTypes { get; init; } = [];

    public IReadOnlyList<string> PossibleTables { get; init; } = [];

    public IReadOnlyList<string> PossibleMethods { get; init; } = [];

    public IReadOnlyList<string> BodyPreviewLines { get; init; } = [];
}
