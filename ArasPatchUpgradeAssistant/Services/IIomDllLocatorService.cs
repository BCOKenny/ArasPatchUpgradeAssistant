namespace ArasPatchUpgradeAssistant.Services;

public interface IIomDllLocatorService
{
    string? FindIomDllPath(string supportRoot);
}
