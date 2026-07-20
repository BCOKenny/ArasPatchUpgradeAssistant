using ArasPatchUpgradeAssistant.Services;
using ArasPatchUpgradeAssistant.Tests.TestSupport;

namespace ArasPatchUpgradeAssistant.Tests.Services;

public sealed class SetupPathParserTests
{
    [Fact]
    public void Parse_ValidSetupPath_DerivesAllParts()
    {
        using var temp = new TemporaryDirectory();
        var setupPath = temp.CreateFile(Path.Combine(
            "12SP18", "Support", "commands", "01-Upgrade",
            "SETUP-DEFAULTS-MACHINENAME.CMD"));

        var result = new SetupPathParser().Parse(setupPath);

        Assert.Equal("01-Upgrade", result.CommandFolder);
        Assert.Equal("12SP18", result.Version);
        Assert.Equal("120", result.VersionCode);
        Assert.Equal(
            Path.Combine(temp.Path, "12SP18", "Support"),
            result.SupportRoot);
        Assert.Equal(Path.Combine(temp.Path, "12SP18"), result.UpgradeRoot);
    }

    [Theory]
    [InlineData("12SP18", "12SP18")]
    [InlineData("R38 (14.38.0)", "R38")]
    [InlineData("R37_OOTB", "R37")]
    [InlineData("11SP1", "11SP1")]
    [InlineData("R11SP15", "R11SP15")]
    [InlineData("", "")]
    public void NormalizeVersionName_SplitsFolderNameIntoShortVersion(
        string folderName,
        string expected)
    {
        var result = SetupPathParser.NormalizeVersionName(folderName);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Parse_FilenameComparison_IsCaseInsensitive()
    {
        using var temp = new TemporaryDirectory();
        var setupPath = temp.CreateFile(Path.Combine(
            "12SP18", "Support", "commands", "01-Upgrade",
            "setup-defaults-machinename.cmd"));

        var result = new SetupPathParser().Parse(setupPath);

        Assert.Equal(Path.GetFullPath(setupPath), result.SetupCmdPath);
    }

    [Fact]
    public void Parse_MissingFile_ThrowsActionableError()
    {
        var exception = Assert.Throws<FileNotFoundException>(
            () => new SetupPathParser().Parse(@"Z:\missing\SETUP-DEFAULTS-MACHINENAME.CMD"));

        Assert.Contains("不存在", exception.Message);
    }

    [Fact]
    public void Parse_InvalidFilename_ThrowsActionableError()
    {
        using var temp = new TemporaryDirectory();
        var setupPath = temp.CreateFile(Path.Combine(
            "12SP18", "Support", "commands", "01-Upgrade", "wrong.cmd"));

        var exception = Assert.Throws<ArgumentException>(
            () => new SetupPathParser().Parse(setupPath));

        Assert.Contains("SETUP-DEFAULTS-MACHINENAME.CMD", exception.Message);
    }

    [Fact]
    public void Parse_InvalidDirectoryStructure_ThrowsActionableError()
    {
        using var temp = new TemporaryDirectory();
        var setupPath = temp.CreateFile(Path.Combine(
            "12SP18", "Other", "commands", "01-Upgrade",
            "SETUP-DEFAULTS-MACHINENAME.CMD"));

        var exception = Assert.Throws<ArgumentException>(
            () => new SetupPathParser().Parse(setupPath));

        Assert.Contains(@"Support\commands", exception.Message);
    }

    [Fact]
    public void Parse_NonSpVersionFolder_NormalizesVersionWithoutThrowing()
    {
        using var temp = new TemporaryDirectory();
        var setupPath = temp.CreateFile(Path.Combine(
            "R38 (14.38.0)", "Support", "commands", "01-Upgrade",
            "SETUP-DEFAULTS-MACHINENAME.CMD"));

        var result = new SetupPathParser().Parse(setupPath);

        Assert.Equal("R38", result.Version);
        Assert.Equal("R38", result.VersionCode);
    }
}
