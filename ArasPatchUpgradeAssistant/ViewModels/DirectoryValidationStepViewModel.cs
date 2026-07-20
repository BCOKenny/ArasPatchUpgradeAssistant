using System.Collections.ObjectModel;
using ArasPatchUpgradeAssistant.Models;
using ArasPatchUpgradeAssistant.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArasPatchUpgradeAssistant.ViewModels;

public partial class DirectoryValidationStepViewModel : ObservableObject
{
    private readonly IDirectoryValidationService _validationService;
    private readonly IMessageDialogService _messageDialogService;
    private UpgradePathInfo? _paths;
    private string? _generatedCmdPath;

    public DirectoryValidationStepViewModel(
        IDirectoryValidationService validationService,
        IMessageDialogService messageDialogService)
    {
        _validationService = validationService;
        _messageDialogService = messageDialogService;
    }

    public ObservableCollection<DirectoryValidationItem> Items { get; } = [];

    [ObservableProperty]
    private string _upgradeRoot = string.Empty;

    [ObservableProperty]
    private string _version = string.Empty;

    [ObservableProperty]
    private DateTimeOffset? _lastCheckedAt;

    [ObservableProperty]
    private int _okCount;

    [ObservableProperty]
    private int _missingCount;

    [ObservableProperty]
    private int _warningCount;

    public void Configure(UpgradePathInfo paths, string generatedCmdPath)
    {
        _paths = paths;
        _generatedCmdPath = generatedCmdPath;
        UpgradeRoot = paths.UpgradeRoot;
        Version = paths.Version;
    }

    [RelayCommand]
    private void Refresh()
    {
        if (_paths is null || string.IsNullOrWhiteSpace(_generatedCmdPath))
        {
            return;
        }

        try
        {
            var snapshot = _validationService.Validate(_paths, _generatedCmdPath);
            Items.Clear();
            foreach (var item in snapshot.Items)
            {
                Items.Add(item);
            }

            LastCheckedAt = snapshot.CheckedAt;
            OkCount = Items.Count(item => item.Status == DirectoryValidationStatus.OK);
            MissingCount = Items.Count(item => item.Status == DirectoryValidationStatus.Missing);
            WarningCount = Items.Count(item => item.Status == DirectoryValidationStatus.Warning);
        }
        catch (Exception exception)
        {
            _messageDialogService.ShowError(exception.Message);
        }
    }

    [RelayCommand]
    private void CreateFolder(DirectoryValidationItem? item)
    {
        if (item is null || !item.CanCreate)
        {
            return;
        }

        if (!_messageDialogService.Confirm(
                "建立資料夾",
                $"是否建立以下資料夾？{Environment.NewLine}{item.FullPath}"))
        {
            return;
        }

        try
        {
            _validationService.CreateDirectory(item);
            Refresh();
        }
        catch (Exception exception)
        {
            _messageDialogService.ShowError(exception.Message);
        }
    }
}
