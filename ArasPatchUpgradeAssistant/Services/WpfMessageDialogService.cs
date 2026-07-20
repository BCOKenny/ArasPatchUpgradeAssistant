using System.Windows;

namespace ArasPatchUpgradeAssistant.Services;

public sealed class WpfMessageDialogService : IMessageDialogService
{
    public void ShowError(string message)
    {
        MessageBox.Show(
            message,
            "Aras Innovator Patches 升級助手",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    public bool Confirm(string title, string message) =>
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No) == MessageBoxResult.Yes;
}
