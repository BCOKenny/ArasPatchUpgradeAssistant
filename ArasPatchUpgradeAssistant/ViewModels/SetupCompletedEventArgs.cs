using ArasPatchUpgradeAssistant.Models;

namespace ArasPatchUpgradeAssistant.ViewModels;

public sealed class SetupCompletedEventArgs : EventArgs
{
    public SetupCompletedEventArgs(UpgradePathInfo paths, string targetPath)
    {
        Paths = paths;
        TargetPath = targetPath;
    }

    public UpgradePathInfo Paths { get; }

    public string TargetPath { get; }
}
