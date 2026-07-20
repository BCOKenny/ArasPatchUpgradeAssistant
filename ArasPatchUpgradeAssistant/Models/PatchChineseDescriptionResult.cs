namespace ArasPatchUpgradeAssistant.Models;

public sealed class PatchChineseDescriptionResult
{
    public string ChineseExplanation { get; init; } = "待人工確認";

    public string ChineseSummary { get; init; } = "待人工確認";

    public string ImpactScope { get; init; } = "待人工確認";

    public string RiskNotes { get; init; } = "待人工確認";

    public bool IsAiGenerated { get; init; }

    public string Model { get; init; } = string.Empty;

    public string Status { get; init; } = "Fallback";

    public string ErrorMessage { get; init; } = string.Empty;

    public string SourceMode { get; init; } = "DescriptionOnly";

    public bool BodyIncluded { get; init; }

    public int DescriptionChars { get; init; }

    public int BodyChars { get; init; }

    public int BodyPreviewLines { get; init; }

    public int PromptChars { get; init; }

    public int RequestJsonChars { get; init; }

    public string ErrorCode { get; init; } = string.Empty;

    public static PatchChineseDescriptionResult Fallback(
        string model,
        string status,
        string errorMessage,
        string errorCode = "",
        string sourceMode = "DescriptionOnly",
        int descriptionChars = 0,
        int promptChars = 0,
        int requestJsonChars = 0) => new()
        {
            Model = model,
            Status = status,
            ErrorMessage = errorMessage,
            ErrorCode = errorCode,
            SourceMode = sourceMode,
            DescriptionChars = descriptionChars,
            PromptChars = promptChars,
            RequestJsonChars = requestJsonChars
        };
}
