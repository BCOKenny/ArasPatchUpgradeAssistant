using ArasPatchUpgradeAssistant.Models;
using ArasPatchUpgradeAssistant.Services;
using ArasPatchUpgradeAssistant.Tests.TestDoubles;
using ArasPatchUpgradeAssistant.Tests.TestSupport;
using ArasPatchUpgradeAssistant.ViewModels;
using Serilog.Core;

namespace ArasPatchUpgradeAssistant.Tests.ViewModels;

public sealed class SetupStepViewModelTests
{
    [Fact]
    public void LoadInnovatorConfig_SelectsFirstConnectionAndMasksPassword()
    {
        var fixture = new Fixture();
        var vm = fixture.CreateViewModel();

        vm.LoadInnovatorConfig(fixture.Configuration.ConfigPath);

        Assert.Equal("Main", vm.SelectedConnection?.Label);
        Assert.Equal("*********", vm.MaskedPassword);
        Assert.Equal(fixture.Configuration.ServerPrefix, vm.ServerPrefix);
    }

    [Fact]
    public void Generate_SuccessRaisesCompletionAndMasksPasswordChange()
    {
        var fixture = new Fixture();
        var vm = fixture.CreateValidViewModel();
        SetupCompletedEventArgs? completed = null;
        vm.SetupCompleted += (_, args) => completed = args;

        vm.GenerateCommand.Execute(null);

        Assert.True(vm.IsCompleted);
        Assert.NotNull(completed);
        Assert.Equal(fixture.TargetPath, completed.TargetPath);
        Assert.Equal("root", fixture.Generation.LastValues!["AMLRUN_LOGINNAME"]);
        Assert.Equal("Innovator", fixture.Generation.LastValues["COPY_SOURCE_DB_NAME"]);
        Assert.Equal("Innovator", fixture.Generation.LastValues["COPY_TARGET_DB_NAME"]);
        Assert.Equal("Innovator", fixture.Generation.LastValues["UPGRADE_DB_NAME"]);
        Assert.Equal("WIN19SQL2022", fixture.Generation.LastValues["SOURCE_DB_SERV"]);
        Assert.Equal("WIN19SQL2022", fixture.Generation.LastValues["TARGET_DB_SERV"]);
        Assert.Equal("sa", fixture.Generation.LastValues["SOURCE_SA_USER"]);
        Assert.Equal("sa", fixture.Generation.LastValues["TARGET_SA_USER"]);
        var passwordChange = Assert.Single(
            vm.GenerationChanges,
            change => change.Name == "AMLRUN_PASSWORD");
        Assert.Equal("*********", passwordChange.NewValue);
        Assert.DoesNotContain("innovator", passwordChange.NewValue, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WhenTargetExistsAndOverwriteDeclined_DoesNotWrite()
    {
        var fixture = new Fixture();
        fixture.FileSystem.AddFile(fixture.TargetPath);
        fixture.Messages.ConfirmResult = false;
        var vm = fixture.CreateValidViewModel();

        vm.GenerateCommand.Execute(null);

        Assert.Equal(0, fixture.Generation.CallCount);
        Assert.False(vm.IsCompleted);
    }

    [Fact]
    public void Generate_WithoutRequiredSelections_ShowsError()
    {
        var fixture = new Fixture();
        var vm = fixture.CreateViewModel();

        vm.GenerateCommand.Execute(null);

        Assert.Single(fixture.Messages.Errors);
        Assert.Contains("SETUP CMD", fixture.Messages.Errors[0]);
    }

    [Fact]
    public void Generate_WhenTargetCannotBeChecked_ShowsErrorInsteadOfThrowing()
    {
        var fixture = new Fixture();
        fixture.FileSystem.ThrowWhenCheckingFile(
            fixture.TargetPath,
            new UnauthorizedAccessException("沒有權限檢查目標檔。"));
        var vm = fixture.CreateValidViewModel();

        vm.GenerateCommand.Execute(null);

        Assert.Contains("沒有權限", fixture.Messages.Errors.Single());
        Assert.Equal(0, fixture.Generation.CallCount);
        Assert.False(vm.IsCompleted);
    }

    [Fact]
    public void LoadSetupPath_WhenNewSelectionIsInvalid_ClearsPreviousValidSelection()
    {
        var fixture = new Fixture();
        var vm = fixture.CreateValidViewModel();
        fixture.PathParser.Error = new ArgumentException("新的 SETUP CMD 無效。");

        vm.LoadSetupPath("invalid.cmd");
        vm.GenerateCommand.Execute(null);

        Assert.Null(vm.UpgradePaths);
        Assert.Equal(string.Empty, vm.SetupCmdPath);
        Assert.Equal(0, fixture.Generation.CallCount);
        Assert.Contains(fixture.Messages.Errors, message => message.Contains("無效"));
        Assert.Contains(fixture.Messages.Errors, message => message.Contains("SETUP CMD"));
    }

    [Fact]
    public void LoadSetupPath_NotifiesAllDerivedPathProperties()
    {
        var fixture = new Fixture();
        var vm = fixture.CreateViewModel();
        var changedProperties = new HashSet<string>();
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not null)
            {
                changedProperties.Add(args.PropertyName);
            }
        };

        vm.LoadSetupPath(fixture.Paths.SetupCmdPath);

        Assert.Contains(nameof(vm.Version), changedProperties);
        Assert.Contains(nameof(vm.VersionCode), changedProperties);
        Assert.Contains(nameof(vm.CommandFolder), changedProperties);
        Assert.Contains(nameof(vm.UpgradeRoot), changedProperties);
        Assert.Contains(nameof(vm.SupportRoot), changedProperties);
    }

