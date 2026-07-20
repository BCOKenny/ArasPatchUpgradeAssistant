using System.Windows;
using ArasPatchUpgradeAssistant.Services;
using ArasPatchUpgradeAssistant.ViewModels;
using ArasPatchUpgradeAssistant.Views;
using Serilog;

namespace ArasPatchUpgradeAssistant;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Log.Logger = LoggingConfigurator.CreateLogger();
        Log.Information("Application started");

        try
        {
            var window = CreateMainWindow();
            MainWindow = window;
            window.Show();
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "Application startup failed");
            MessageBox.Show(
                exception.Message,
                "Aras Innovator Patches 升級助手",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Application exited");
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static MainWindow CreateMainWindow()
    {
        var logger = Log.Logger;
        var fileSystem = new SystemFileSystem();
        var messageDialogs = new WpfMessageDialogService();
        var secretProtectionService = new SecretProtectionService();
        var aiSettingsService = new AiPatchDescriptionSettingsService(logger: logger);
        var aiPatchDescriptionService = new AiPatchDescriptionService(logger);
        var vaultConfigService = new VaultConfigService(logger);
        var setupStep = new SetupStepViewModel(
            new SetupPathParser(),
            new InnovatorConfigService(vaultConfigService, logger),
            new CmdVariableBuilder(),
            new CmdGenerationService(),
            fileSystem,
            new WpfFileDialogService(),
            messageDialogs,
            new UserSettingsService(logger: logger),
            secretProtectionService,
            logger,
            LoggingConfigurator.GetDefaultLogDirectory(),
            iomDllLocatorService: new IomDllLocatorService(logger));
        var directoryStep = new DirectoryValidationStepViewModel(
            new DirectoryValidationService(fileSystem, logger),
            messageDialogs);
        var upgradeOptionsStep = new UpgradeOptionsStepViewModel(
            fileSystem,
            logger,
            messageDialogs,
            new PatchExplanationService(
                logger,
                aiPatchDescriptionService,
                aiSettingsService,
                secretProtectionService),
            aiSettingsService,
            secretProtectionService);

        return new MainWindow(
            new MainWindowViewModel(
                setupStep,
                directoryStep,
                upgradeOptionsStep,
                messageDialogs));
    }
}
