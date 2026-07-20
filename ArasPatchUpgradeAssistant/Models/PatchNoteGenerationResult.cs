namespace ArasPatchUpgradeAssistant.Models;

public sealed class PatchNoteGenerationResult
{
    public bool Succeeded { get; init; }

    public bool PatchXmlExists { get; init; }

    public string MarkdownPath { get; init; } = string.Empty;

    public string PatchXmlPath { get; init; } = string.Empty;

    public string WarningMessage { get; init; } = string.Empty;

    public bool IsAiGenerated { get; init; }

    public string AiStatus { get; init; } = string.Empty;

    public string AiSourceMode { get; init; } = string.Empty;

    public string AiErrorMessage { get; init; } = string.Empty;

    public DateTimeOffset GeneratedAt { get; init; }
}
