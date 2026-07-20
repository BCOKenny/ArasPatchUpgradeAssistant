using System.Text;
using ArasPatchUpgradeAssistant.Services;
using ArasPatchUpgradeAssistant.Tests.TestSupport;

namespace ArasPatchUpgradeAssistant.Tests.Services;

public sealed class CmdGenerationServiceTests
{
    [Fact]
    public void Transform_UpdatesQuotedAndPlainSet_AndAppendsMissing()
    {
        const string source =
            "@REM keep\n" +
            "@SET TOOLS_FOLDER=old\r\n" +
            "@SET \"AMLRUN_LOGINNAME=old-user\"\r" +
            "@SET UNRELATED=unchanged\r\n";
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["TOOLS_FOLDER"] = @"K:\Support\tools\DBUpdateTool",
            ["AMLRUN_LOGINNAME"] = "root",
            ["AMLRUN_PASSWORD"] = "innovator"
        };

        var result = CmdGenerationService.Transform(source, values);

        Assert.Equal(
            "@REM keep\r\n" +
            "@SET TOOLS_FOLDER=K:\\Support\\tools\\DBUpdateTool\r\n" +
            "@SET \"AMLRUN_LOGINNAME=root\"\r\n" +
            "@SET UNRELATED=unchanged\r\n" +
            "@SET AMLRUN_PASSWORD=innovator\r\n",
            result.Content);
        Assert.Equal(["更新", "更新", "新增"], result.Changes.Select(change => change.Action));
        Assert.Equal("old", result.Changes[0].OldValue);
        Assert.Null(result.Changes[2].OldValue);
    }

    [Fact]
    public void Transform_MatchesNamesCaseInsensitively_AndPreservesInlineComment()
    {
        const string source = "@set tools_folder=old & REM keep this\r\n";
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["TOOLS_FOLDER"] = "new"
        };

        var result = CmdGenerationService.Transform(source, values);

        Assert.Equal("@set tools_folder=new & REM keep this\r\n", result.Content);
    }

    [Fact]
    public void Transform_DoesNotAppendMissingSupportPathVariables()
    {
        const string source = "@SET AMLRUN_LOGINNAME=old\r\n";
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AMLRUN_LOGINNAME"] = "root",
            ["PATCHES_FOLDER"] = @"K:\Upgrade\Support\Patches\110"
        };

        var result = CmdGenerationService.Transform(source, values);

        Assert.Equal(
            "@SET AMLRUN_LOGINNAME=root\r\n",
            result.Content);
        Assert.DoesNotContain(
            result.Changes,
            change => change.Name == "PATCHES_FOLDER");
    }

    [Fact]
    public void Transform_PreservesQuotedValueWhenUpdatingPathVariable()
    {
        const string source = "@SET TARGET_IOM_DLL=\"c:\\Support\\tools\\SolutionUpgrade\\Import\\IOM.dll\"\r\n";
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["TARGET_IOM_DLL"] = @"K:\Upgrade\Support\tools\SolutionUpgrade\Import\IOM.dll"
        };

        var result = CmdGenerationService.Transform(source, values);

        Assert.Equal(
            "@SET TARGET_IOM_DLL=\"K:\\Upgrade\\Support\\tools\\SolutionUpgrade\\Import\\IOM.dll\"\r\n",
            result.Content);
    }

    [Fact]
    public void Transform_UpdatesSupportPathVariablesWithSpacesAroundEquals()
    {
        const string source =
            "@SET CONSOLEUPGRADE_FOLDER=C:\\Support\\tools\\SolutionUpgrade\\consoleUpgrade\r\n" +
            "@SET SOLUTIONS_FOLDER = C:\\support\\solutions\\100\r\n" +
            "@SET PATCHES_FOLDER=C:\\Support\\Patches\\110\r\n" +
            "@SET OLD_SOLUTIONS_FOLDER = C:\\support\\solutions\\100\r\n" +
            "@SET BACKUPS_FOLDER = C:\\support\\backup\r\n" +
            "@SET LOGS_FOLDER=C:\\Support\\LOGS\\DBUPDATE\r\n" +
            "@SET MS_DTS_LOG_DIR = C:\\Support\\LOGS\\DTS\r\n" +
            "@SET LOG_TRUNCATE_DEST=C:\\Support\\backup\\DeleteMeLog.bak\r\n" +
            "@SET UPDATES_CATALOG=C:\\Support\\Patches\\100\\core\\pre-patches.manifest.xml\r\n" +
            "@SET POST_UPDATES_CATALOG=C:\\Support\\Patches\\100\\core\\post-patches.manifest.xml\r\n" +
            "@SET UPDATES_FOLDER=C:\\Support\\Patches\\100\\core\r\n" +
            "@SET PLM_POST_PATCHES=C:\\Support\\Patches\\100\\PLM\\post\r\n";
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CONSOLEUPGRADE_FOLDER"] = @"K:\10.Upgrades\11SP!5\Support\tools\SolutionUpgrade\consoleUpgrade",
            ["SOLUTIONS_FOLDER"] = @"K:\10.Upgrades\11SP!5\Support\solutions\100",
            ["PATCHES_FOLDER"] = @"K:\10.Upgrades\11SP!5\Support\Patches\110",
            ["OLD_SOLUTIONS_FOLDER"] = @"K:\10.Upgrades\11SP!5\Support\solutions\100",
            ["BACKUPS_FOLDER"] = @"K:\10.Upgrades\11SP!5\Support\backup",
            ["LOGS_FOLDER"] = @"K:\10.Upgrades\11SP!5\Support\LOGS\DBUPDATE",
            ["MS_DTS_LOG_DIR"] = @"K:\10.Upgrades\11SP!5\Support\LOGS\DTS",
            ["LOG_TRUNCATE_DEST"] = @"K:\10.Upgrades\11SP!5\Support\backup\DeleteMeLog.bak",
            ["UPDATES_CATALOG"] = @"K:\10.Upgrades\11SP!5\Support\Patches\100\core\pre-patches.manifest.xml",
            ["POST_UPDATES_CATALOG"] = @"K:\10.Upgrades\11SP!5\Support\Patches\100\core\post-patches.manifest.xml",
            ["UPDATES_FOLDER"] = @"K:\10.Upgrades\11SP!5\Support\Patches\100\core",
            ["PLM_POST_PATCHES"] = @"K:\10.Upgrades\11SP!5\Support\Patches\100\PLM\post"
        };

        var result = CmdGenerationService.Transform(source, values);

        Assert.Equal(
            "@SET CONSOLEUPGRADE_FOLDER=K:\\10.Upgrades\\11SP!5\\Support\\tools\\SolutionUpgrade\\consoleUpgrade\r\n" +
            "@SET SOLUTIONS_FOLDER = K:\\10.Upgrades\\11SP!5\\Support\\solutions\\100\r\n" +
            "@SET PATCHES_FOLDER=K:\\10.Upgrades\\11SP!5\\Support\\Patches\\110\r\n" +
            "@SET OLD_SOLUTIONS_FOLDER = K:\\10.Upgrades\\11SP!5\\Support\\solutions\\100\r\n" +
            "@SET BACKUPS_FOLDER = K:\\10.Upgrades\\11SP!5\\Support\\backup\r\n" +
            "@SET LOGS_FOLDER=K:\\10.Upgrades\\11SP!5\\Support\\LOGS\\DBUPDATE\r\n" +
            "@SET MS_DTS_LOG_DIR = K:\\10.Upgrades\\11SP!5\\Support\\LOGS\\DTS\r\n" +
            "@SET LOG_TRUNCATE_DEST=K:\\10.Upgrades\\11SP!5\\Support\\backup\\DeleteMeLog.bak\r\n" +
            "@SET UPDATES_CATALOG=K:\\10.Upgrades\\11SP!5\\Support\\Patches\\100\\core\\pre-patches.manifest.xml\r\n" +
            "@SET POST_UPDATES_CATALOG=K:\\10.Upgrades\\11SP!5\\Support\\Patches\\100\\core\\post-patches.manifest.xml\r\n" +
            "@SET UPDATES_FOLDER=K:\\10.Upgrades\\11SP!5\\Support\\Patches\\100\\core\r\n" +
            "@SET PLM_POST_PATCHES=K:\\10.Upgrades\\11SP!5\\Support\\Patches\\100\\PLM\\post\r\n",
            result.Content);
    }

    [Fact]
    public void Generate_CreatesMachineSpecificFile_AndDoesNotModifySource()
    {
        using var temp = new TemporaryDirectory();
        var sourcePath = temp.CreateFile(
            SetupPathParser.ExpectedFilename,
            "@SET AMLRUN_LOGINNAME=old\r\n");
        var originalBytes = File.ReadAllBytes(sourcePath);

        var result = new CmdGenerationService().Generate(
            sourcePath,
            "BUILD01",
            new Dictionary<string, string> { ["AMLRUN_LOGINNAME"] = "root" });

        Assert.Equal(
            Path.Combine(temp.Path, "SETUP-DEFAULTS-BUILD01.CMD"),
            result.TargetPath);
        Assert.True(File.Exists(result.TargetPath));
        Assert.Equal(originalBytes, File.ReadAllBytes(sourcePath));
        Assert.Equal("@SET AMLRUN_LOGINNAME=root\r\n", File.ReadAllText(result.TargetPath));
    }

    [Fact]
    public void Generate_PreservesUtf8Bom()
    {
        using var temp = new TemporaryDirectory();
        var sourcePath = Path.Combine(temp.Path, SetupPathParser.ExpectedFilename);
        File.WriteAllText(
            sourcePath,
            "@SET AMLRUN_LOGINNAME=old\r\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var result = new CmdGenerationService().Generate(
            sourcePath,
            "BUILD01",
            new Dictionary<string, string> { ["AMLRUN_LOGINNAME"] = "root" });

        var bytes = File.ReadAllBytes(result.TargetPath);
        Assert.True(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
    }

    [Fact]
    public void Generate_RejectsMachineNameThatWouldOverwriteSource()
    {
        using var temp = new TemporaryDirectory();
        var sourcePath = temp.CreateFile(
            "SETUP-DEFAULTS-MACHINENAME.CMD",
            "@SET AMLRUN_LOGINNAME=old\r\n");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new CmdGenerationService().Generate(
                sourcePath,
                "MACHINENAME",
                new Dictionary<string, string> { ["AMLRUN_LOGINNAME"] = "root" }));

        Assert.Contains("來源", exception.Message);
    }
}
