using System.IO;
using System.Text.RegularExpressions;
using ArasPatchUpgradeAssistant.Models;

namespace ArasPatchUpgradeAssistant.Services;

public sealed partial class SetupPathParser : ISetupPathParser
{
    public const string ExpectedFilename = "SETUP-DEFAULTS-MACHINENAME.CMD";

    public UpgradePathInfo Parse(string setupCmdPath)
    {
        if (string.IsNullOrWhiteSpace(setupCmdPath))
        {
            throw new ArgumentException("請選擇 SETUP CMD 檔案。", nameof(setupCmdPath));
        }

        var fullPath = Path.GetFullPath(setupCmdPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"SETUP CMD 不存在：{fullPath}", fullPath);
        }

        if (!string.Equals(
                Path.GetFileName(fullPath),
                ExpectedFilename,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"檔名必須是 {ExpectedFilename}。",
                nameof(setupCmdPath));
        }

        var commandFolder = Directory.GetParent(fullPath);
        var commandsFolder = commandFolder?.Parent;
        var supportFolder = commandsFolder?.Parent;
        var upgradeFolder = supportFolder?.Parent;

        if (commandFolder is null ||
            commandsFolder is null ||
            supportFolder is null ||
            upgradeFolder is null ||
            !string.Equals(commandsFolder.Name, "commands", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(supportFolder.Name, "Support", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                @"SETUP CMD 路徑必須位於 Support\commands\<Command Folder> 底下。",
                nameof(setupCmdPath));
        }

        var version = NormalizeVersionName(upgradeFolder.Name);
        return new UpgradePathInfo(
            fullPath,
            commandFolder.Name,
            supportFolder.FullName,
            upgradeFolder.FullName,
            version,
            GetVersionCode(version));
    }

    public static string NormalizeVersionName(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return string.Empty;
        }

        var trimmed = folderName.Trim();
        var splitIndex = trimmed.IndexOfAny([' ', '(', '_', '-']);
        return splitIndex <= 0
            ? trimmed
            : trimmed[..splitIndex];
    }

    private static string GetVersionCode(string version)
    {
        var match = VersionPattern().Match(version);
        return match.Success
            ? $"{match.Groups["major"].Value}0"
            : version;
    }

    [GeneratedRegex(@"^R?(?<major>\d+)SP(?<servicePack>\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex VersionPattern();
}
