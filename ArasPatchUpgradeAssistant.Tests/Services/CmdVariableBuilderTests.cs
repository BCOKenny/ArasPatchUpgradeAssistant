using ArasPatchUpgradeAssistant.Models;
using ArasPatchUpgradeAssistant.Services;

namespace ArasPatchUpgradeAssistant.Tests.Services;

public sealed class CmdVariableBuilderTests
{
    [Fact]
    public void Build_ReturnsAllVariablesInRequiredOrder()
    {
        var supportRoot = Path.Combine("K:\\", "10.Upgrades", "12SP18", "Support");
        var paths = new UpgradePathInfo(
            Path.Combine(supportRoot, "commands", "01", SetupPathParser.ExpectedFilename),
            "01",
            supportRoot,
            Directory.GetParent(supportRoot)!.FullName,
            "12SP18",
            "120");

        var values = new CmdVariableBuilder().Build(
            paths,
            new DatabaseConnectionOption("Main", "Innovator", "WIN19SQL2022"),
            @"C:\Aras\InnovatorServerConfig.xml",
            "http://localhost/InnovatorServer",
            "root",
            "innovator",
            "sa",
            "sql-secret",
            "SourceInnovator",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["TOOLS_FOLDER"] = @"C:\Support\tools\DBUpdateTool",
                ["CONSOLEUPGRADE_FOLDER"] = @"C:\Support\tools\SolutionUpgrade\consoleUpgrade",
                ["SOLUTIONS_FOLDER"] = @"C:\support\solutions\100",
                ["IMPORTS_FOLDER"] = @"C:\Support\Solutions\120",
                ["BACKUPS_FOLDER"] = @"C:\support\backup",
                ["LOGS_FOLDER"] = @"C:\Support\LOGS\DBUPDATE",
                ["MS_DTS_LOG_DIR"] = @"C:\Support\LOGS\DTS",
                ["LOG_TRUNCATE_DEST"] = @"C:\Support\backup\DeleteMeLog.bak",
                ["UPDATES_FOLDER"] = @"C:\Support\Patches\100\core",
                ["UPDATES_CATALOG"] = @"C:\Support\Patches\100\core\pre-patches.manifest.xml",
                ["POST_UPDATES_CATALOG"] = @"C:\Support\Patches\100\core\post-patches.manifest.xml",
                ["PLM_POST_PATCHES"] = @"C:\Support\Patches\100\PLM\post",
                ["CORE_PRE_PATCHES"] = @"C:\Support\Patches\120\core\pre",
                ["PE_POST_CATALOG"] = @"C:\Support\Patches\120\PE\post_patches.xml"
            });

        Assert.Equal(
            [
                "TOOLS_FOLDER", "CONSOLEUPGRADE_FOLDER",
                "SOLUTIONS_FOLDER", "IMPORTS_FOLDER",
                "BACKUPS_FOLDER", "LOGS_FOLDER", "MS_DTS_LOG_DIR",
                "LOG_TRUNCATE_DEST",
                "UPDATES_FOLDER", "UPDATES_CATALOG", "POST_UPDATES_CATALOG",
                "PLM_POST_PATCHES",
                "CORE_PRE_PATCHES", "PE_POST_CATALOG",
                "UPGRADE_DB_NAME", "COPY_SOURCE_DB_NAME", "COPY_TARGET_DB_NAME",
                "SOURCE_DB_SERV", "TARGET_DB_SERV",
                "SOURCE_SA_USER", "TARGET_SA_USER",
                "SOURCE_SA_PASS", "TARGET_SA_PASS",
                "INNOVATOR_SERVER_CONFIG", "AMLRUN_SERVERPREFIX",
                "AMLRUN_DATABASE", "AMLRUN_LOGINNAME", "AMLRUN_PASSWORD"
            ],
            values.Keys);
        Assert.Equal(
            Path.Combine(supportRoot, "tools", "SolutionUpgrade", "consoleUpgrade"),
            values["CONSOLEUPGRADE_FOLDER"]);
        Assert.Equal(Path.Combine(supportRoot, "solutions", "100"), values["SOLUTIONS_FOLDER"]);
        Assert.Equal(Path.Combine(supportRoot, "Solutions", "120"), values["IMPORTS_FOLDER"]);
        Assert.Equal(Path.Combine(supportRoot, "backup"), values["BACKUPS_FOLDER"]);
        Assert.Equal(Path.Combine(supportRoot, "LOGS", "DBUPDATE"), values["LOGS_FOLDER"]);
        Assert.Equal(Path.Combine(supportRoot, "LOGS", "DTS"), values["MS_DTS_LOG_DIR"]);
        Assert.Equal(
            Path.Combine(supportRoot, "backup", "DeleteMeLog.bak"),
            values["LOG_TRUNCATE_DEST"]);
        Assert.Equal(
            Path.Combine(supportRoot, "Patches", "100", "core"),
            values["UPDATES_FOLDER"]);
        Assert.Equal(
            Path.Combine(supportRoot, "Patches", "100", "core", "pre-patches.manifest.xml"),
            values["UPDATES_CATALOG"]);
        Assert.Equal(
            Path.Combine(supportRoot, "Patches", "100", "core", "post-patches.manifest.xml"),
            values["POST_UPDATES_CATALOG"]);
        Assert.Equal(
            Path.Combine(supportRoot, "Patches", "100", "PLM", "post"),
            values["PLM_POST_PATCHES"]);
        Assert.Equal("%UPGRADE_DB_NAME%", values["AMLRUN_DATABASE"]);
        Assert.Equal("Innovator", values["UPGRADE_DB_NAME"]);
        Assert.Equal("SourceInnovator", values["COPY_SOURCE_DB_NAME"]);
        Assert.Equal("Innovator", values["COPY_TARGET_DB_NAME"]);
        Assert.Equal("WIN19SQL2022", values["SOURCE_DB_SERV"]);
        Assert.Equal("WIN19SQL2022", values["TARGET_DB_SERV"]);
        Assert.Equal("sa", values["SOURCE_SA_USER"]);
        Assert.Equal("sa", values["TARGET_SA_USER"]);
        Assert.Equal("sql-secret", values["SOURCE_SA_PASS"]);
        Assert.Equal("sql-secret", values["TARGET_SA_PASS"]);
        Assert.Equal(Path.Combine(supportRoot, "Patches", "120", "PE", "post_patches.xml"),
            values["PE_POST_CATALOG"]);
    }
}
