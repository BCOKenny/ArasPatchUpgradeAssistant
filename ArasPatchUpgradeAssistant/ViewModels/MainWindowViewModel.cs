using ArasPatchUpgradeAssistant.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArasPatchUpgradeAssistant.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IMessageDialogService _messageDialogService;
    private SetupCompletedEventArgs? _completedSetup;

    public MainWindowViewModel(
        SetupStepViewModel setupStep,
        DirectoryValidationStepViewModel directoryStep,
        UpgradeOptionsStepViewModel upgradeOptionsStep,
        IMessageDialogService messageDialogService)
    {
        SetupStep = setupStep;
        DirectoryStep = directoryStep;
        UpgradeOptionsStep = upgradeOptionsStep;
        _messageDialogService = messageDialogService;
        _currentContent = setupStep;
        setupStep.SetupCompleted += OnSetupCompleted;
        setupStep.SetupInvalidated += OnSetupInvalidated;
    }

    public SetupStepViewModel SetupStep { get; }

    public DirectoryValidationStepViewModel DirectoryStep { get; }

    public UpgradeOptionsStepViewModel UpgradeOptionsStep { get; }

    [ObservableProperty]
    private int _currentStep = 1;

    [ObservableProperty]
    private object _currentContent;

    [RelayCommand]
    private void Navigate(object? stepParameter)
    {
        var step = stepParameter switch
        {
            int number => number,
            string text when int.TryParse(text, out var number) => number,
            _ => 0
        };

        if (step is < 1 or > 6)
        {
            return;
        }

        if (step == 1)
        {
            CurrentStep = 1;
            CurrentContent = SetupStep;
            return;
        }

        if (_completedSetup is null)
        {
            _messageDialogService.ShowError("請先完成基本設定。");
            return;
        }

        if (step == 2)
        {
            DirectoryStep.Configure(
                _completedSetup.Paths,
                _completedSetup.TargetPath);
            DirectoryStep.RefreshCommand.Execute(null);
            CurrentStep = 2;
            CurrentContent = DirectoryStep;
            return;
        }

        if (step == 3)
        {
            UpgradeOptionsStep.Configure(_completedSetup);
            UpgradeOptionsStep.RefreshCommand.Execute(null);
            CurrentStep = 3;
            CurrentContent = UpgradeOptionsStep;
            return;
        }

        _messageDialogService.ShowError("此步驟尚未在第一版實作。");
    }

    private void OnSetupCompleted(object? sender, SetupCompletedEventArgs args)
    {
        _completedSetup = args;
    }

    private void OnSetupInvalidated(object? sender, EventArgs args)
    {
        _completedSetup = null;
    }
}