    [Fact]
    public void LoadInnovatorConfig_ExposesSelectedSqlServerAndDatabase()
    {
        var fixture = new Fixture();
        var vm = fixture.CreateViewModel();

        vm.LoadInnovatorConfig(fixture.Configuration.ConfigPath);

        Assert.Equal("WIN19SQL2022", vm.SelectedSqlServer);
        Assert.Equal("Innovator", vm.SelectedDatabaseName);
        Assert.Equal("Innovator", vm.CopySourceDbName);
    }

    [Fact]
    public void SelectedConnectionChanged_WhenCopySourceDbNameAlreadyHasValue_DoesNotOverwriteIt()
    {
        var fixture = new Fixture();
        var vm = fixture.CreateValidViewModel();

        vm.CopySourceDbName = "ManuallyMaintainedSource";
        vm.SelectedConnection = vm.DatabaseConnections.Single(connection => connection.Label == "Reporting");

        Assert.Equal("ManuallyMaintainedSource", vm.CopySourceDbName);
        Assert.Equal("Reporting", vm.SelectedDatabaseName);
    }

    [Fact]
    public void CopySourceDbNameChanged_SavesSettingsAndUpdatesPreview()
    {
        var fixture = new Fixture();
        var vm = fixture.CreateValidViewModel();

        vm.CopySourceDbName = "SourceDb";

        Assert.Equal("SourceDb", fixture.Settings.SavedSettings?.CopySourceDbName);
        Assert.Equal("SourceDb", vm.VariablePreview.Single(row => row.Name == "COPY_SOURCE_DB_NAME").Value);
    }

    [Fact]
    public void Preview_MasksInnovatorAndSqlPasswords()
    {
        var fixture = new Fixture();
        var vm = fixture.CreateValidViewModel();

        vm.Password = "aras-secret";
        vm.SqlPassword = "sql-secret";

        Assert.Equal("***********", vm.VariablePreview.Single(row => row.Name == "AMLRUN_PASSWORD").Value);
        Assert.Equal("**********", vm.VariablePreview.Single(row => row.Name == "SOURCE_SA_PASS").Value);
        Assert.Equal("**********", vm.VariablePreview.Single(row => row.Name == "TARGET_SA_PASS").Value);
    }

