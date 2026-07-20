namespace ArasPatchUpgradeAssistant.Services;

public interface IMessageDialogService
{
    void ShowError(string message);

    bool Confirm(string title, string message);
}
