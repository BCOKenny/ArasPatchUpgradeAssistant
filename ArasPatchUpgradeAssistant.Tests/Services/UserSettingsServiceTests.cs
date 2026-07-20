using ArasPatchUpgradeAssistant.Models;
using ArasPatchUpgradeAssistant.Services;
using ArasPatchUpgradeAssistant.Tests.TestSupport;
using Serilog.Core;
using System.Text.Json;

namespace ArasPatchUpgradeAssistant.Tests.Services;

public sealed class UserSettingsServiceTests
{
    [Fact]
    public void Load_WhenSettingsFileDoesNotExist_ReturnsDefaultSettings()
    {
        using var temp = new TemporaryDirectory();
        var settingsPath = Path.Combine(temp.Path, "settings.json");
        var service = new UserSettingsService(settingsPath, Logger.None);

        var settings = service.Load();

        Assert.Equal("root", settings.LoginName);
        Assert.True(string.IsNullOrEmpty(settings.SetupDefaultsTemplatePath));
        Assert.True(string.IsNullOrEmpty(settings.InnovatorServerConfigPath));
    }

    [Fact]
    public void Save_CreatesSettingsJsonAndLoadRestoresSavedPaths()
    {
        using var temp = new TemporaryDirectory();
        var settingsPath = Path.Combine(temp.Path, "nested", "settings.json");
        var service = new UserSettingsService(settingsPath, Logger.None);

        service.Save(new UserSettings
        {
            SetupDefaultsTemplatePath = @"K:\10.Upgrades\12SP18\Support\commands\Upgrade-110SP15-12\SETUP-DEFAULTS-MACHINENAME.CMD",
            InnovatorServerConfigPath = @"C:\Program Files (x86)\Aras\1209\InnovatorServerConfig.xml",
            SelectedDatabaseId = "Main",
            SelectedDatabaseName = "Innovator",
            CopySourceDbName = "SourceInnovator",
            LoginName = "admin",
            EncryptedPassword = "cipher-main",
            SqlLoginName = "sa",
            EncryptedSqlPassword = "cipher-sql"
        });

        Assert.True(File.Exists(settingsPath));
        var loaded = service.Load();
        Assert.Equal(@"K:\10.Upgrades\12SP18\Support\commands\Upgrade-110SP15-12\SETUP-DEFAULTS-MACHINENAME.CMD", loaded.SetupDefaultsTemplatePath);
        Assert.Equal(@"C:\Program Files (x86)\Aras\1209\InnovatorServerConfig.xml", loaded.InnovatorServerConfigPath);
        Assert.Equal("Main", loaded.SelectedDatabaseId);
        Assert.Equal("Innovator", loaded.SelectedDatabaseName);
        Assert.Equal("SourceInnovator", loaded.CopySourceDbName);
        Assert.Equal("admin", loaded.LoginName);
        Assert.Equal("cipher-main", loaded.EncryptedPassword);
        Assert.Equal("sa", loaded.SqlLoginName);
        Assert.Equal("cipher-sql", loaded.EncryptedSqlPassword);
    }

    [Fact]
    public void Save_WritesOnlyEncryptedPasswordFields()
    {
        using var temp = new TemporaryDirectory();
        var settingsPath = Path.Combine(temp.Path, "settings.json");
        var service = new UserSettingsService(settingsPath, Logger.None);

        service.Save(new UserSettings
        {
            LoginName = "root",
            EncryptedPassword = "cipher-main",
            SqlLoginName = "sa",
            EncryptedSqlPassword = "cipher-sql"
        });

        var json = File.ReadAllText(settingsPath);
        Assert.Contains("encryptedPassword", json, StringComparison.Ordinal);
        Assert.Contains("encryptedSqlPassword", json, StringComparison.Ordinal);
        Assert.Contains("copySourceDbName", json, StringComparison.Ordinal);
        Assert.Contains("cipher-main", json, StringComparison.Ordinal);
        Assert.Contains("cipher-sql", json, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("cipher-main", root.GetProperty("encryptedPassword").GetString());
        Assert.Equal("cipher-sql", root.GetProperty("encryptedSqlPassword").GetString());
        Assert.False(root.TryGetProperty("password", out _));
        Assert.False(root.TryGetProperty("sqlPassword", out _));
    }
}