    [Fact]
    public void PreviewAndGenerate_WhenConsoleUpgradeFolderExists_UseSupportRootRemap()
    {
        using var temp = new TemporaryDirectory();
        var supportRoot = Path.Combine(temp.Path, "Support");
        var setupPath = temp.CreateFile(
            Path.Combine("Support", "commands", "01", SetupPathParser.ExpectedFilename),
            "@SET CONSOLEUPGRADE_FOLDER=C:\\Support\\tools\\SolutionUpgrade\\consoleUpgrade\r\n");
        var expected = Path.Combine(
            supportRoot,
            "tools",
            "SolutionUpgrade",
            "consoleUpgrade");
        var paths = new UpgradePathInfo(
            setupPath,
            "01",
            supportRoot,
            temp.Path,
            "11SP!5",
            "110");
        var configuration = new InnovatorConfiguration(
            @"C:\Aras\InnovatorServerConfig.xml",
            @"C:\Aras",
            [new DatabaseConnectionOption("Main", "Innovator", "WIN19SQL2022")],
            "http://localhost/InnovatorServer");
        var generation = new StubCmdGenerationService(Path.Combine(
            Path.GetDirectoryName(setupPath)!,
            "SETUP-DEFAULTS-BUILD01.CMD"));
        var vm = new SetupStepViewModel(
            new StubSetupPathParser(paths),
            new StubInnovatorConfigService(configuration),
            new CmdVariableBuilder(),
            generation,
            new FakeFileSystem(),
            new FakeFileDialogService(),
            new FakeMessageDialogService(),
            new FakeUserSettingsService(),
            new FakeSecretProtectionService(),
            Logger.None,
            @"C:\Users\kenny\AppData\Local\ArasPatchUpgradeAssistant\logs",
            () => "BUILD01",
            new StubIomDllLocatorService());

        vm.LoadSetupPath(setupPath);
        vm.LoadInnovatorConfig(configuration.ConfigPath);

        var row = vm.VariablePreview.Single(row => row.Name == "CONSOLEUPGRADE_FOLDER");
        Assert.Equal(@"C:\Support\tools\SolutionUpgrade\consoleUpgrade", row.OriginalValue);
        Assert.Equal(expected, row.Value);
        Assert.Equal("Updated", row.Status);

        vm.GenerateCommand.Execute(null);

        Assert.Equal(expected, generation.LastValues!["CONSOLEUPGRADE_FOLDER"]);
    }

    [Fact]
    public void PreviewAndGenerate_WhenAdditionalSupportPathVariablesExist_UseSupportRootRemap()
    {
        using var temp = new TemporaryDirectory();
        var supportRoot = Path.Combine(temp.Path, "Support");
        var setupPath = temp.CreateFile(
            Path.Combine("Support", "commands", "01", SetupPathParser.ExpectedFilename),
            "@SET MS_DTS_LOG_DIR=C:\\Support\\LOGS\\DTS\r\n" +
            "@SET LOG_TRUNCATE_DEST=C:\\Support\\backup\\DeleteMeLog.bak\r\n" +
            "@SET UPDATES_CATALOG=C:\\Support\\Patches\\100\\core\\pre-patches.manifest.xml\r\n" +
            "@SET POST_UPDATES_CATALOG=C:\\Support\\Patches\\100\\core\\post-patches.manifest.xml\r\n" +
            "@SET UPDATES_FOLDER=C:\\Support\\Patches\\100\\core\r\n" +
            "@SET PLM_POST_PATCHES=C:\\Support\\Patches\\100\\PLM\\post\r\n");
        var expected = new Dictionary<string, string>
        {
            ["MS_DTS_LOG_DIR"] = Path.Combine(
                supportRoot,
                "LOGS",
                "DTS"),
            ["LOG_TRUNCATE_DEST"] = Path.Combine(
                supportRoot,
                "backup",
                "DeleteMeLog.bak"),
            ["UPDATES_CATALOG"] = Path.Combine(
                supportRoot,
                "Patches",
                "100",
                "core",
                "pre-patches.manifest.xml"),
            ["POST_UPDATES_CATALOG"] = Path.Combine(
                supportRoot,
                "Patches",
                "100",
                "core",
                "post-patches.manifest.xml"),
            ["UPDATES_FOLDER"] = Path.Combine(
                supportRoot,
                "Patches",
                "100",
                "core"),
            ["PLM_POST_PATCHES"] = Path.Combine(
                supportRoot,
                "Patches",
                "100",
                "PLM",
                "post")
        };
        var paths = new UpgradePathInfo(
            setupPath,
            "01",
            supportRoot,
            temp.Path,
            "11SP!5",
            "110");
        var configuration = new InnovatorConfiguration(
            @"C:\Aras\InnovatorServerConfig.xml",
            @"C:\Aras",
            [new DatabaseConnectionOption("Main", "Innovator", "WIN19SQL2022")],
            "http://localhost/InnovatorServer");
        var generation = new StubCmdGenerationService(Path.Combine(
            Path.GetDirectoryName(setupPath)!,
            "SETUP-DEFAULTS-BUILD01.CMD"));
        var vm = new SetupStepViewModel(
            new StubSetupPathParser(paths),
            new StubInnovatorConfigService(configuration),
            new CmdVariableBuilder(),
            generation,
            new FakeFileSystem(),
            new FakeFileDialogService(),
            new FakeMessageDialogService(),
            new FakeUserSettingsService(),
            new FakeSecretProtectionService(),
            Logger.None,
            @"C:\Users\kenny\AppData\Local\ArasPatchUpgradeAssistant\logs",
            () => "BUILD01",
            new StubIomDllLocatorService());

        vm.LoadSetupPath(setupPath);
        vm.LoadInnovatorConfig(configuration.ConfigPath);

        foreach (var pair in expected)
        {
            var row = vm.VariablePreview.Single(row => row.Name == pair.Key);
            Assert.Equal(pair.Value, row.Value);
            Assert.Equal("Updated", row.Status);
        }

        vm.GenerateCommand.Execute(null);

        foreach (var pair in expected)
        {
            Assert.Equal(pair.Value, generation.LastValues![pair.Key]);
        }
    }

