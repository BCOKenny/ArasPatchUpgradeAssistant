using ArasPatchUpgradeAssistant.Models;

namespace ArasPatchUpgradeAssistant.Services;

public interface ICmdGenerationService
{
    CmdGenerationResult Generate(
        string sourcePath,
        string machineName,
        IReadOnlyDictionary<string, string> values);
}
