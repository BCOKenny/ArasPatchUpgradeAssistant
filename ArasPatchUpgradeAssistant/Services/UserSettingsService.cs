using System.IO;
using System.Text.Json;
using ArasPatchUpgradeAssistant.Models;
using Serilog;

namespace ArasPatchUpgradeAssistant.Services;

public sealed class UserSettingsService : IUserSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly ILogger _logger;

    public UserSettingsService(string? settingsFilePath = null, ILogger? logger = null)
    {
        SettingsFilePath = settingsFilePath ?? GetDefaultSettingsFilePath();
        _logger = logger ?? Log.ForContext<UserSettingsService>();
    }

    public string SettingsFilePath { get; }

    public UserSettings Load()
    {
        _logger.Information("Load user settings started {SettingsFilePath}", SettingsFilePath);

        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                _logger.Information(
                    "Load user settings completed {SettingsFilePath} {SettingsFileExists}",
                    SettingsFilePath,
                    false);
                return CreateDefaultSettings();
            }

            var json = File.ReadAllText(SettingsFilePath);
            var settings = JsonSerializer.Deserialize<UserSettings>(json, JsonOptions)
                ?? CreateDefaultSettings();
            if (string.IsNullOrWhiteSpace(settings.LoginName))
            {
                settings.LoginName = "root";
            }

            if (string.IsNullOrWhiteSpace(settings.SqlLoginName))
            {
                settings.SqlLoginName = "sa";
            }

            _logger.Information(
                "Load user settings completed {SettingsFilePath} {SettingsFileExists}",
                SettingsFilePath,
                true);
            return settings;
        }
        catch (Exception exception)
        {
            _logger.Error(
                exception,
                "Load user settings failed {SettingsFilePath}",
                SettingsFilePath);
            return CreateDefaultSettings();
        }
    }

    public void Save(UserSettings settings)
    {
        _logger.Information("Save user settings started {SettingsFilePath}", SettingsFilePath);

        try
        {
            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);
            _logger.Information("Save user settings completed {SettingsFilePath}", SettingsFilePath);
        }
        catch (Exception exception)
        {
            _logger.Error(
                exception,
                "Save user settings failed {SettingsFilePath}",
                SettingsFilePath);
        }
    }

    private static UserSettings CreateDefaultSettings() => new()
    {
        LoginName = "root",
        SqlLoginName = "sa"
    };

    private static string GetDefaultSettingsFilePath()
    {
        var localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(
            localAppData,
            "ArasPatchUpgradeAssistant",
            "settings.json");
    }
}