    [Fact]
    public void Preview_WhenTargetIomDllExistsAndFound_ShowsUpdatedActualPath()
    {
        using var temp = new TemporaryDirectory();
        var supportRoot = Path.Combine(temp.Path, "Support");
        var setupPath = temp.CreateFile(
            Path.Combine("Support", "commands", "01", SetupPathParser.ExpectedFilename),
            "@SET TARGET_IOM_DLL=c:\\Support\\tools\\SolutionUpgrade\\Import\\IOM.dll\r\n");
        var foundIom = Path.Combine(
            supportRoot,
            "tools",
            "SolutionUpgrade",
            "consoleUpgrade",
            "IOM.dll");
        var paths = new UpgradePathInfo(
            setupPath,
            "01",
            supportRoot,
            temp.Path,
            "10SP1",
            "100");
        var configuration = new InnovatorConfiguration(
            @"C:\Aras\InnovatorServerConfig.xml",
            @"C:\Aras",
            [new DatabaseConnectionOption("Main", "Innovator", "WIN19SQL2022")],
            "http://localhost/InnovatorServer");
        var locator = new StubIomDllLocatorService { PathToReturn = foundIom };
        var vm = new SetupStepViewModel(
            new StubSetupPathParser(paths),
            new StubInnovatorConfigService(configuration),
            new CmdVariableBuilder(),
            new StubCmdGenerationService(Path.Combine(
                Path.GetDirectoryName(setupPath)!,
                "SETUP-DEFAULTS-BUILD01.CMD")),
            new FakeFileSystem(),
            new FakeFileDialogService(),
            new FakeMessageDialogService(),
            new FakeUserSettingsService(),
            new FakeSecretProtectionService(),
            Logger.None,
            @"C:\Users\kenny\AppData\Local\ArasPatchUpgradeAssistant\logs",
            () => "BUILD01",
            locator);

        vm.LoadSetupPath(setupPath);
        vm.LoadInnovatorConfig(configuration.ConfigPath);

        var row = vm.VariablePreview.Single(row => row.Name == "TARGET_IOM_DLL");
        Assert.Equal(@"c:\Support\tools\SolutionUpgrade\Import\IOM.dll", row.OriginalValue);
        Assert.Equal(foundIom, row.Value);
        Assert.Equal("Updated", row.Status);
        Assert.Equal(string.Empty, vm.PreviewWarningMessage);
    }

