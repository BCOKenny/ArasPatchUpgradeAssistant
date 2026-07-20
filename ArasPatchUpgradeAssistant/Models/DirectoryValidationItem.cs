namespace ArasPatchUpgradeAssistant.Models;

public enum DirectoryItemKind
{
    Folder,
    File
}

public enum DirectoryValidationStatus
{
    OK,
    Missing,
    Warning
}

public sealed record DirectoryValidationItem(
    string Name,
    DirectoryItemKind Kind,
    string FullPath,
    bool Exists,
    DirectoryValidationStatus Status,
    string ErrorMessage,
    bool CanCreate);
