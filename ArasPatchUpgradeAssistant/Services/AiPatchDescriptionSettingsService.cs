using System.IO;
using System.Text.Json;
using ArasPatchUpgradeAssistant.Models;
using Serilog;
using Serilog.Core;

namespace ArasPatchUpgradeAssistant.Services;

public sealed class AiPatchDescriptionSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly ILogger _logger;

    public AiPatchDescriptionSettingsService(
        string? settingsFilePath = null,
        ILogger? logger = null)
    {
        SettingsFilePath = settingsFilePath ?? GetDefaultSettingsFilePath();
        _logger = logger ?? Logger.None;
    }

    public string SettingsFilePath { get; }

    public AiPatchDescriptionSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return CreateDefaultSettings();
            }

            var settings = JsonSerializer.Deserialize<AiPatchDescriptionSettings>(
                    File.ReadAllText(SettingsFilePath),
                    JsonOptions)
                ?? CreateDefaultSettings();
            Normalize(settings);
            return settings;
        }
        catch (Exception exception) when (IsPathException(exception) || exception is JsonException)
        {
            _logger.Warning(
                exception,
                "Load AI patch description settings failed {SettingsFilePath}",
                SettingsFilePath);
            return CreateDefaultSettings();
        }
    }

    public void Save(AiPatchDescriptionSettings settings)
    {
        try
        {
            Normalize(settings);
            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                SettingsFilePath,
                JsonSerializer.Serialize(settings, JsonOptions));
            _logger.Information(
                "AI patch description settings saved {SettingsFilePath} {EnableAiPatchDescription} {OpenAiBaseUrl} {OpenAiModel}",
                SettingsFilePath,
                settings.EnableAiPatchDescription,
                settings.OpenAiBaseUrl,
                settings.OpenAiModel);
        }
        catch (Exception exception) when (IsPathException(exception) || exception is JsonException)
        {
            _logger.Warning(
                exception,
                "Save AI patch description settings failed {SettingsFilePath}",
                SettingsFilePath);
        }
    }

    private static AiPatchDescriptionSettings CreateDefaultSettings() => new();

    private static void Normalize(AiPatchDescriptionSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.OpenAiBaseUrl))
        {
            settings.OpenAiBaseUrl = "https://api.openai.com/v1";
        }

        if (string.IsNullOrWhiteSpace(settings.OpenAiModel))
        {
            settings.OpenAiModel = "gpt-4.1-mini";
        }

        if (settings.RequestTimeoutSeconds <= 0)
        {
            settings.RequestTimeoutSeconds = 60;
        }

        if (settings.MaxBodyPreviewLines <= 0)
        {
            settings.MaxBodyPreviewLines = 80;
        }
    }

    private static string GetDefaultSettingsFilePath()
    {
        var localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(
            localAppData,
            "ArasPatchUpgradeAssistant",
            "ai-settings.json");
    }

    private static bool IsPathException(Exception exception) =>
        exception is UnauthorizedAccessException or IOException or ArgumentException
            or NotSupportedException;
}
