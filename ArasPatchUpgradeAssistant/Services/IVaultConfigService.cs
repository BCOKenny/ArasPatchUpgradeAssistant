namespace ArasPatchUpgradeAssistant.Services;

public interface IVaultConfigService
{
    string GetVaultConfigPath(string innovatorServerConfigPath);

    string ParseInnovatorServerUrl(string vaultConfigPath);
}
