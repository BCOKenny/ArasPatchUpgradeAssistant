using ArasPatchUpgradeAssistant.Services;

namespace ArasPatchUpgradeAssistant.Tests.Services;

public sealed class SupportPathRemapperTests
{
    [Theory]
    [InlineData(
        @"C:\support\solutions\100",
        @"K:\10.Upgrades\11SP!5\Support\solutions\100")]
    [InlineData(
        @"C:\Support\Patches\110",
        @"K:\10.Upgrades\11SP!5\Support\Patches\110")]
    [InlineData(
        @"C:\support\backup",
        @"K:\10.Upgrades\11SP!5\Support\backup")]
    [InlineData(
        @"C:\Support\LOGS\DBUPDATE",
        @"K:\10.Upgrades\11SP!5\Support\LOGS\DBUPDATE")]
    [InlineData(
        @"C:\Support\LOGS\DTS",
        @"K:\10.Upgrades\11SP!5\Support\LOGS\DTS")]
    [InlineData(
        @"C:\Support\tools\SolutionUpgrade\consoleUpgrade",
        @"K:\10.Upgrades\11SP!5\Support\tools\SolutionUpgrade\consoleUpgrade")]
    [InlineData(
        @"C:\Support\backup\DeleteMeLog.bak",
        @"K:\10.Upgrades\11SP!5\Support\backup\DeleteMeLog.bak")]
    [InlineData(
        @"C:\Support\Patches\100\core\pre-patches.manifest.xml",
        @"K:\10.Upgrades\11SP!5\Support\Patches\100\core\pre-patches.manifest.xml")]
    [InlineData(
        @"C:\Support\Patches\100\core\post-patches.manifest.xml",
        @"K:\10.Upgrades\11SP!5\Support\Patches\100\core\post-patches.manifest.xml")]
    [InlineData(
        @"C:\Support\Patches\100\core",
        @"K:\10.Upgrades\11SP!5\Support\Patches\100\core")]
    [InlineData(
        @"C:\Support\Patches\100\PLM\post",
        @"K:\10.Upgrades\11SP!5\Support\Patches\100\PLM\post")]
    [InlineData(
        @"K:\10.Upgrades\10SP1\Support\Patches\100\core\pre",
        @"K:\10.Upgrades\11SP!5\Support\Patches\100\core\pre")]
    [InlineData(
        @"c:\Support\tools\SolutionUpgrade\Import\IOM.dll",
        @"K:\10.Upgrades\11SP!5\Support\tools\SolutionUpgrade\Import\IOM.dll")]
    [InlineData(
        "\"c:\\Support\\tools\\SolutionUpgrade\\Import\\IOM.dll\"",
        "\"K:\\10.Upgrades\\11SP!5\\Support\\tools\\SolutionUpgrade\\Import\\IOM.dll\"")]
    public void RemapSupportPath_ReplacesOnlySupportRoot(
        string originalValue,
        string expected)
    {
        var result = SupportPathRemapper.RemapSupportPath(
            originalValue,
            @"K:\10.Upgrades\11SP!5\Support");

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(@"D:\Aras\tools\DBUpdateTool", @"K:\Upgrade\Support\tools\DBUpdateTool")]
    [InlineData(@"D:\Aras\Patches\120\core\pre", @"K:\Upgrade\Support\Patches\120\core\pre")]
    [InlineData(@"D:\Aras\Solutions\120", @"K:\Upgrade\Support\Solutions\120")]
    [InlineData(@"D:\Aras\LOGS\DBUPDATE", @"K:\Upgrade\Support\LOGS\DBUPDATE")]
    [InlineData(@"D:\Aras\backup", @"K:\Upgrade\Support\backup")]
    public void RemapSupportPath_WhenSupportSegmentMissing_UsesKnownAnchor(
        string originalValue,
        string expected)
    {
        var result = SupportPathRemapper.RemapSupportPath(
            originalValue,
            @"K:\Upgrade\Support");

        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryRemapSupportPath_WhenValueIsNotSupportPath_ReturnsFalse()
    {
        var remapped = SupportPathRemapper.TryRemapSupportPath(
            @"C:\Aras\InnovatorServerConfig.xml",
            @"K:\Upgrade\Support",
            out _);

        Assert.False(remapped);
    }

    [Fact]
    public void BuildExistingPathValues_IncludesSolutionsPatchesAndOldSolutionsWhenPresent()
    {
        var values = SupportPathRemapper.BuildExistingPathValues(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["SOLUTIONS_FOLDER"] = @"C:\support\solutions\100",
                ["PATCHES_FOLDER"] = @"C:\Support\Patches\110",
                ["OLD_SOLUTIONS_FOLDER"] = @"C:\support\solutions\100",
                ["CONSOLEUPGRADE_FOLDER"] = @"C:\Support\tools\SolutionUpgrade\consoleUpgrade",
                ["BACKUPS_FOLDER"] = @"C:\support\backup",
                ["LOGS_FOLDER"] = @"C:\Support\LOGS\DBUPDATE",
                ["MS_DTS_LOG_DIR"] = @"C:\Support\LOGS\DTS",
                ["LOG_TRUNCATE_DEST"] = @"C:\Support\backup\DeleteMeLog.bak",
                ["UPDATES_CATALOG"] = @"C:\Support\Patches\100\core\pre-patches.manifest.xml",
                ["POST_UPDATES_CATALOG"] = @"C:\Support\Patches\100\core\post-patches.manifest.xml",
                ["UPDATES_FOLDER"] = @"C:\Support\Patches\100\core",
                ["PLM_POST_PATCHES"] = @"C:\Support\Patches\100\PLM\post"
            },
            @"K:\10.Upgrades\11SP!5\Support");

        Assert.Equal(
            @"K:\10.Upgrades\11SP!5\Support\solutions\100",
            values["SOLUTIONS_FOLDER"]);
        Assert.Equal(
            @"K:\10.Upgrades\11SP!5\Support\Patches\110",
            values["PATCHES_FOLDER"]);
        Assert.Equal(
            @"K:\10.Upgrades\11SP!5\Support\solutions\100",
            values["OLD_SOLUTIONS_FOLDER"]);
        Assert.Equal(
            @"K:\10.Upgrades\11SP!5\Support\tools\SolutionUpgrade\consoleUpgrade",
            values["CONSOLEUPGRADE_FOLDER"]);
        Assert.Equal(
            @"K:\10.Upgrades\11SP!5\Support\backup",
            values["BACKUPS_FOLDER"]);
        Assert.Equal(
            @"K:\10.Upgrades\11SP!5\Support\LOGS\DBUPDATE",
            values["LOGS_FOLDER"]);
        Assert.Equal(
            @"K:\10.Upgrades\11SP!5\Support\LOGS\DTS",
            values["MS_DTS_LOG_DIR"]);
        Assert.Equal(
            @"K:\10.Upgrades\11SP!5\Support\backup\DeleteMeLog.bak",
            values["LOG_TRUNCATE_DEST"]);
        Assert.Equal(
            @"K:\10.Upgrades\11SP!5\Support\Patches\100\core\pre-patches.manifest.xml",
            values["UPDATES_CATALOG"]);
        Assert.Equal(
            @"K:\10.Upgrades\11SP!5\Support\Patches\100\core\post-patches.manifest.xml",
            values["POST_UPDATES_CATALOG"]);
        Assert.Equal(
            @"K:\10.Upgrades\11SP!5\Support\Patches\100\core",
            values["UPDATES_FOLDER"]);
        Assert.Equal(
            @"K:\10.Upgrades\11SP!5\Support\Patches\100\PLM\post",
            values["PLM_POST_PATCHES"]);
    }
}
