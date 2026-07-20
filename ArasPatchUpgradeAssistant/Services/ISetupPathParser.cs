using ArasPatchUpgradeAssistant.Models;

namespace ArasPatchUpgradeAssistant.Services;

public interface ISetupPathParser
{
    UpgradePathInfo Parse(string setupCmdPath);
}
