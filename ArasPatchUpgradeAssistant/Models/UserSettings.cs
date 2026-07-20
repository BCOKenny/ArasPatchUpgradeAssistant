namespace ArasPatchUpgradeAssistant.Models;

public sealed class UserSettings
{
    public string SetupDefaultsTemplatePath { get; set; } = string.Empty;

    public string InnovatorServerConfigPath { get; set; } = string.Empty;

    public string SelectedDatabaseId { get; set; } = string.Empty;

    public string SelectedDatabaseName { get; set; } = string.Empty;

    public string CopySourceDbName { get; set; } = string.Empty;

    public string LoginName { get; set; } = "root";

    public string EncryptedPassword { get; set; } = string.Empty;

    public string SqlLoginName { get; set; } = "sa";

    public string EncryptedSqlPassword { get; set; } = string.Empty;
}
