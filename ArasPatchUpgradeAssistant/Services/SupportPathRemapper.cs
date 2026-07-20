using System.Text.RegularExpressions;

namespace ArasPatchUpgradeAssistant.Services;

public static partial class SupportPathRemapper
{
    private static readonly string[] AnchorNames =
    [
        "tools",
        "Patches",
        "Solutions",
        "LOGS",
        "backup"
    ];

    public static readonly IReadOnlyList<string> SupportedVariableNames =
    [
        "TOOLS_FOLDER",
        "CONSOLEUPGRADE_FOLDER",
        "TARGET_IOM_DLL",
        "PATCHES_FOLDER",
        "SOLUTIONS_FOLDER",
        "OLD_SOLUTIONS_FOLDER",
        "IMPORTS_FOLDER",
        "BACKUPS_FOLDER",
        "LOGS_FOLDER",
        "MS_DTS_LOG_DIR",
        "LOG_TRUNCATE_DEST",
        "ES_UPGRADE_FOLDER",
        "UPDATES_FOLDER",
        "UPDATES_CATALOG",
        "POST_UPDATES_CATALOG",
        "PLM_POST_PATCHES",
        "PLM_POST_CATALOG",
        "PROJECT_POST_PATCHES",
        "PROJECT_POST_CATALOG",
        "CORE_PRE_PATCHES",
        "CORE_PRE_CATALOG",
        "CORE_POST_PATCHES",
        "CORE_POST_CATALOG",
        "PE_PRE_PATCHES",
        "PE_PRE_CATALOG",
        "PE_POST_PATCHES",
        "PE_POST_CATALOG"
    ];

    private static readonly ISet<string> SupportedVariableNameSet =
        new HashSet<string>(SupportedVariableNames, StringComparer.OrdinalIgnoreCase);

    public static bool IsSupportedVariable(string variableName) =>
        SupportedVariableNameSet.Contains(variableName);

    public static string RemapSupportPath(
        string originalValue,
        string currentSupportRoot)
    {
        return TryRemapSupportPath(originalValue, currentSupportRoot, out var remapped)
            ? remapped
            : originalValue;
    }

    public static bool TryRemapSupportPath(
        string originalValue,
        string currentSupportRoot,
        out string remappedValue)
    {
        remappedValue = originalValue;
        if (string.IsNullOrWhiteSpace(originalValue) ||
            string.IsNullOrWhiteSpace(currentSupportRoot))
        {
            return false;
        }

        var value = originalValue;
        var quoted = value.Length >= 2 &&
            value.StartsWith('"') &&
            value.EndsWith('"');
        if (quoted)
        {
            value = value[1..^1];
        }

        if (TryGetRelativeAfterSupport(value, out var relativeAfterSupport) ||
            TryGetRelativeFromAnchor(value, out relativeAfterSupport))
        {
            var remapped = CombineSupportRoot(
                currentSupportRoot,
                relativeAfterSupport);
            remappedValue = quoted ? $"\"{remapped}\"" : remapped;
            return true;
        }

        return false;
    }

    public static IReadOnlyDictionary<string, string> BuildExistingPathValues(
        IReadOnlyDictionary<string, string>? existingValues,
        string currentSupportRoot)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (existingValues is null)
        {
            return result;
        }

        foreach (var variableName in SupportedVariableNames)
        {
            if (!existingValues.TryGetValue(variableName, out var originalValue) ||
                !TryRemapSupportPath(originalValue, currentSupportRoot, out var remapped))
            {
                continue;
            }

            result[variableName] = remapped;
        }

        return result;
    }

    private static bool TryGetRelativeAfterSupport(
        string value,
        out string relativePath)
    {
        relativePath = string.Empty;
        var match = SupportSegmentPattern().Match(value);
        if (!match.Success)
        {
            return false;
        }

        var supportSegmentStart = match.Index + match.Groups["prefix"].Length;
        var supportSegmentEnd = supportSegmentStart + "Support".Length;
        relativePath = value.Length > supportSegmentEnd
            ? value[supportSegmentEnd..]
            : string.Empty;
        return true;
    }

    private static bool TryGetRelativeFromAnchor(
        string value,
        out string relativePath)
    {
        relativePath = string.Empty;
        foreach (var anchorName in AnchorNames)
        {
            var match = AnchorPattern(anchorName).Match(value);
            if (!match.Success)
            {
                continue;
            }

            var anchorStart = match.Index + match.Groups["prefix"].Length;
            relativePath = value[anchorStart..];
            return true;
        }

        return false;
    }

    private static string CombineSupportRoot(
        string currentSupportRoot,
        string relativePath)
    {
        var root = currentSupportRoot.TrimEnd('\\', '/');
        if (string.IsNullOrEmpty(relativePath))
        {
            return root;
        }

        var normalizedRelative = relativePath.Replace('/', '\\');
        return normalizedRelative.StartsWith('\\')
            ? root + normalizedRelative
            : root + "\\" + normalizedRelative;
    }

    [GeneratedRegex(@"(?<prefix>^|[\\/])Support(?=$|[\\/])", RegexOptions.IgnoreCase)]
    private static partial Regex SupportSegmentPattern();

    private static Regex AnchorPattern(string anchorName) =>
        new(
            $@"(?<prefix>^|[\\/]){Regex.Escape(anchorName)}(?=$|[\\/])",
            RegexOptions.IgnoreCase);
}