    [Fact]
    public void Preview_WhenTargetIomDllNotFound_ShowsWarningAndFallbackRemap()
    {
        using var temp = new TemporaryDirectory();
        var supportRoot = Path.Combine(temp.Path, "Support");
        var setupPath = temp.CreateFile(
            Path.Combine("Support", "commands", "01", SetupPathParser.ExpectedFilename),
            "@SET TARGET_IOM_DLL=c:\\Support\\tools\\SolutionUpgrade\\Import\\IOM.dll\r\n");
        var paths = new UpgradePathInfo(
            setupPath,
            "01",
            supportRoot,
            temp.Path,
            "10SP1",
            "100");
        var configuration = new InnovatorConfiguration(
            @"C:\Aras\InnovatorServerConfig.xml",
            @"C:\Aras",
            [new DatabaseConnectionOption("Main", "Innovator", "WIN19SQL2022")],
            "http://localhost/InnovatorServer");
        var vm = new SetupStepViewModel(
            new StubSetupPathParser(paths),
            new StubInnovatorConfigService(configuration),
            new CmdVariableBuilder(),
            new StubCmdGenerationService(Path.Combine(
                Path.GetDirectoryName(setupPath)!,
                "SETUP-DEFAULTS-BUILD01.CMD")),
            new FakeFileSystem(),
            new FakeFileDialogService(),
            new FakeMessageDialogService(),
            new FakeUserSettingsService(),
            new FakeSecretProtectionService(),
            Logger.None,
            @"C:\Users\kenny\AppData\Local\ArasPatchUpgradeAssistant\logs",
            () => "BUILD01",
            new StubIomDllLocatorService());

        vm.LoadSetupPath(setupPath);
        vm.LoadInnovatorConfig(configuration.ConfigPath);

        var row = vm.VariablePreview.Single(row => row.Name == "TARGET_IOM_DLL");
        Assert.Equal(
            Path.Combine(
                supportRoot,
                "tools",
                "SolutionUpgrade",
                "Import",
                "IOM.dll"),
            row.Value);
        Assert.Equal("Warning", row.Status);
        Assert.Contains("IOM.dll", vm.PreviewWarningMessage);
    }

    [Fact]
    public void Constructor_WhenSavedPathsExist_AutoLoadsSetupAndInnovatorConfig()
    {
        var fixture = new Fixture();
        fixture.FileSystem.AddFile(fixture.Paths.SetupCmdPath);
        fixture.FileSystem.AddFile(fixture.Configuration.ConfigPath);
        fixture.Settings.SettingsToLoad = new UserSettings
        {
            SetupDefaultsTemplatePath = fixture.Paths.SetupCmdPath,
            InnovatorServerConfigPath = fixture.Configuration.ConfigPath,
            SelectedDatabaseId = "Reporting",
            SelectedDatabaseName = "Reporting",
            CopySourceDbName = "SavedSourceDb",
            LoginName = "admin",
            EncryptedPassword = fixture.Secret.Protect("aras-secret"),
            SqlLoginName = "sqladmin",
            EncryptedSqlPassword = fixture.Secret.Protect("sql-secret")
        };

        var vm = fixture.CreateViewModel();

        Assert.Equal(fixture.Paths.SetupCmdPath, vm.SetupCmdPath);
        Assert.Equal(fixture.Configuration.ConfigPath, vm.InnovatorConfigPath);
        Assert.Equal("Reporting", vm.SelectedConnection?.Label);
        Assert.Equal("SavedSourceDb", vm.CopySourceDbName);
        Assert.Equal("admin", vm.LoginName);
        Assert.Equal("aras-secret", vm.Password);
        Assert.Equal("sqladmin", vm.SqlLoginName);
        Assert.Equal("sql-secret", vm.SqlPassword);
        Assert.Equal(fixture.Settings.SettingsFilePath, vm.SettingsFilePath);
    }

    [Fact]
    public void Constructor_WhenEncryptedPasswordCannotBeDecrypted_UsesDefaultsAndShowsError()
    {
        var fixture = new Fixture();
        fixture.Secret.ThrowOnUnprotect = true;
        fixture.Settings.SettingsToLoad = new UserSettings
        {
            EncryptedPassword = "broken",
            EncryptedSqlPassword = "also-broken"
        };

        var vm = fixture.CreateViewModel();

        Assert.Equal("innovator", vm.Password);
        Assert.Equal(string.Empty, vm.SqlPassword);
        Assert.Contains(fixture.Messages.Errors, message => message.Contains("密碼無法解密"));
    }

