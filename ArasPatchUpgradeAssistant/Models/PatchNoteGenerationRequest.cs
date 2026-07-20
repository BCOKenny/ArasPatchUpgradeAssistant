namespace ArasPatchUpgradeAssistant.Models;

public sealed class PatchNoteGenerationRequest
{
    public string SupportRoot { get; init; } = string.Empty;

    public string CommandFolder { get; init; } = string.Empty;

    public string PatchesBase { get; init; } = string.Empty;

    public string BatFileName { get; init; } = string.Empty;

    public string BatType { get; init; } = string.Empty;

    public string CatalogXmlPath { get; init; } = string.Empty;

    public string UpNumber { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Order { get; init; } = string.Empty;

    public string Generation { get; init; } = string.Empty;

    public string SoftwareVersion { get; init; } = string.Empty;

    public string DbTargetVersion { get; init; } = string.Empty;

    public bool IsExternal { get; init; }

    public string ExternalStoredFullPath { get; init; } = string.Empty;
}
