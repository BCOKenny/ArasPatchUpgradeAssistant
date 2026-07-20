namespace ArasPatchUpgradeAssistant.Models;

public sealed class AiPatchDescriptionSettings
{
    public bool EnableAiPatchDescription { get; set; }

    public string OpenAiBaseUrl { get; set; } = "https://api.openai.com/v1";

    public string OpenAiModel { get; set; } = "gpt-4.1-mini";

    public string EncryptedOpenAiApiKey { get; set; } = string.Empty;

    public int RequestTimeoutSeconds { get; set; } = 60;

    public int MaxBodyPreviewLines { get; set; } = 80;

    public bool EnableAiRequestDebugLog { get; set; }
}