    [Fact]
    public void Constructor_WhenSavedPathDoesNotExist_KeepsSettingsButLeavesScreenBlank()
    {
        var fixture = new Fixture();
        fixture.Settings.SettingsToLoad = new UserSettings
        {
            SetupDefaultsTemplatePath = fixture.Paths.SetupCmdPath,
            InnovatorServerConfigPath = fixture.Configuration.ConfigPath,
            LoginName = "admin"
        };

        var vm = fixture.CreateViewModel();

        Assert.Equal(string.Empty, vm.SetupCmdPath);
        Assert.Equal(string.Empty, vm.InnovatorConfigPath);
        Assert.Contains(fixture.Messages.Errors, message => message.Contains("上次選取"));
        Assert.Equal(0, fixture.Settings.SaveCallCount);
    }

    [Fact]
    public void BrowseSetup_WhenSelectionLoadsSuccessfully_SavesSettings()
    {
        var fixture = new Fixture();
        fixture.FileDialog.SetupPath = fixture.Paths.SetupCmdPath;
        var vm = fixture.CreateViewModel();

        vm.BrowseSetupCommand.Execute(null);

        Assert.Equal(fixture.Paths.SetupCmdPath, fixture.Settings.SavedSettings?.SetupDefaultsTemplatePath);
    }

    [Fact]
    public void BrowseInnovatorConfig_WhenSelectionLoadsSuccessfully_SavesPathAndSelectedDatabase()
    {
        var fixture = new Fixture();
        fixture.FileDialog.ConfigPath = fixture.Configuration.ConfigPath;
        var vm = fixture.CreateViewModel();

        vm.BrowseInnovatorConfigCommand.Execute(null);

        Assert.Equal(fixture.Configuration.ConfigPath, fixture.Settings.SavedSettings?.InnovatorServerConfigPath);
        Assert.Equal("Main", fixture.Settings.SavedSettings?.SelectedDatabaseId);
        Assert.Equal("Innovator", fixture.Settings.SavedSettings?.SelectedDatabaseName);
    }

    [Fact]
    public void ChangingDatabaseLoginAndPasswords_SavesEncryptedSettings()
    {
        var fixture = new Fixture();
        var vm = fixture.CreateValidViewModel();

        vm.SelectedConnection = vm.DatabaseConnections.Single(connection => connection.Label == "Reporting");
        vm.LoginName = "superuser";
        vm.Password = "new-aras-secret";
        vm.SqlLoginName = "sqladmin";
        vm.SqlPassword = "new-sql-secret";
        vm.CopySourceDbName = "SourceReporting";

        Assert.Equal("Reporting", fixture.Settings.SavedSettings?.SelectedDatabaseId);
        Assert.Equal("Reporting", fixture.Settings.SavedSettings?.SelectedDatabaseName);
        Assert.Equal("SourceReporting", fixture.Settings.SavedSettings?.CopySourceDbName);
        Assert.Equal("superuser", fixture.Settings.SavedSettings?.LoginName);
        Assert.NotEqual("new-aras-secret", fixture.Settings.SavedSettings?.EncryptedPassword);
        Assert.Equal("sqladmin", fixture.Settings.SavedSettings?.SqlLoginName);
        Assert.NotEqual("new-sql-secret", fixture.Settings.SavedSettings?.EncryptedSqlPassword);
        Assert.DoesNotContain("new-aras-secret", fixture.Settings.SavedSettings?.EncryptedPassword);
        Assert.DoesNotContain("new-sql-secret", fixture.Settings.SavedSettings?.EncryptedSqlPassword);
    }

    [Fact]
    public void Generate_WhenDbConnectionHasNoServer_ShowsErrorAndDoesNotWrite()
    {
        var fixture = new Fixture(useConnectionWithoutServer: true);
        var vm = fixture.CreateValidViewModel();

        vm.GenerateCommand.Execute(null);

        Assert.Equal(0, fixture.Generation.CallCount);
        Assert.Contains(fixture.Messages.Errors, message => message.Contains("DB-Connection 未定義 server"));
    }

