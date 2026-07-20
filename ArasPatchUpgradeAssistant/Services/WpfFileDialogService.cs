using Microsoft.Win32;

namespace ArasPatchUpgradeAssistant.Services;

public sealed class WpfFileDialogService : IFileDialogService
{
    public string? SelectSetupCommand() =>
        SelectFile(
            "選取 SETUP CMD",
            "SETUP CMD (SETUP-DEFAULTS-MACHINENAME.CMD)|SETUP-DEFAULTS-MACHINENAME.CMD|CMD 檔案 (*.cmd)|*.cmd");

    public string? SelectInnovatorConfig() =>
        SelectFile(
            "選取 InnovatorServerConfig.xml",
            "Innovator Server Config (InnovatorServerConfig.xml)|InnovatorServerConfig.xml|XML 檔案 (*.xml)|*.xml");

    private static string? SelectFile(string title, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
            Multiselect = false
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
