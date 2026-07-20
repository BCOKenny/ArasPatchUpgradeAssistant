using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ArasPatchUpgradeAssistant.Models;
using ArasPatchUpgradeAssistant.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Serilog;
using Serilog.Core;

namespace ArasPatchUpgradeAssistant.ViewModels;

public sealed partial class UpgradeOptionsStepViewModel : ObservableObject
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly IFileSystem _fileSystem;
    private readonly ILogger _logger;
    private readonly IMessageDialogService? _messageDialogService;
    private readonly PatchExplanationService _patchExplanationService;
    private readonly AiPatchDescriptionSettingsService _aiSettingsService;
    private readonly ISecretProtectionService _secretProtectionService;
    private SetupCompletedEventArgs? _setup;
    private bool _suppressPlanSave;
    private UpgradePlanState _upgradePlan = new();

    public UpgradeOptionsStepViewModel(
        IFileSystem fileSystem,
        ILogger? logger = null,
        IMessageDialogService? messageDialogService = null,
        PatchExplanationService? patchExplanationService = null,
        AiPatchDescriptionSettingsService? aiSettingsService = null,
        ISecretProtectionService? secretProtectionService = null)
    {
        _fileSystem = fileSystem;
        _logger = logger ?? Logger.None;
        _messageDialogService = messageDialogService;
        _aiSettingsService = aiSettingsService ?? new AiPatchDescriptionSettingsService(logger: _logger);
        _secretProtectionService = secretProtectionService ?? new SecretProtectionService();
        _patchExplanationService = patchExplanationService ?? new PatchExplanationService(_logger);
        AiSettingsFilePath = _aiSettingsService.SettingsFilePath;
        LoadAiSettings();
    }

    public ObservableCollection<BatExecutionPlanItemViewModel> BatFiles { get; } = [];

    public IReadOnlyDictionary<string, string> SetupVariables { get; private set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    private string _setupCmdPath = string.Empty;

    [ObservableProperty]
    private string _generatedSetupCmdPath = string.Empty;

    [ObservableProperty]
    private string _commandFolder = string.Empty;

    [ObservableProperty]
    private string _commandsDirectory = string.Empty;

    [ObservableProperty]
    private string _supportRoot = string.Empty;

    [ObservableProperty]
    private string _patchesBase = string.Empty;

    [ObservableProperty]
    private string _solutionsBase = string.Empty;

    [ObservableProperty]
    private int _setupVariableCount;

    [ObservableProperty]
    private int _commandsDirectoryFileCount;

    [ObservableProperty]
    private int _batFileCount;

    [ObservableProperty]
    private DateTimeOffset? _lastScannedAt;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _enableAiPatchDescription;

    [ObservableProperty]
    private string _openAiBaseUrl = "https://api.openai.com/v1";

    [ObservableProperty]
    private string _openAiModel = "gpt-4.1-mini";

    [ObservableProperty]
    private string _openAiApiKey = string.Empty;

    [ObservableProperty]
    private int _requestTimeoutSeconds = 60;

    [ObservableProperty]
    private int _maxBodyPreviewLines = 80;

    [ObservableProperty]
    private bool _enableAiRequestDebugLog;

    [ObservableProperty]
    private string _aiSettingsFilePath = string.Empty;

    public void Configure(SetupCompletedEventArgs setup)
    {
        ArgumentNullException.ThrowIfNull(setup);

        _setup = setup;
        SetupCmdPath = setup.Paths.SetupCmdPath;
        GeneratedSetupCmdPath = setup.TargetPath;
        CommandFolder = setup.Paths.CommandFolder;
        CommandsDirectory = Path.GetDirectoryName(setup.Paths.SetupCmdPath) ?? string.Empty;
        SupportRoot = setup.Paths.SupportRoot;
        PatchesBase = DetectPatchesBase(SupportRoot);
        SolutionsBase = DetectSolutionsBase(SupportRoot);
        SetupVariables = ReadSetupVariables();
        SetupVariableCount = SetupVariables.Count;
        CommandsDirectoryFileCount = CountCommandsDirectoryFiles();
        OnPropertyChanged(nameof(SetupVariables));
        _upgradePlan = LoadUpgradePlan();
    }

    [RelayCommand]
    private void Refresh() => ScanBatFiles();

    [RelayCommand]
    private void SelectAll()
    {
        _suppressPlanSave = true;
        foreach (var item in BatFiles)
        {
            item.IsChecked = true;
        }

        _suppressPlanSave = false;
        SaveUpgradePlan();
    }

    [RelayCommand]
    private void DeselectAll()
    {
        _suppressPlanSave = true;
        foreach (var item in BatFiles)
        {
            item.IsChecked = false;
        }

        _suppressPlanSave = false;
        SaveUpgradePlan();
    }

    [RelayCommand]
    private void ExpandAll()
    {
        foreach (var item in BatFiles)
        {
            item.IsExpanded = item.HasChildren;
        }
    }

    [RelayCommand]
    private void CollapseAll()
    {
        foreach (var item in BatFiles)
        {
            item.IsExpanded = false;
        }
    }

    [RelayCommand]
    private void SaveAiSettings()
    {
        try
        {
            _aiSettingsService.Save(new AiPatchDescriptionSettings
            {
                EnableAiPatchDescription = EnableAiPatchDescription,
                OpenAiBaseUrl = OpenAiBaseUrl,
                OpenAiModel = OpenAiModel,
                EncryptedOpenAiApiKey = _secretProtectionService.Protect(OpenAiApiKey),
                RequestTimeoutSeconds = RequestTimeoutSeconds,
                MaxBodyPreviewLines = MaxBodyPreviewLines,
                EnableAiRequestDebugLog = EnableAiRequestDebugLog
            });
            StatusMessage = $"AI Patch 中文說明設定已儲存：{AiSettingsFilePath}";
        }
        catch (Exception exception)
        {
            _logger.Warning(
                exception,
                "Save AI patch description settings from view model failed {SettingsFilePath}",
                AiSettingsFilePath);
            StatusMessage = $"AI 設定儲存失敗：{exception.Message}";
            _messageDialogService?.ShowError(StatusMessage);
        }
    }

    private void LoadAiSettings()
    {
        var settings = _aiSettingsService.Load();
        EnableAiPatchDescription = settings.EnableAiPatchDescription;
        OpenAiBaseUrl = settings.OpenAiBaseUrl;
        OpenAiModel = settings.OpenAiModel;
        RequestTimeoutSeconds = settings.RequestTimeoutSeconds;
        MaxBodyPreviewLines = settings.MaxBodyPreviewLines;
        EnableAiRequestDebugLog = settings.EnableAiRequestDebugLog;

        if (string.IsNullOrWhiteSpace(settings.EncryptedOpenAiApiKey))
        {
            OpenAiApiKey = string.Empty;
            return;
        }

        try
        {
            OpenAiApiKey = _secretProtectionService.Unprotect(settings.EncryptedOpenAiApiKey);
        }
        catch (Exception exception)
        {
            _logger.Warning(
                exception,
                "AI patch description API key decryption failed {SettingsFilePath}",
                AiSettingsFilePath);
            OpenAiApiKey = string.Empty;
            StatusMessage = "已儲存的 OpenAI API Key 無法解密，請重新輸入。";
        }
    }

    [RelayCommand(CanExecute = nameof(CanInsertExternalBatItem))]
    private void InsertExternalBatItem(BatExecutionPlanItemViewModel? target)
    {
        if (target is null)
        {
            return;
        }

        var externalItem = CreateExternalPlanItem(
            level: ExternalItemLevel.Bat,
            targetBatFile: null,
            insertBeforeKey: target.GetPlanKey());
        if (externalItem is null)
        {
            return;
        }

        var insertIndex = BatFiles.IndexOf(target);
        var viewModel = CreateExternalBatItem(externalItem, "Pending");
        SubscribeCheckStateEvents(viewModel);
        BatFiles.Insert(insertIndex < 0 ? BatFiles.Count : insertIndex, viewModel);
        RefreshParentOrders();
        SaveUpgradePlan();
        RefreshExternalCommandStates();
    }

    private static bool CanInsertExternalBatItem(BatExecutionPlanItemViewModel? target) =>
        target is not null;

    [RelayCommand(CanExecute = nameof(CanDeleteExternalBatItem))]
    private void DeleteExternalBatItem(BatExecutionPlanItemViewModel? target)
    {
        if (target is null ||
            !target.IsExternal ||
            !ConfirmRemoveExternalItem())
        {
            return;
        }

        BatFiles.Remove(target);
        RefreshParentOrders();
        SaveUpgradePlan();
        RefreshExternalCommandStates();
    }

    private static bool CanDeleteExternalBatItem(BatExecutionPlanItemViewModel? target) =>
        target?.IsExternal == true;

    [RelayCommand(CanExecute = nameof(CanMoveExternalBatItemUp))]
    private void MoveExternalBatItemUp(BatExecutionPlanItemViewModel? target) =>
        MoveExternalBatItem(target, -1);

    private bool CanMoveExternalBatItemUp(BatExecutionPlanItemViewModel? target) =>
        target?.IsExternal == true && BatFiles.IndexOf(target) > 0;

    [RelayCommand(CanExecute = nameof(CanMoveExternalBatItemDown))]
    private void MoveExternalBatItemDown(BatExecutionPlanItemViewModel? target) =>
        MoveExternalBatItem(target, 1);

    private bool CanMoveExternalBatItemDown(BatExecutionPlanItemViewModel? target)
    {
        if (target?.IsExternal != true)
        {
            return false;
        }

        var index = BatFiles.IndexOf(target);
        return index >= 0 && index < BatFiles.Count - 1;
    }

    [RelayCommand(CanExecute = nameof(CanInsertExternalUpdateItem))]
    private void InsertExternalUpdateItem(BatUpdateItemViewModel? target)
    {
        var parent = FindParentBat(target);
        if (target is null || parent is null)
        {
            return;
        }

        var externalItem = CreateExternalPlanItem(
            level: ExternalItemLevel.Update,
            targetBatFile: parent.FileName,
            insertBeforeKey: target.GetItemKey(parent.FileName));
        if (externalItem is null)
        {
            return;
        }

        var insertIndex = parent.Updates.IndexOf(target);
        var update = CreateExternalUpdateItem(externalItem, "Pending");
        parent.InsertUpdate(insertIndex < 0 ? parent.Updates.Count : insertIndex, update);
        parent.RefreshCheckStateFromChildren();
        SaveUpgradePlan();
        RefreshExternalCommandStates();
    }

    private bool CanInsertExternalUpdateItem(BatUpdateItemViewModel? target) =>
        FindParentBat(target) is not null;

    [RelayCommand(CanExecute = nameof(CanDeleteExternalUpdateItem))]
    private void DeleteExternalUpdateItem(BatUpdateItemViewModel? target)
    {
        var parent = FindParentBat(target);
        if (target is null ||
            !target.IsExternal ||
            parent is null ||
            !ConfirmRemoveExternalItem())
        {
            return;
        }

        parent.RemoveUpdate(target);
        SaveUpgradePlan();
        RefreshExternalCommandStates();
    }

    private bool CanDeleteExternalUpdateItem(BatUpdateItemViewModel? target) =>
        target?.IsExternal == true && FindParentBat(target) is not null;

    [RelayCommand(CanExecute = nameof(CanMoveExternalUpdateItemUp))]
    private void MoveExternalUpdateItemUp(BatUpdateItemViewModel? target) =>
        MoveExternalUpdateItem(target, -1);

    private bool CanMoveExternalUpdateItemUp(BatUpdateItemViewModel? target)
    {
        var parent = FindParentBat(target);
        return target?.IsExternal == true &&
            parent is not null &&
            parent.Updates.IndexOf(target) > 0;
    }

    [RelayCommand(CanExecute = nameof(CanMoveExternalUpdateItemDown))]
    private void MoveExternalUpdateItemDown(BatUpdateItemViewModel? target) =>
        MoveExternalUpdateItem(target, 1);

    private bool CanMoveExternalUpdateItemDown(BatUpdateItemViewModel? target)
    {
        var parent = FindParentBat(target);
        if (target?.IsExternal != true || parent is null)
        {
            return false;
        }

        var index = parent.Updates.IndexOf(target);
        return index >= 0 && index < parent.Updates.Count - 1;
    }

    [RelayCommand(CanExecute = nameof(CanGeneratePatchNote))]
    private async Task GeneratePatchNoteAsync(BatUpdateItemViewModel? target)
    {
        var parent = FindParentBat(target);
        if (target is null || parent is null)
        {
            return;
        }

        try
        {
            StatusMessage = "正在產生 Patch 說明...";
            var result = await _patchExplanationService.GenerateAsync(new PatchNoteGenerationRequest
            {
                SupportRoot = SupportRoot,
                CommandFolder = CommandFolder,
                PatchesBase = PatchesBase,
                BatFileName = parent.FileName,
                BatType = parent.Type,
                CatalogXmlPath = parent.CatalogPath,
                UpNumber = target.IsExternal ? string.Empty : target.UpNumber,
                Name = target.IsExternal
                    ? GetExternalDisplayName(target.DisplayName, target.Name)
                    : target.Name,
                Order = target.Order,
                Generation = target.Generation,
                SoftwareVersion = target.SoftwareVersion,
                DbTargetVersion = target.DbTargetVersion,
                IsExternal = target.IsExternal,
                ExternalStoredFullPath = target.StoredFullPath
            });

            target.MarkPatchNoteGenerated(
                result.MarkdownPath,
                result.AiStatus,
                result.AiSourceMode,
                result.AiErrorMessage,
                result.GeneratedAt == default
                    ? DateTimeOffset.Now
                    : result.GeneratedAt);

            StatusMessage = result.PatchXmlExists
                ? $"已產生 Patch 說明：{result.MarkdownPath}"
                : $"已產生 Patch 說明（Patch XML Missing）：{result.MarkdownPath}";
        }
        catch (Exception exception) when (IsPathException(exception) ||
                                          exception is System.Xml.XmlException or InvalidOperationException)
        {
            _logger.Error(
                exception,
                "Generate patch note failed {BatFileName} {UpdateName}",
                parent.FileName,
                target.Name);
            StatusMessage = $"產生 Patch 說明失敗：{exception.Message}";
            _messageDialogService?.ShowError(StatusMessage);
        }
    }

    private bool CanGeneratePatchNote(BatUpdateItemViewModel? target) =>
        FindParentBat(target) is not null;

    private void ScanBatFiles()
    {
        BatFiles.Clear();
        BatFileCount = 0;
        CommandsDirectoryFileCount = CountCommandsDirectoryFiles();
        LastScannedAt = DateTimeOffset.Now;

        if (_setup is null)
        {
            StatusMessage = "請先完成基本設定。";
            return;
        }

        if (string.IsNullOrWhiteSpace(CommandsDirectory) ||
            !_fileSystem.DirectoryExists(CommandsDirectory))
        {
            StatusMessage = $"commands 目錄不存在：{CommandsDirectory}";
            return;
        }

        _logger.Information(
            "Scan numeric-prefixed BAT files started {CommandsDirectory}",
            CommandsDirectory);

        try
        {
            var allBatFiles = _fileSystem
                .EnumerateFileSystemEntries(CommandsDirectory)
                .Where(_fileSystem.FileExists)
                .Where(path => string.Equals(
                    Path.GetExtension(path),
                    ".bat",
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (var skippedBat in allBatFiles.Where(path => !IsNumericPrefixedBat(path)))
            {
                _logger.Information(
                    "Skipped non-numeric BAT file {BatFileName}",
                    Path.GetFileName(skippedBat));
            }

            var batFiles = allBatFiles
                .Select(path => new
                {
                    Path = path,
                    Match = NumericBatFilePattern().Match(Path.GetFileName(path))
                })
                .Where(candidate => candidate.Match.Success)
                .OrderBy(candidate => int.Parse(
                    candidate.Match.Groups["order"].Value,
                    CultureInfo.InvariantCulture))
                .ThenBy(candidate => Path.GetFileName(candidate.Path), StringComparer.OrdinalIgnoreCase)
                .Select(candidate => candidate.Path)
                .ToArray();

            var order = 1;
            foreach (var batPath in batFiles)
            {
                var item = CreateBatItem(order++, batPath);
                ApplySavedCheckState(item);
                SubscribeCheckStateEvents(item);
                BatFiles.Add(item);
            }

            InsertSavedExternalItems();
            RefreshParentOrders();
            RefreshAllPatchNoteStates();
            BatFileCount = batFiles.Length;
            StatusMessage = batFiles.Length == 0
                ? "commands 目錄下沒有找到數字前綴的 .BAT 檔案。"
                : $"已掃描 {batFiles.Length} 個數字前綴 .BAT 檔案。";

            _logger.Information(
                "Upgrade options BAT scan completed {CommandsDirectory} {BatFileCount}",
                CommandsDirectory,
                batFiles.Length);
        }
        catch (Exception exception) when (IsPathException(exception))
        {
            _logger.Error(
                exception,
                "Upgrade options BAT scan failed {CommandsDirectory}",
                CommandsDirectory);
            StatusMessage = $"掃描 BAT 檔案失敗：{exception.Message}";
        }
    }

    private BatExecutionPlanItemViewModel CreateBatItem(int order, string batPath)
    {
        var fileName = Path.GetFileName(batPath);
        var descriptor = TryParsePatchBat(fileName);
        var catalogPath = string.Empty;
        var message = "已掃描 BAT 檔案。";
        var status = "Found";
        var updates = Array.Empty<BatUpdateItemViewModel>();

        if (descriptor is not null)
        {
            _logger.Information(
                "Pre/Post BAT detected {BatFileName} {Domain} {Stage}",
                fileName,
                descriptor.Domain,
                descriptor.Stage);

            catalogPath = ResolveCatalogPath(descriptor);
            if (string.IsNullOrWhiteSpace(catalogPath))
            {
                status = "Warning";
                message = "找不到 Catalog XML";
            }
            else
            {
                updates = ParseCatalogUpdates(catalogPath);
                status = "Catalog OK";
                message = updates.Length == 0
                    ? "Catalog XML 沒有 update 子項目。"
                    : $"已解析 {updates.Length} 個 update 子項目。";
            }
        }

        var item = new BatExecutionPlanItemViewModel
        {
            Order = order,
            FileName = fileName,
            Type = descriptor is null
                ? "BAT"
                : $"{descriptor.Domain} {descriptor.Stage}",
            Status = status,
            CatalogPath = catalogPath,
            Message = message,
            FullPath = Path.GetFullPath(batPath),
            IsExpanded = false
        };

        foreach (var update in updates)
        {
            item.AddUpdate(update);
        }

        _logger.Information(
            "Child update count {BatFileName} {ChildUpdateCount}",
            fileName,
            item.ChildItemCount);
        return item;
    }

    private PatchBatDescriptor? TryParsePatchBat(string batFileName)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(batFileName);
        var body = NumericPrefixPattern().Replace(fileNameWithoutExtension, string.Empty);
        var tokens = body
            .Split(['-', '_', ' ', '.'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var hasCore = tokens.Any(token => string.Equals(token, "CORE", StringComparison.OrdinalIgnoreCase));
        var hasPe = tokens.Any(token => string.Equals(token, "PE", StringComparison.OrdinalIgnoreCase));
        var hasPre = tokens.Any(token => string.Equals(token, "PRE", StringComparison.OrdinalIgnoreCase));
        var hasPost = tokens.Any(token => string.Equals(token, "POST", StringComparison.OrdinalIgnoreCase));

        var domain = hasCore
            ? "CORE"
            : hasPe
                ? "PE"
                : string.Empty;
        var stage = hasPre
            ? "PRE"
            : hasPost
                ? "POST"
                : string.Empty;

        return string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(stage)
            ? null
            : new PatchBatDescriptor(domain, stage);
    }

    private string ResolveCatalogPath(PatchBatDescriptor descriptor)
    {
        _logger.Information(
            "Catalog XML resolving started {PatchesBase} {Domain} {Stage}",
            PatchesBase,
            descriptor.Domain,
            descriptor.Stage);

        try
        {
            if (string.IsNullOrWhiteSpace(PatchesBase) ||
                !_fileSystem.DirectoryExists(PatchesBase))
            {
                _logger.Warning(
                    "Catalog XML missing {PatchesBase} {Domain} {Stage}",
                    PatchesBase,
                    descriptor.Domain,
                    descriptor.Stage);
                return string.Empty;
            }

            var domainDirectory = GetDirectChildDirectories(PatchesBase)
                .FirstOrDefault(path => IsDomainDirectory(path, descriptor.Domain));
            if (string.IsNullOrWhiteSpace(domainDirectory))
            {
                _logger.Warning(
                    "Catalog XML missing {PatchesBase} {Domain} {Stage}",
                    PatchesBase,
                    descriptor.Domain,
                    descriptor.Stage);
                return string.Empty;
            }

            var catalogPath = FindCatalogXml(domainDirectory, descriptor.Stage);
            if (string.IsNullOrWhiteSpace(catalogPath))
            {
                _logger.Warning(
                    "Catalog XML missing {DomainDirectory} {Stage}",
                    domainDirectory,
                    descriptor.Stage);
                return string.Empty;
            }

            _logger.Information(
                "Catalog XML resolved {CatalogPath}",
                catalogPath);
            return catalogPath;
        }
        catch (Exception exception) when (IsPathException(exception))
        {
            _logger.Error(
                exception,
                "Catalog XML missing {PatchesBase} {Domain} {Stage}",
                PatchesBase,
                descriptor.Domain,
                descriptor.Stage);
            return string.Empty;
        }
    }

    private string FindCatalogXml(string domainDirectory, string stage)
    {
        string[] preferredNames = stage.Equals("PRE", StringComparison.OrdinalIgnoreCase)
            ?
            [
                "pre-patches.xml",
                "pre_patches.xml",
                "pre-patches.manifest.xml",
                "pre_patches.manifest.xml"
            ]
            :
            [
                "post-patches.xml",
                "post_patches.xml",
                "post-patches.manifest.xml",
                "post_patches.manifest.xml"
            ];

        var xmlFiles = _fileSystem
            .EnumerateFileSystemEntries(domainDirectory)
            .Where(_fileSystem.FileExists)
            .Where(path => string.Equals(
                Path.GetExtension(path),
                ".xml",
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var preferredName in preferredNames)
        {
            var match = xmlFiles.FirstOrDefault(path => string.Equals(
                Path.GetFileName(path),
                preferredName,
                StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match))
            {
                return Path.GetFullPath(match);
            }
        }

        var stageToken = stage.Equals("PRE", StringComparison.OrdinalIgnoreCase)
            ? "pre"
            : "post";
        var fallback = xmlFiles.FirstOrDefault(path =>
        {
            var fileName = Path.GetFileNameWithoutExtension(path);
            var firstToken = fileName
                .Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            return string.Equals(firstToken, stageToken, StringComparison.OrdinalIgnoreCase);
        });

        return string.IsNullOrWhiteSpace(fallback)
            ? string.Empty
            : Path.GetFullPath(fallback);
    }

    private BatUpdateItemViewModel[] ParseCatalogUpdates(string catalogPath)
    {
        try
        {
            var document = XDocument.Load(catalogPath);
            var updates = document
                .Descendants()
                .Where(element => string.Equals(
                    element.Name.LocalName,
                    "update",
                    StringComparison.OrdinalIgnoreCase))
                .Select(element => new BatUpdateItemViewModel
                {
                    UpNumber = GetChildValue(element, "up_number"),
                    Name = GetChildValue(element, "name"),
                    Order = GetChildValue(element, "order"),
                    Generation = GetChildValue(element, "generation"),
                    SoftwareVersion = GetChildValue(element, "software_version"),
                    DbTargetVersion = GetChildValue(element, "db_target_version"),
                    Source = "Official"
                })
                .ToArray();

            _logger.Information(
                "Catalog XML parse completed {CatalogPath} {UpdateCount}",
                catalogPath,
                updates.Length);
            return updates;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                   or System.Xml.XmlException or InvalidOperationException)
        {
            _logger.Error(
                exception,
                "Catalog XML parse failed {CatalogPath}",
                catalogPath);
            return [];
        }
    }

    private void ApplySavedCheckState(BatExecutionPlanItemViewModel item)
    {
        var selectedBatFiles = new HashSet<string>(
            _upgradePlan.SelectedBatFiles,
            StringComparer.OrdinalIgnoreCase);
        var selectedUpdateKeys = _upgradePlan.SelectedUpdateKeys is null
            ? null
            : new HashSet<string>(
                _upgradePlan.SelectedUpdateKeys,
                StringComparer.OrdinalIgnoreCase);

        if (item.Updates.Count == 0)
        {
            item.IsChecked = selectedBatFiles.Contains(item.FileName);
            return;
        }

        if (selectedUpdateKeys is null)
        {
            var parentSelected = selectedBatFiles.Contains(item.FileName);
            foreach (var update in item.Updates)
            {
                update.IsChecked = parentSelected;
            }
        }
        else
        {
            foreach (var update in item.Updates)
            {
                update.IsChecked = selectedUpdateKeys.Contains(update.GetPlanKey(item.FileName));
            }
        }

        item.RefreshCheckStateFromChildren();
    }

    private void SubscribeCheckStateEvents(BatExecutionPlanItemViewModel item)
    {
        item.CheckStateChanged += OnParentCheckStateChanged;
        item.ChildCheckStateChanged += OnChildCheckStateChanged;
    }

    private void OnParentCheckStateChanged(BatExecutionPlanItemViewModel item)
    {
        _logger.Information(
            "Parent check state changed {BatFileName} {IsChecked}",
            item.FileName,
            item.IsChecked?.ToString() ?? "Partial");

        if (item.IsChecked is null)
        {
            _logger.Information(
                "Parent check state became partial {BatFileName}",
                item.FileName);
        }

        if (!_suppressPlanSave)
        {
            SaveUpgradePlan();
        }
    }

    private void OnChildCheckStateChanged(
        BatExecutionPlanItemViewModel item,
        BatUpdateItemViewModel update)
    {
        _logger.Information(
            "Child check state changed {BatFileName} {UpdateKey} {IsChecked}",
            item.FileName,
            update.GetItemKey(item.FileName),
            update.IsChecked);

        if (item.IsChecked is null)
        {
            _logger.Information(
                "Parent check state became partial {BatFileName}",
                item.FileName);
        }

        if (!_suppressPlanSave)
        {
            SaveUpgradePlan();
        }
    }

    private ExternalPlanItem? CreateExternalPlanItem(
        string level,
        string? targetBatFile,
        string insertBeforeKey)
    {
        var selectedPath = SelectExternalFilePath();
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return null;
        }

        try
        {
            var externalFilesDirectory = GetExternalFilesDirectory();
            if (string.IsNullOrWhiteSpace(externalFilesDirectory))
            {
                _messageDialogService?.ShowError("無法推導外部修正檔案目錄，請先完成基本設定。");
                return null;
            }

            var sourceFullPath = Path.GetFullPath(selectedPath);
            var originalFileName = Path.GetFileName(sourceFullPath);
            var timeStamp = DateTime.Now.ToString(
                "yyyyMMddHHmmssfff",
                CultureInfo.InvariantCulture);
            var storedFileName = GetUniqueStoredFileName(timeStamp, originalFileName);
            Directory.CreateDirectory(externalFilesDirectory);

            var storedFullPath = Path.Combine(externalFilesDirectory, storedFileName);
            File.Copy(sourceFullPath, storedFullPath, overwrite: false);

            var externalItem = new ExternalPlanItem
            {
                Id = $"ext-{timeStamp}",
                Level = level,
                TargetBatFile = targetBatFile,
                InsertBeforeKey = insertBeforeKey,
                DisplayName = originalFileName,
                OriginalFilePath = sourceFullPath,
                StoredRelativePath = Path.Combine("external-files", storedFileName),
                StoredFullPath = storedFullPath,
                FileName = originalFileName,
                FileExtension = Path.GetExtension(originalFileName),
                Enabled = true,
                SortIndex = GetNextExternalSortIndex()
            };

            _logger.Information(
                "External fix file inserted {Level} {TargetBatFile} {InsertBeforeKey} {StoredRelativePath}",
                level,
                targetBatFile,
                insertBeforeKey,
                externalItem.StoredRelativePath);
            return externalItem;
        }
        catch (Exception exception) when (IsPathException(exception))
        {
            _logger.Error(
                exception,
                "External fix file copy failed {Level} {TargetBatFile}",
                level,
                targetBatFile);
            _messageDialogService?.ShowError($"複製外部修正檔案失敗：{exception.Message}");
            return null;
        }
    }

    private string? SelectExternalFilePath()
    {
        var dialog = new OpenFileDialog
        {
            Title = "選擇外部修正檔案",
            CheckFileExists = true,
            Multiselect = false,
            Filter = "All files (*.*)|*.*"
        };

        return dialog.ShowDialog() == true
            ? dialog.FileName
            : null;
    }

    private string GetUniqueStoredFileName(string timeStamp, string originalFileName)
    {
        var directory = GetExternalFilesDirectory();
        var candidate = $"{timeStamp}-{originalFileName}";
        var index = 1;
        while (File.Exists(Path.Combine(directory, candidate)))
        {
            candidate = $"{timeStamp}-{index:000}-{originalFileName}";
            index++;
        }

        return candidate;
    }

    private int GetNextExternalSortIndex()
    {
        var currentItems = BuildExternalPlanItemsFromUi();
        var maxExisting = _upgradePlan.ExternalItems
            .Concat(currentItems)
            .Select(item => item.SortIndex)
            .DefaultIfEmpty(0)
            .Max();
        return maxExisting + 100;
    }

    private BatExecutionPlanItemViewModel CreateExternalBatItem(
        ExternalPlanItem externalItem,
        string status)
    {
        var displayName = GetExternalDisplayName(externalItem);
        return new BatExecutionPlanItemViewModel
        {
            IsExternal = true,
            ExternalId = externalItem.Id,
            DisplayName = displayName,
            OriginalFilePath = externalItem.OriginalFilePath,
            StoredRelativePath = externalItem.StoredRelativePath,
            StoredFullPath = ResolveStoredFullPath(externalItem),
            FileExtension = externalItem.FileExtension,
            FileName = $"🛠 {displayName}",
            Type = "外部修正",
            Status = status,
            CatalogPath = string.Empty,
            Message = status.Equals("Warning", StringComparison.OrdinalIgnoreCase)
                ? "找不到插入位置，已附加於最後"
                : "外部插入檔案",
            FullPath = ResolveStoredFullPath(externalItem),
            IsChecked = externalItem.Enabled,
            IsExpanded = false
        };
    }

    private BatUpdateItemViewModel CreateExternalUpdateItem(
        ExternalPlanItem externalItem,
        string status)
    {
        var displayName = GetExternalDisplayName(externalItem);
        return new BatUpdateItemViewModel
        {
            IsExternal = true,
            ExternalId = externalItem.Id,
            DisplayName = displayName,
            OriginalFilePath = externalItem.OriginalFilePath,
            StoredRelativePath = externalItem.StoredRelativePath,
            StoredFullPath = ResolveStoredFullPath(externalItem),
            FileName = externalItem.FileName,
            FileExtension = externalItem.FileExtension,
            UpNumber = "Custom",
            Name = $"🛠 {displayName}",
            Source = "External",
            IsChecked = externalItem.Enabled,
            Status = status
        };
    }

    private void InsertSavedExternalItems()
    {
        if (_upgradePlan.ExternalItems.Count == 0)
        {
            return;
        }

        _suppressPlanSave = true;
        try
        {
            foreach (var externalItem in _upgradePlan.ExternalItems
                         .Where(item => IsExternalLevel(item, ExternalItemLevel.Bat))
                         .OrderBy(item => item.SortIndex)
                         .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase))
            {
                var insertIndex = FindBatInsertIndex(externalItem.InsertBeforeKey);
                var status = insertIndex < 0 &&
                    !string.IsNullOrWhiteSpace(externalItem.InsertBeforeKey)
                        ? "Warning"
                        : "Pending";
                var viewModel = CreateExternalBatItem(externalItem, status);
                SubscribeCheckStateEvents(viewModel);

                if (insertIndex < 0)
                {
                    BatFiles.Add(viewModel);
                }
                else
                {
                    BatFiles.Insert(insertIndex, viewModel);
                }
            }

            foreach (var externalItem in _upgradePlan.ExternalItems
                         .Where(item => IsExternalLevel(item, ExternalItemLevel.Update))
                         .OrderBy(item => item.SortIndex)
                         .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase))
            {
                var parent = BatFiles.FirstOrDefault(item =>
                    !item.IsExternal &&
                    string.Equals(
                        item.FileName,
                        externalItem.TargetBatFile,
                        StringComparison.OrdinalIgnoreCase));
                if (parent is null)
                {
                    _logger.Warning(
                        "External update target BAT missing {TargetBatFile} {ExternalItemId}",
                        externalItem.TargetBatFile,
                        externalItem.Id);
                    var fallbackViewModel = CreateExternalBatItem(externalItem, "Warning");
                    SubscribeCheckStateEvents(fallbackViewModel);
                    BatFiles.Add(fallbackViewModel);
                    continue;
                }

                var insertIndex = FindUpdateInsertIndex(parent, externalItem.InsertBeforeKey);
                var status = insertIndex < 0 &&
                    !string.IsNullOrWhiteSpace(externalItem.InsertBeforeKey)
                        ? "Warning"
                        : "Pending";
                parent.InsertUpdate(
                    insertIndex < 0 ? parent.Updates.Count : insertIndex,
                    CreateExternalUpdateItem(externalItem, status));
                parent.RefreshCheckStateFromChildren();
            }
        }
        finally
        {
            _suppressPlanSave = false;
        }
    }

    private int FindBatInsertIndex(string insertBeforeKey)
    {
        if (string.IsNullOrWhiteSpace(insertBeforeKey))
        {
            return -1;
        }

        for (var index = 0; index < BatFiles.Count; index++)
        {
            if (string.Equals(
                    BatFiles[index].GetPlanKey(),
                    insertBeforeKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindUpdateInsertIndex(
        BatExecutionPlanItemViewModel parent,
        string insertBeforeKey)
    {
        if (string.IsNullOrWhiteSpace(insertBeforeKey))
        {
            return -1;
        }

        for (var index = 0; index < parent.Updates.Count; index++)
        {
            if (string.Equals(
                    parent.Updates[index].GetItemKey(parent.FileName),
                    insertBeforeKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private BatUpdateItemViewModel? FindUpdateByExternalId(string externalId)
    {
        return BatFiles
            .SelectMany(item => item.Updates)
            .FirstOrDefault(update => string.Equals(
                update.ExternalId,
                externalId,
                StringComparison.OrdinalIgnoreCase));
    }

    private BatExecutionPlanItemViewModel? FindParentBat(BatUpdateItemViewModel? target)
    {
        return target is null
            ? null
            : BatFiles.FirstOrDefault(item => item.Updates.Contains(target));
    }

    private void MoveExternalBatItem(BatExecutionPlanItemViewModel? target, int direction)
    {
        if (target?.IsExternal != true)
        {
            return;
        }

        var oldIndex = BatFiles.IndexOf(target);
        var newIndex = oldIndex + direction;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= BatFiles.Count)
        {
            return;
        }

        BatFiles.Move(oldIndex, newIndex);
        RefreshParentOrders();
        SaveUpgradePlan();
        RefreshExternalCommandStates();
        _logger.Information(
            "External BAT fix item moved {ExternalItemId} {NewIndex}",
            target.ExternalId,
            newIndex);
    }

    private void MoveExternalUpdateItem(BatUpdateItemViewModel? target, int direction)
    {
        var parent = FindParentBat(target);
        if (target?.IsExternal != true || parent is null)
        {
            return;
        }

        var oldIndex = parent.Updates.IndexOf(target);
        var newIndex = oldIndex + direction;
        parent.MoveUpdate(oldIndex, newIndex);
        parent.RefreshCheckStateFromChildren();
        SaveUpgradePlan();
        RefreshExternalCommandStates();
        _logger.Information(
            "External update fix item moved {ExternalItemId} {TargetBatFile} {NewIndex}",
            target.ExternalId,
            parent.FileName,
            newIndex);
    }

    private bool ConfirmRemoveExternalItem()
    {
        return _messageDialogService?.Confirm(
            "移除外部修正",
            "是否移除此外部修正項目？") ?? true;
    }

    private void RefreshParentOrders()
    {
        for (var index = 0; index < BatFiles.Count; index++)
        {
            BatFiles[index].Order = index + 1;
        }
    }

    private void RefreshExternalCommandStates()
    {
        InsertExternalBatItemCommand.NotifyCanExecuteChanged();
        DeleteExternalBatItemCommand.NotifyCanExecuteChanged();
        MoveExternalBatItemUpCommand.NotifyCanExecuteChanged();
        MoveExternalBatItemDownCommand.NotifyCanExecuteChanged();
        InsertExternalUpdateItemCommand.NotifyCanExecuteChanged();
        DeleteExternalUpdateItemCommand.NotifyCanExecuteChanged();
        MoveExternalUpdateItemUpCommand.NotifyCanExecuteChanged();
        MoveExternalUpdateItemDownCommand.NotifyCanExecuteChanged();
        GeneratePatchNoteCommand.NotifyCanExecuteChanged();
    }

    private UpgradePlanState LoadUpgradePlan()
    {
        var path = GetUpgradePlanPath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new UpgradePlanState();
        }

        try
        {
            return JsonSerializer.Deserialize<UpgradePlanState>(
                    File.ReadAllText(path),
                    JsonOptions)
                ?? new UpgradePlanState();
        }
        catch (Exception exception) when (IsPathException(exception) || exception is JsonException)
        {
            _logger.Warning(
                exception,
                "Load upgrade plan failed {UpgradePlanPath}",
                path);
            return new UpgradePlanState();
        }
    }

    private void SaveUpgradePlan()
    {
        var path = GetUpgradePlanPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            _upgradePlan = BuildUpgradePlanStateFromUi();

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                path,
                JsonSerializer.Serialize(_upgradePlan, JsonOptions));
            _logger.Information(
                "Upgrade plan saved {UpgradePlanPath}",
                path);
        }
        catch (Exception exception) when (IsPathException(exception) || exception is JsonException)
        {
            _logger.Error(
                exception,
                "Upgrade plan save failed {UpgradePlanPath}",
                path);
        }
    }

    private string GetUpgradePlanPath()
    {
        var assistantRoot = GetUpgradeAssistantRoot();
        return string.IsNullOrWhiteSpace(assistantRoot)
            ? string.Empty
            : Path.Combine(assistantRoot, "upgrade-plan.json");
    }

    private string GetUpgradeAssistantRoot()
    {
        return string.IsNullOrWhiteSpace(SupportRoot) ||
            string.IsNullOrWhiteSpace(CommandFolder)
                ? string.Empty
                : Path.Combine(SupportRoot, "UpgradeAssistant", CommandFolder);
    }

    private string GetExternalFilesDirectory()
    {
        var assistantRoot = GetUpgradeAssistantRoot();
        return string.IsNullOrWhiteSpace(assistantRoot)
            ? string.Empty
            : Path.Combine(assistantRoot, "external-files");
    }

    private UpgradePlanState BuildUpgradePlanStateFromUi() =>
        new()
        {
            SelectedBatFiles = BatFiles
                .Where(item => !item.IsExternal && item.IsChecked == true)
                .Select(item => item.FileName)
                .ToList(),
            SelectedUpdateKeys = BatFiles
                .Where(item => !item.IsExternal)
                .SelectMany(item => item.Updates
                    .Where(update => !update.IsExternal && update.IsChecked)
                    .Select(update => update.GetPlanKey(item.FileName)))
                .ToList(),
            ExternalItems = BuildExternalPlanItemsFromUi()
        };

    private List<ExternalPlanItem> BuildExternalPlanItemsFromUi()
    {
        var items = new List<ExternalPlanItem>();

        for (var index = 0; index < BatFiles.Count; index++)
        {
            var item = BatFiles[index];
            if (item.IsExternal)
            {
                items.Add(new ExternalPlanItem
                {
                    Id = item.ExternalId,
                    Level = ExternalItemLevel.Bat,
                    TargetBatFile = null,
                    InsertBeforeKey = FindNextOfficialBatKey(index),
                    DisplayName = GetExternalDisplayName(item.DisplayName, item.FileName),
                    OriginalFilePath = item.OriginalFilePath,
                    StoredRelativePath = item.StoredRelativePath,
                    StoredFullPath = item.StoredFullPath,
                    FileName = GetExternalDisplayName(item.DisplayName, item.FileName),
                    FileExtension = item.FileExtension,
                    Enabled = item.IsChecked == true,
                    SortIndex = (index + 1) * 100
                });
            }

            for (var updateIndex = 0; updateIndex < item.Updates.Count; updateIndex++)
            {
                var update = item.Updates[updateIndex];
                if (!update.IsExternal)
                {
                    continue;
                }

                items.Add(new ExternalPlanItem
                {
                    Id = update.ExternalId,
                    Level = ExternalItemLevel.Update,
                    TargetBatFile = item.FileName,
                    InsertBeforeKey = FindNextOfficialUpdateKey(item, updateIndex),
                    DisplayName = GetExternalDisplayName(update.DisplayName, update.Name),
                    OriginalFilePath = update.OriginalFilePath,
                    StoredRelativePath = update.StoredRelativePath,
                    StoredFullPath = update.StoredFullPath,
                    FileName = GetExternalDisplayName(update.FileName, update.DisplayName),
                    FileExtension = update.FileExtension,
                    Enabled = update.IsChecked,
                    SortIndex = (updateIndex + 1) * 100
                });
            }
        }

        return items;
    }

    private string FindNextOfficialBatKey(int fromIndex)
    {
        for (var index = fromIndex + 1; index < BatFiles.Count; index++)
        {
            if (!BatFiles[index].IsExternal)
            {
                return BatFiles[index].GetPlanKey();
            }
        }

        return string.Empty;
    }

    private static string FindNextOfficialUpdateKey(
        BatExecutionPlanItemViewModel parent,
        int fromIndex)
    {
        for (var index = fromIndex + 1; index < parent.Updates.Count; index++)
        {
            if (!parent.Updates[index].IsExternal)
            {
                return parent.Updates[index].GetPlanKey(parent.FileName);
            }
        }

        return string.Empty;
    }

    private string ResolveStoredFullPath(ExternalPlanItem externalItem)
    {
        if (!string.IsNullOrWhiteSpace(externalItem.StoredRelativePath))
        {
            var assistantRoot = GetUpgradeAssistantRoot();
            if (!string.IsNullOrWhiteSpace(assistantRoot))
            {
                return Path.GetFullPath(Path.Combine(assistantRoot, externalItem.StoredRelativePath));
            }
        }

        return externalItem.StoredFullPath;
    }

    private static string GetExternalDisplayName(ExternalPlanItem externalItem) =>
        GetExternalDisplayName(externalItem.DisplayName, externalItem.FileName);

    private static string GetExternalDisplayName(string primary, string fallback)
    {
        var value = string.IsNullOrWhiteSpace(primary)
            ? fallback
            : primary;
        return value
            .Replace("🛠", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private void RefreshAllPatchNoteStates()
    {
        foreach (var item in BatFiles)
        {
            foreach (var update in item.Updates)
            {
                RefreshPatchNoteState(item, update);
            }
        }
    }

    private void RefreshPatchNoteState(
        BatExecutionPlanItemViewModel parent,
        BatUpdateItemViewModel update)
    {
        var markdownPath = GetExpectedPatchNoteMarkdownPath(parent, update);
        if (string.IsNullOrWhiteSpace(markdownPath) || !File.Exists(markdownPath))
        {
            update.MarkPatchNoteMissing();
            return;
        }

        var generatedAt = DateTimeOffset.Now;
        try
        {
            generatedAt = new DateTimeOffset(File.GetLastWriteTime(markdownPath));
        }
        catch (Exception exception) when (IsPathException(exception))
        {
            _logger.Warning(
                exception,
                "Read patch note generated time failed {MarkdownPath}",
                markdownPath);
        }

        var (aiStatus, sourceMode, fallbackReason) = ReadPatchNoteAiMetadata(markdownPath);
        update.MarkPatchNoteGenerated(
            markdownPath,
            aiStatus,
            sourceMode,
            fallbackReason,
            generatedAt);
    }

    private string GetExpectedPatchNoteMarkdownPath(
        BatExecutionPlanItemViewModel parent,
        BatUpdateItemViewModel update)
    {
        var assistantRoot = GetUpgradeAssistantRoot();
        if (string.IsNullOrWhiteSpace(assistantRoot))
        {
            return string.Empty;
        }

        var batFolder = SanitizePatchNoteFileName(
            Path.GetFileNameWithoutExtension(parent.FileName));
        var fileNameSource = IsMeaningfulPatchNoteUpNumber(update.UpNumber)
            ? update.UpNumber
            : CleanPatchNoteDisplayName(update.IsExternal
                ? GetExternalDisplayName(update.DisplayName, update.Name)
                : update.Name);
        var fileName = $"{SanitizePatchNoteFileName(fileNameSource)}.md";

        return Path.GetFullPath(Path.Combine(
            assistantRoot,
            "patch-notes",
            batFolder,
            fileName));
    }

    private (string AiStatus, string SourceMode, string FallbackReason) ReadPatchNoteAiMetadata(
        string markdownPath)
    {
        try
        {
            var markdown = File.ReadAllText(markdownPath);
            var aiStatus = DetectPatchNoteAiStatus(markdown);
            var sourceMode = DetectPatchNoteSourceMode(markdown);
            var fallbackReason = string.Equals(aiStatus, "Fallback", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(aiStatus, "Error", StringComparison.OrdinalIgnoreCase)
                    ? "請開啟 Markdown 查看 AI 產生狀態與訊息"
                    : string.Empty;

            return (aiStatus, sourceMode, fallbackReason);
        }
        catch (Exception exception) when (IsPathException(exception))
        {
            _logger.Warning(
                exception,
                "Read patch note metadata failed {MarkdownPath}",
                markdownPath);
            return ("Unknown", "Unknown", "讀取 Markdown 狀態失敗");
        }
    }

    private static string DetectPatchNoteAiStatus(string markdown)
    {
        if (Regex.IsMatch(markdown, @"\|\s*Error\s*\|", RegexOptions.IgnoreCase))
        {
            return "Error";
        }

        if (Regex.IsMatch(markdown, @"\|\s*Fallback\s*\|", RegexOptions.IgnoreCase))
        {
            return "Fallback";
        }

        return Regex.IsMatch(markdown, @"\|\s*Success\s*\|", RegexOptions.IgnoreCase)
            ? "Success"
            : "Unknown";
    }

    private static string DetectPatchNoteSourceMode(string markdown)
    {
        var match = Regex.Match(
            markdown,
            @"\b(DescriptionOnly|DescriptionAndBodySummary|FullBody)\b",
            RegexOptions.IgnoreCase);
        return match.Success
            ? match.Value
            : "Unknown";
    }

    private static bool IsMeaningfulPatchNoteUpNumber(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !string.Equals(value, "Custom", StringComparison.OrdinalIgnoreCase);

    private static string CleanPatchNoteDisplayName(string value) =>
        value
            .Replace("??", string.Empty, StringComparison.Ordinal)
            .Replace("📎", string.Empty, StringComparison.Ordinal)
            .Replace("🛠", string.Empty, StringComparison.Ordinal)
            .Trim();

    private static string SanitizePatchNoteFileName(string value)
    {
        var cleaned = Regex
            .Replace(CleanPatchNoteDisplayName(value), @"\s+", " ")
            .Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return "patch-note";
        }

        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
        {
            cleaned = cleaned.Replace(invalidCharacter, '_');
        }

        return cleaned;
    }

    private IReadOnlyDictionary<string, string> ReadSetupVariables()
    {
        var path = _fileSystem.FileExists(GeneratedSetupCmdPath)
            ? GeneratedSetupCmdPath
            : SetupCmdPath;

        if (string.IsNullOrWhiteSpace(path) || !_fileSystem.FileExists(path))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            return CmdGenerationService
                .ExtractSetVariables(File.ReadAllText(path));
        }
        catch (Exception exception) when (IsPathException(exception))
        {
            _logger.Warning(
                exception,
                "Read SETUP CMD variables for upgrade options failed {SetupCmdPath}",
                path);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private int CountCommandsDirectoryFiles()
    {
        if (string.IsNullOrWhiteSpace(CommandsDirectory) ||
            !Directory.Exists(CommandsDirectory))
        {
            return 0;
        }

        try
        {
            return Directory
                .GetFiles(
                    CommandsDirectory,
                    "*",
                    SearchOption.TopDirectoryOnly)
                .Length;
        }
        catch (Exception exception) when (IsPathException(exception))
        {
            _logger.Warning(
                exception,
                "Count commands directory files failed {CommandsDirectory}",
                CommandsDirectory);
            return 0;
        }
    }

    private string DetectPatchesBase(string supportRoot)
    {
        var patchesRoot = Path.Combine(supportRoot, "Patches");
        try
        {
            var candidates = EnumerateDirectoriesIncludingSelf(patchesRoot)
                .Select(path =>
                {
                    var children = GetDirectChildDirectories(path).ToArray();
                    return new
                    {
                        Path = path,
                        HasCore = children.Any(IsCoreDirectoryName),
                        HasPe = children.Any(IsPeDirectoryName)
                    };
                })
                .Where(candidate => candidate.HasCore || candidate.HasPe)
                .OrderByDescending(candidate => candidate.HasCore && candidate.HasPe)
                .ThenByDescending(candidate => candidate.HasCore)
                .ThenBy(candidate => GetPathDepth(patchesRoot, candidate.Path))
                .ThenBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            return candidates?.Path ?? Path.GetFullPath(patchesRoot);
        }
        catch (Exception exception) when (IsPathException(exception))
        {
            _logger.Warning(
                exception,
                "Detect patches base for upgrade options failed {PatchesRoot}",
                patchesRoot);
            return Path.GetFullPath(patchesRoot);
        }
    }

    private string DetectSolutionsBase(string supportRoot)
    {
        var solutionsRoot = Path.Combine(supportRoot, "Solutions");
        try
        {
            var candidates = EnumerateDirectoriesIncludingSelf(solutionsRoot)
                .Select(path =>
                {
                    var manifestFiles = _fileSystem
                        .EnumerateFileSystemEntries(path)
                        .Where(_fileSystem.FileExists)
                        .Where(entry => string.Equals(
                            Path.GetExtension(entry),
                            ".mf",
                            StringComparison.OrdinalIgnoreCase))
                        .ToArray();

                    return new
                    {
                        Path = path,
                        ManifestCount = manifestFiles.Length,
                        HasCoreImportsManifest = manifestFiles.Any(file => string.Equals(
                            Path.GetFileName(file),
                            "core_imports.mf",
                            StringComparison.OrdinalIgnoreCase))
                    };
                })
                .Where(candidate => candidate.ManifestCount > 0)
                .OrderByDescending(candidate => candidate.HasCoreImportsManifest)
                .ThenBy(candidate => GetPathDepth(solutionsRoot, candidate.Path))
                .ThenBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            return candidates?.Path ?? Path.GetFullPath(solutionsRoot);
        }
        catch (Exception exception) when (IsPathException(exception))
        {
            _logger.Warning(
                exception,
                "Detect solutions base for upgrade options failed {SolutionsRoot}",
                solutionsRoot);
            return Path.GetFullPath(solutionsRoot);
        }
    }

    private IEnumerable<string> EnumerateDirectoriesIncludingSelf(string root)
    {
        var fullRoot = Path.GetFullPath(root);
        if (!_fileSystem.DirectoryExists(fullRoot))
        {
            return [];
        }

        return EnumerateDirectoriesRecursive(fullRoot).Prepend(fullRoot);
    }

    private IEnumerable<string> EnumerateDirectoriesRecursive(string path)
    {
        foreach (var childDirectory in GetDirectChildDirectories(path))
        {
            yield return childDirectory;

            foreach (var nestedDirectory in EnumerateDirectoriesRecursive(childDirectory))
            {
                yield return nestedDirectory;
            }
        }
    }

    private IEnumerable<string> GetDirectChildDirectories(string path) =>
        _fileSystem
            .EnumerateFileSystemEntries(path)
            .Where(_fileSystem.DirectoryExists)
            .Select(Path.GetFullPath);

    private static bool IsNumericPrefixedBat(string path) =>
        NumericBatFilePattern().IsMatch(Path.GetFileName(path));

    private static bool IsDomainDirectory(string path, string domain)
    {
        var name = Path.GetFileName(path) ?? string.Empty;
        return domain.Equals("CORE", StringComparison.OrdinalIgnoreCase)
            ? name.Contains("core", StringComparison.OrdinalIgnoreCase)
            : name.Equals("PE", StringComparison.OrdinalIgnoreCase) ||
              name.Contains("PE", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCoreDirectoryName(string path) =>
        (Path.GetFileName(path) ?? string.Empty).Contains(
            "core",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsPeDirectoryName(string path) =>
        (Path.GetFileName(path) ?? string.Empty).Contains(
            "PE",
            StringComparison.OrdinalIgnoreCase);

    private static string GetChildValue(XElement element, string childName)
    {
        return element
            .Elements()
            .FirstOrDefault(child => string.Equals(
                child.Name.LocalName,
                childName,
                StringComparison.OrdinalIgnoreCase))
            ?.Value
            .Trim() ?? string.Empty;
    }

    private static int GetPathDepth(string root, string path)
    {
        var relativePath = Path.GetRelativePath(root, path);
        return string.Equals(relativePath, ".", StringComparison.Ordinal)
            ? 0
            : relativePath
                .Split(
                    [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                    StringSplitOptions.RemoveEmptyEntries)
                .Length;
    }

    private static bool IsPathException(Exception exception) =>
        exception is UnauthorizedAccessException or IOException or ArgumentException
            or NotSupportedException;

    [GeneratedRegex(@"^(?<order>\d+)-.+\.BAT$", RegexOptions.IgnoreCase)]
    private static partial Regex NumericBatFilePattern();

    [GeneratedRegex(@"^\d+-")]
    private static partial Regex NumericPrefixPattern();

    private sealed record PatchBatDescriptor(string Domain, string Stage);

    private sealed class UpgradePlanState
    {
        public List<string> SelectedBatFiles { get; set; } = [];

        public List<string>? SelectedUpdateKeys { get; set; }

        public List<ExternalPlanItem> ExternalItems { get; set; } = [];
    }

    private static class ExternalItemLevel
    {
        public const string Bat = "Bat";

        public const string Update = "Update";
    }

    private static bool IsExternalLevel(ExternalPlanItem item, string level) =>
        string.Equals(item.Level, level, StringComparison.OrdinalIgnoreCase);

    private sealed class ExternalPlanItem
    {
        public string Id { get; set; } = string.Empty;

        public string Level { get; set; } = string.Empty;

        public string? TargetBatFile { get; set; }

        public string InsertBeforeKey { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string OriginalFilePath { get; set; } = string.Empty;

        public string StoredRelativePath { get; set; } = string.Empty;

        public string StoredFullPath { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public string FileExtension { get; set; } = string.Empty;

        public bool Enabled { get; set; }

        public int SortIndex { get; set; }
    }
}
