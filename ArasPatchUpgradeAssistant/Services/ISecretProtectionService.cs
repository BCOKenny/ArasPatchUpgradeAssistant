namespace ArasPatchUpgradeAssistant.Services;

public interface ISecretProtectionService
{
    string Protect(string plainText);

    string Unprotect(string protectedText);
}
