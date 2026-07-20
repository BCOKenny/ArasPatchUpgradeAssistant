using CommunityToolkit.Mvvm.ComponentModel;

namespace ArasPatchUpgradeAssistant.ViewModels;

public partial class BatUpdateItemViewModel : ObservableObject
{
    public event Action<BatUpdateItemViewModel>? CheckStateChanged;

    [ObservableProperty]
    private bool _isChecked;

    [ObservableProperty]
    private string _status = "Skipped";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PatchNoteIcon))]
    [NotifyPropertyChangedFor(nameof(PatchNoteToolTip))]
    private bool _isPatchNoteGenerated;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PatchNoteIcon))]
    [NotifyPropertyChangedFor(nameof(PatchNoteToolTip))]
    private bool _isPatchNoteAiFallback;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PatchNoteToolTip))]
    private string _patchNoteMarkdownPath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PatchNoteToolTip))]
    private string _patchNoteAiStatus = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PatchNoteToolTip))]
    private string _patchNoteSourceMode = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PatchNoteToolTip))]
    private string _patchNoteFallbackReason = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PatchNoteToolTip))]
    private DateTimeOffset? _patchNoteGeneratedAt;

    public string UpNumber { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Order { get; init; } = string.Empty;

    public string Generation { get; init; } = string.Empty;

    public string SoftwareVersion { get; init; } = string.Empty;

    public string DbTargetVersion { get; init; } = string.Empty;

    public string Source { get; init; } = "Official";

    public bool IsExternal { get; init; }

    public string ExternalId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string OriginalFilePath { get; init; } = string.Empty;

    public string StoredRelativePath { get; init; } = string.Empty;

    public string StoredFullPath { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string FileExtension { get; init; } = string.Empty;

    public string PatchNoteIcon =>
        !IsPatchNoteGenerated
            ? "○"
            : IsPatchNoteAiFallback
                ? "⚠️"
                : "📝";

    public string PatchNoteToolTip
    {
        get
        {
            if (!IsPatchNoteGenerated)
            {
                return "尚未產生 Patch 說明" +
                    Environment.NewLine +
                    "可按右鍵「產生 Patch 說明」";
            }

            if (IsPatchNoteAiFallback)
            {
                return "Patch 說明已產生，但 AI 中文說明 fallback" +
                    Environment.NewLine +
                    $"fallback 原因：{FormatToolTipValue(PatchNoteFallbackReason)}" +
                    Environment.NewLine +
                    $"Markdown 路徑：{FormatToolTipValue(PatchNoteMarkdownPath)}";
            }

            return "Patch 說明已產生" +
                Environment.NewLine +
                $"Markdown 路徑：{FormatToolTipValue(PatchNoteMarkdownPath)}" +
                Environment.NewLine +
                $"AI 狀態：{FormatToolTipValue(PatchNoteAiStatus)}" +
                Environment.NewLine +
                $"說明來源：{FormatToolTipValue(PatchNoteSourceMode)}" +
                Environment.NewLine +
                $"產生時間：{FormatGeneratedAt(PatchNoteGeneratedAt)}";
        }
    }

    public void MarkPatchNoteGenerated(
        string markdownPath,
        string aiStatus,
        string sourceMode,
        string fallbackReason,
        DateTimeOffset generatedAt)
    {
        PatchNoteMarkdownPath = markdownPath;
        PatchNoteAiStatus = string.IsNullOrWhiteSpace(aiStatus)
            ? "Unknown"
            : aiStatus;
        PatchNoteSourceMode = string.IsNullOrWhiteSpace(sourceMode)
            ? "Unknown"
            : sourceMode;
        PatchNoteFallbackReason = fallbackReason;
        PatchNoteGeneratedAt = generatedAt;
        IsPatchNoteAiFallback = IsFallbackStatus(PatchNoteAiStatus);
        IsPatchNoteGenerated = true;
    }

    public void MarkPatchNoteMissing()
    {
        IsPatchNoteGenerated = false;
        IsPatchNoteAiFallback = false;
        PatchNoteMarkdownPath = string.Empty;
        PatchNoteAiStatus = string.Empty;
        PatchNoteSourceMode = string.Empty;
        PatchNoteFallbackReason = string.Empty;
        PatchNoteGeneratedAt = null;
    }

    public string GetPlanKey(string batFileName)
    {
        return !string.IsNullOrWhiteSpace(UpNumber)
            ? $"{batFileName}|{UpNumber}"
            : $"{batFileName}|{Order}|{Name}";
    }

    public string GetItemKey(string batFileName) =>
        IsExternal && !string.IsNullOrWhiteSpace(ExternalId)
            ? ExternalId
            : GetPlanKey(batFileName);

    partial void OnIsCheckedChanged(bool value)
    {
        if (!IsExternal)
        {
            Status = value ? "Selected" : "Skipped";
        }

        CheckStateChanged?.Invoke(this);
    }

    private static bool IsFallbackStatus(string status) =>
        string.Equals(status, "Fallback", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "Error", StringComparison.OrdinalIgnoreCase);

    private static string FormatToolTipValue(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? "Unknown"
            : value;

    private static string FormatGeneratedAt(DateTimeOffset? generatedAt) =>
        generatedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown";
}
