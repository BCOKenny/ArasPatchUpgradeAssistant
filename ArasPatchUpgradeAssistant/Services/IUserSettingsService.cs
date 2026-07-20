using ArasPatchUpgradeAssistant.Models;

namespace ArasPatchUpgradeAssistant.Services;

public interface IUserSettingsService
{
    string SettingsFilePath { get; }

    UserSettings Load();

    void Save(UserSettings settings);
}