    [Fact]
    public void Generate_WhenCopySourceDbNameIsBlank_ShowsErrorAndDoesNotWrite()
    {
        var fixture = new Fixture();
        var vm = fixture.CreateValidViewModel();
        vm.CopySourceDbName = string.Empty;

        vm.GenerateCommand.Execute(null);

        Assert.Equal(0, fixture.Generation.CallCount);
        Assert.Contains(fixture.Messages.Errors, message => message.Contains("COPY_SOURCE_DB_NAME"));
    }

    [Fact]
    public void ExpandAndCollapseAllSections_UpdatesAllSectionFlags()
    {
        var fixture = new Fixture();
        var vm = fixture.CreateViewModel();

        vm.CollapseAllSectionsCommand.Execute(null);

        Assert.False(vm.IsSetupPathSectionExpanded);
        Assert.False(vm.IsInnovatorSectionExpanded);
        Assert.False(vm.IsSqlLoginSectionExpanded);
        Assert.False(vm.IsPreviewSectionExpanded);
        Assert.False(vm.IsResultSectionExpanded);

        vm.ExpandAllSectionsCommand.Execute(null);

        Assert.True(vm.IsSetupPathSectionExpanded);
        Assert.True(vm.IsInnovatorSectionExpanded);
        Assert.True(vm.IsSqlLoginSectionExpanded);
        Assert.True(vm.IsPreviewSectionExpanded);
        Assert.True(vm.IsResultSectionExpanded);
    }

    private sealed class Fixture
    {
        private readonly string _supportRoot =
            Path.Combine("K:\\", "Upgrade", "12SP18", "Support");

        public Fixture(bool useConnectionWithoutServer = false)
        {
            Paths = new UpgradePathInfo(
                Path.Combine(_supportRoot, "commands", "01", SetupPathParser.ExpectedFilename),
                "01",
                _supportRoot,
                Directory.GetParent(_supportRoot)!.FullName,
                "12SP18",
                "120");
            Configuration = new InnovatorConfiguration(
                @"C:\Aras\InnovatorServerConfig.xml",
                @"C:\Aras",
                useConnectionWithoutServer
                    ? [new DatabaseConnectionOption("Main", "Innovator")]
                    : [
                        new DatabaseConnectionOption("Main", "Innovator", "WIN19SQL2022"),
                        new DatabaseConnectionOption("Reporting", "Reporting", "SQL02")
                    ],
                "http://localhost/InnovatorServer");
            TargetPath = Path.Combine(
                _supportRoot, "commands", "01", "SETUP-DEFAULTS-BUILD01.CMD");
            Generation = new StubCmdGenerationService(TargetPath);
            PathParser = new ConfigurableSetupPathParser(Paths);
        }

        public UpgradePathInfo Paths { get; }
        public InnovatorConfiguration Configuration { get; }
        public string TargetPath { get; }
        public FakeFileSystem FileSystem { get; } = new();
        public FakeMessageDialogService Messages { get; } = new();
        public FakeFileDialogService FileDialog { get; } = new();
        public FakeUserSettingsService Settings { get; } = new();
        public FakeSecretProtectionService Secret { get; } = new();
        public StubCmdGenerationService Generation { get; }
        public ConfigurableSetupPathParser PathParser { get; }

        public SetupStepViewModel CreateViewModel() =>
            new(
                PathParser,
                new StubInnovatorConfigService(Configuration),
                new CmdVariableBuilder(),
                Generation,
                FileSystem,
                FileDialog,
                Messages,
                Settings,
                Secret,
                Logger.None,
                @"C:\Users\kenny\AppData\Local\ArasPatchUpgradeAssistant\logs",
                () => "BUILD01");

        public SetupStepViewModel CreateValidViewModel()
        {
            var vm = CreateViewModel();
            vm.LoadSetupPath(Paths.SetupCmdPath);
            vm.LoadInnovatorConfig(Configuration.ConfigPath);
            return vm;
        }
    }
}
