namespace ArasPatchUpgradeAssistant.Models;

public sealed class PatchDescriptionTranslationRequest
{
    public string UpNumber { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public string BatType { get; init; } = string.Empty;

    public string Order { get; init; } = string.Empty;

    public string Generation { get; init; } = string.Empty;

    public string SoftwareVersion { get; init; } = string.Empty;

    public string DbTargetVersion { get; init; } = string.Empty;
}
