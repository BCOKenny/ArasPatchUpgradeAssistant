using ArasPatchUpgradeAssistant.Models;

namespace ArasPatchUpgradeAssistant.Services;

public interface IDirectoryValidationService
{
    DirectoryValidationSnapshot Validate(
        UpgradePathInfo paths,
        string generatedCmdPath);

    void CreateDirectory(DirectoryValidationItem item);
}
