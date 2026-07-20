using ArasPatchUpgradeAssistant.Models;

namespace ArasPatchUpgradeAssistant.Services;

public interface IInnovatorConfigService
{
    InnovatorConfiguration Load(string configPath);
}
