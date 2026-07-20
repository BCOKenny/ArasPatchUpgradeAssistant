using ArasPatchUpgradeAssistant.Models;
using ArasPatchUpgradeAssistant.Services;
using ArasPatchUpgradeAssistant.Tests.TestDoubles;
using ArasPatchUpgradeAssistant.ViewModels;
using Serilog.Core;

namespace ArasPatchUpgradeAssistant.Tests.ViewModels;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void NavigateToDirectoryValidation_BeforeSetup_ShowsBlockingMessage()
    {
        var fixture = new Fixture();
        var vm = fixture.CreateMainViewModel();

        vm.NavigateCommand.Execute("2");

        Assert.Equal(1, vm.CurrentStep);
        Assert.Contains("請先完成基本設定", fixture.Messages.Errors.Single());
    }

    [Fact]
    public void NavigateToDirectoryValidation_AfterSetup_RefreshesAndNavigates()
    {
        var fixture = new Fixture();
        var vm = fixture.CreateMainViewModel();
        fixture.Setup.LoadSetupPath(fixture.Paths.SetupCmdPath);
        fixture.Setup.LoadInnovatorConfig(fixture.Configuration.ConfigPath);
        fixture.Setup.GenerateCommand.Execute(null);

        vm.NavigateCommand.Execute(2);

        Assert.Equal(2, vm.CurrentStep);
        Assert.Same(fixture.DirectoryStep, vm.CurrentContent);
        Assert.Equal(1, fixture.Validation.ValidateCallCount);
    }

    [Fact]
    public void NavigateToUpgradeOptions_AfterSetup_ScansAndNavigates()
    {
        var fixture = new Fixture();
        var vm = fixture.CreateMainViewModel();
        fixture.Setup.LoadSetupPath(fixture.Paths.SetupCmdPath);
        fixture.Setup.LoadInnovatorConfig(fixture.Configuration.ConfigPath);
        fixture.Setup.GenerateCommand.Execute(null);

        vm.NavigateCommand.Execute(3);

        Assert.Equal(3, vm.CurrentStep);
        Assert.Same(fixture.UpgradeOptionsStep, vm.CurrentContent);
    }

    [Fact]
    public void NavigateToFutureStep_AfterSetup_ShowsNotImplementedMessage()
    {
        var fixture = new Fixture();
        var vm = fixture.CreateMainViewModel();
        fixture.Setup.LoadSetupPath(fixture.Paths.SetupCmdPath);
        fixture.Setup.LoadInnovatorConfig(fixture.Configuration.ConfigPath);
        fixture.Setup.GenerateCommand.Execute(null);

        vm.NavigateCommand.Execute(4);

        Assert.Equal(1, vm.CurrentStep);
        Assert.Contains("尚未在第一版實作", fixture.Messages.Errors.Single());
    }

    [Fact]
    public void NavigateToDirectoryValidation_AfterSetupChanges_BlocksUntilRegenerated()
    {
        var fixture = new Fixture();
        var vm = fixture.CreateMainViewModel();
        fixture.Setup.LoadSetupPath(fixture.Paths.SetupCmdPath);
        fixture.Setup.LoadInnovatorConfig(fixture.Configuration.ConfigPath);
        fixture.Setup.GenerateCommand.Execute(null);

        fixture.Setup.LoginName = "changed-user";
        vm.NavigateCommand.Execute("2");

        Assert.Equal(1, vm.CurrentStep);
        Assert.Contains("請先完成基本設定", fixture.Messages.Errors.Single());
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Paths = new UpgradePathInfo(
                @"K:\Upgrade\12SP18\Support\commands\01\SETUP-DEFAULTS-MACHINENAME.CMD",
                "01",
                @"K:\Upgrade\12SP18\Support",
                @"K:\Upgrade\12SP18",
                "12SP18",
                "120");
            Configuration = new InnovatorConfiguration(
                @"C:\Aras\InnovatorServerConfig.xml",
                @"C:\Aras",
                [new DatabaseConnectionOption("Main", "Innovator", "WIN19SQL2022")],
                "http://localhost/InnovatorServer");
            var target = @"K:\Upgrade\12SP18\Support\commands\01\SETUP-DEFAULTS-BUILD01.CMD";
            Setup = new SetupStepViewModel(
                new StubSetupPathParser(Paths),
                new StubInnovatorConfigService(Configuration),
                new CmdVariableBuilder(),
                new StubCmdGenerationService(target),
                new FakeFileSystem(),
                new FakeFileDialogService(),
                Messages,
                new FakeUserSettingsService(),
                new FakeSecretProtectionService(),
                Logger.None,
                @"C:\Users\kenny\AppData\Local\ArasPatchUpgradeAssistant\logs",
                () => "BUILD01");
            DirectoryStep = new DirectoryValidationStepViewModel(Validation, Messages);
            UpgradeOptionsStep = new UpgradeOptionsStepViewModel(new FakeFileSystem());
        }

        public UpgradePathInfo Paths { get; }
        public InnovatorConfiguration Configuration { get; }
        public FakeMessageDialogService Messages { get; } = new();
        public FakeDirectoryValidationService Validation { get; } = new();
        public SetupStepViewModel Setup { get; }
        public DirectoryValidationStepViewModel DirectoryStep { get; }
        public UpgradeOptionsStepViewModel UpgradeOptionsStep { get; }

        public MainWindowViewModel CreateMainViewModel() =>
            new(Setup, DirectoryStep, UpgradeOptionsStep, Messages);
    }
}
