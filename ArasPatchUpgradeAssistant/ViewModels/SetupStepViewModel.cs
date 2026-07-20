using System.Collections.ObjectModel;
using System.IO;
using ArasPatchUpgradeAssistant.Helpers;
using ArasPatchUpgradeAssistant.Models;
using ArasPatchUpgradeAssistant.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace ArasPatchUpgradeAssistant.ViewModels;

public partial class SetupStepViewModel : ObservableObject
{
    private readonly ISetupPathParser _setupPathParser;
    private readonly IInnovatorConfigService _innovatorConfigService;
    private readonly CmdVariableBuilder _variableBuilder;
    private readonly ICmdGenerationService _generationService;
    private readonly IFileSystem _fileSystem;
    private readonly IFileDialogService _fileDialogService;
    private readonly IMessageDialogService _messageDialogService;
    private readonly IUserSettingsService _userSettingsService;
    private readonly ISecretProtectionService _secretProtectionService;
    private readonly IIomDllLocatorService _iomDllLocatorService;
    private readonly ILogger _logger;
    private readonly Func<string> _machineNameProvider;
    private readonly string _logDirectory;
    private InnovatorConfiguration? _configuration;
    private UserSettings _userSettings = new();
    private bool _isRestoringSettings;
    private bool _suppressSettingsSave;
    private bool _hasCachedIomDllSearchResult;
    private string _cachedIomDllSupportRoot = string.Empty;
    private string? _cachedIomDllPath;

    public SetupStepViewModel(
        ISetupPathParser setupPathParser,
        IInnovatorConfigService innovatorConfigService,
        CmdVariableBuilder variableBuilder,
        ICmdGenerationService generationService,
        IFileSystem fileSystem,
        IFileDialogService fileDialogService,
        IMessageDialogService messageDialogService,
        IUserSettingsService userSettingsService,
        ISecretProtectionService secretProtectionService,
        ILogger logger,
        string logDirectory,
        Func<string>? machineNameProvider = null,
        IIomDllLocatorService? iomDllLocatorService = null)
    {
        _setupPathParser = setupPathParser;
        _innovatorConfigService = innovatorConfigService;
        _variableBuilder = variableBuilder;
        _generationService = generationService;
        _fileSystem = fileSystem;
        _fileDialogService = fileDialogService;
        _messageDialogService = messageDialogService;
        _userSettingsService = userSettingsService;
        _secretProtectionService = secretProtectionService;
        _iomDllLocatorService = iomDllLocatorService ?? new IomDllLocatorService(logger);
        _logger = logger;
        _logDirectory = logDirectory;
        _machineNameProvider = machineNameProvider ?? (() => Environment.MachineName);

        RestoreSettings();
    }

    public event EventHandler<SetupCompletedEventArgs>? SetupCompleted;

    public event EventHandler? SetupInvalidated;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReferencePath))]
    private string _setupCmdPath = string.Empty;

    [ObservableProperty]
    private string _innovatorConfigPath = string.Empty;

    [ObservableProperty]
    private DatabaseConnectionOption? _selectedConnection;

    [ObservableProperty]
    private string _loginName = "root";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaskedPassword))]
    private string _password = "innovator";

    [ObservableProperty]
    private string _sqlLoginName = "sa";

    [ObservableProperty]
    private string _sqlPassword = string.Empty;

    [ObservableProperty]
    private string _copySourceDbName = string.Empty;

    [ObservableProperty]
    private bool _isSetupPathSectionExpanded = true;

    [ObservableProperty]
    private bool _isInnovatorSectionExpanded = true;

    [ObservableProperty]
    private bool _isSqlLoginSectionExpanded = true;

    [ObservableProperty]
    private bool _isPreviewSectionExpanded = true;

    [ObservableProperty]
    private bool _isResultSectionExpanded = true;

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private string _previewWarningMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GeneratedCmdPath))]
    private string _generatedTargetPath = string.Empty;

    public UpgradePathInfo? UpgradePaths { get; private set; }

    public string CommandFolder => UpgradePaths?.CommandFolder ?? string.Empty;

    public string SupportRoot => UpgradePaths?.SupportRoot ?? string.Empty;

    public string UpgradeRoot => UpgradePaths?.UpgradeRoot ?? string.Empty;

    public string Version => UpgradePaths?.Version ?? string.Empty;

    public string VersionCode => UpgradePaths?.VersionCode ?? string.Empty;

    public string ReferencePath => SetupCmdPath;

    public string ApServerRoot => _configuration?.ApServerRoot ?? string.Empty;

    public string VaultConfigPath => _configuration?.VaultConfigPath ?? string.Empty;

    public string ServerPrefix => _configuration?.ServerPrefix ?? string.Empty;

    public string WebUrl => ServerPrefix;

    public string SelectedSqlServer =>
        string.IsNullOrWhiteSpace(SelectedConnection?.Server)
            ? "Unknown"
            : SelectedConnection.Server;

    public string SelectedDatabaseName => SelectedConnection?.Database ?? string.Empty;

    public string SettingsFilePath => _userSettingsService.SettingsFilePath;

    public string LogDirectory => _logDirectory;

    public string GeneratedCmdPath => GeneratedTargetPath;

    public string MaskedPassword => PasswordMask.Create(Password);

    public ObservableCollection<DatabaseConnectionOption> DatabaseConnections { get; } = [];

    public ObservableCollection<CmdVariablePreview> VariablePreview { get; } = [];

    public ObservableCollection<CmdVariableChangeDisplay> GenerationChanges { get; } = [];

    [RelayCommand]
    private void BrowseSetup()
    {
        var path = _fileDialogService.SelectSetupCommand();
        if (!string.IsNullOrWhiteSpace(path))
        {
            _logger.Information("User selected SETUP CMD path {SetupCmdPath}", path);
            LoadSetupCmdTemplate(path, saveSettings: true);
        }
    }

    [RelayCommand]
    private void BrowseInnovatorConfig()
    {
        var path = _fileDialogService.SelectInnovatorConfig();
        if (!string.IsNullOrWhiteSpace(path))
        {
            _logger.Information("User selected InnovatorServerConfig.xml path {ConfigPath}", path);
            LoadInnovatorConfig(path, saveSettings: true);
        }
    }

    [RelayCommand]
    private void ExpandAllSections()
    {
        SetAllSectionsExpanded(true);
        _logger.Information("Expand all sections");
    }

    [RelayCommand]
    private void CollapseAllSections()
    {
        SetAllSectionsExpanded(false);
        _logger.Information("Collapse all sections");
    }

    public void LoadSetupPath(string path) =>
        LoadSetupCmdTemplate(path, saveSettings: false);

    public bool LoadSetupCmdTemplate(string path, bool saveSettings)
    {
        ClearSetupSelection();

        try
        {
            var parsed = _setupPathParser.Parse(path);
            UpgradePaths = parsed;
            ClearIomDllSearchCache();
            SetupCmdPath = parsed.SetupCmdPath;
            NotifyPathProperties();
            UpdatePreview();

            _logger.Information(
                "Derived upgrade path information {SetupCmdPath} {Version} {CommandFolder} {UpgradeRoot} {SupportRoot}",
                parsed.SetupCmdPath,
                parsed.Version,
                parsed.CommandFolder,
                parsed.UpgradeRoot,
                parsed.SupportRoot);
            _logger.Information(
                "Version normalized {UpgradeRoot} {Version}",
                parsed.UpgradeRoot,
                parsed.Version);

            if (saveSettings)
            {
                _userSettings.SetupDefaultsTemplatePath = parsed.SetupCmdPath;
                SaveUserSettings();
            }

            return true;
        }
        catch (Exception exception)
        {
            _logger.Error(
                exception,
                "Load SETUP CMD path failed {SetupCmdPath}",
                path);
            _messageDialogService.ShowError(exception.Message);
            return false;
        }
    }

    public void LoadInnovatorConfig(string path) =>
        LoadInnovatorConfig(path, saveSettings: false);

    public bool LoadInnovatorConfig(string path, bool saveSettings)
    {
        ClearInnovatorConfigSelection();

        try
        {
            _configuration = _innovatorConfigService.Load(path);
            InnovatorConfigPath = _configuration.ConfigPath;
            DatabaseConnections.Clear();
            foreach (var connection in _configuration.Connections)
            {
                DatabaseConnections.Add(connection);
            }

            _suppressSettingsSave = true;
            SelectedConnection = ResolvePreferredConnection();
            _suppressSettingsSave = false;

            NotifyInnovatorProperties();
            UpdatePreview();

            if (saveSettings)
            {
                _userSettings.InnovatorServerConfigPath = _configuration.ConfigPath;
                PersistSelectedDatabase();
                SaveUserSettings();
            }

            return true;
        }
        catch (Exception exception)
        {
            _suppressSettingsSave = false;
            _logger.Error(
                exception,
                "Load InnovatorServerConfig.xml failed {ConfigPath}",
                path);
            _messageDialogService.ShowError(exception.Message);
            return false;
        }
    }

    [RelayCommand]
    private void Generate()
    {
        if (!TryValidate(out var error))
        {
            _messageDialogService.ShowError(error);
            return;
        }

        try
        {
            var machineName = _machineNameProvider();
            _logger.Information(
                "Generate SETUP-DEFAULTS-{MachineName}.CMD started {SetupCmdPath}",
                machineName,
                UpgradePaths!.SetupCmdPath);

            var targetPath = Path.Combine(
                Path.GetDirectoryName(UpgradePaths.SetupCmdPath)!,
                $"SETUP-DEFAULTS-{machineName}.CMD");
            if (_fileSystem.FileExists(targetPath) &&
                !_messageDialogService.Confirm(
                    "覆蓋 SETUP CMD",
                    $"目標檔案已存在，是否覆蓋？{Environment.NewLine}{targetPath}"))
            {
                return;
            }

            var values = BuildValues();
            var result = _generationService.Generate(
                UpgradePaths.SetupCmdPath,
                machineName,
                values);
            GeneratedTargetPath = result.TargetPath;
            GenerationChanges.Clear();
            foreach (var change in result.Changes)
            {
                GenerationChanges.Add(ToDisplayChange(change));
            }

            IsCompleted = true;
            _logger.Information(
                "Generate SETUP-DEFAULTS-{MachineName}.CMD completed {GeneratedCmdPath}",
                machineName,
                result.TargetPath);
            SetupCompleted?.Invoke(
                this,
                new SetupCompletedEventArgs(UpgradePaths, result.TargetPath));
        }
        catch (Exception exception)
        {
            IsCompleted = false;
            _logger.Error(
                exception,
                "Generate SETUP-DEFAULTS-{MachineName}.CMD failed",
                _machineNameProvider());
            _messageDialogService.ShowError(exception.Message);
        }
    }

    partial void OnSelectedConnectionChanged(DatabaseConnectionOption? value)
    {
        InvalidateCompletion();
        NotifySelectedConnectionProperties();
        if (value is not null)
        {
            _logger.Information(
                "COPY_TARGET_DB_NAME updated from selected database {Database}",
                value.Database);
            AutoFillCopySourceDbName(value.Database);
        }

        UpdatePreview();

        if (_isRestoringSettings || _suppressSettingsSave)
        {
            return;
        }

        PersistSelectedDatabase();
        SaveUserSettings();
    }

    partial void OnLoginNameChanged(string value)
    {
        InvalidateCompletion();
        UpdatePreview();

        if (_isRestoringSettings || _suppressSettingsSave)
        {
            return;
        }

        _userSettings.LoginName = value;
        SaveUserSettings();
    }

    partial void OnPasswordChanged(string value)
    {
        InvalidateCompletion();
        UpdatePreview();

        if (_isRestoringSettings || _suppressSettingsSave)
        {
            return;
        }

        _userSettings.EncryptedPassword = ProtectSecretOrShowError(
            value,
            "Innovator");
        SaveUserSettings();
    }

    partial void OnSqlLoginNameChanged(string value)
    {
        InvalidateCompletion();
        UpdatePreview();

        if (_isRestoringSettings || _suppressSettingsSave)
        {
            return;
        }

        _userSettings.SqlLoginName = string.IsNullOrWhiteSpace(value)
            ? "sa"
            : value;
        SaveUserSettings();
    }

    partial void OnSqlPasswordChanged(string value)
    {
        InvalidateCompletion();
        UpdatePreview();

        if (_isRestoringSettings || _suppressSettingsSave)
        {
            return;
        }

        _userSettings.EncryptedSqlPassword = ProtectSecretOrShowError(
            value,
            "SQL");
        SaveUserSettings();
    }

    partial void OnCopySourceDbNameChanged(string value)
    {
        InvalidateCompletion();
        UpdatePreview();

        if (_isRestoringSettings || _suppressSettingsSave)
        {
            return;
        }

        _logger.Information(
            "COPY_SOURCE_DB_NAME changed {HasValue}",
            !string.IsNullOrWhiteSpace(value));
        _userSettings.CopySourceDbName = value;
        SaveUserSettings();
    }

    private void RestoreSettings()
    {
        try
        {
            _userSettings = _userSettingsService.Load();
            _isRestoringSettings = true;
            LoginName = string.IsNullOrWhiteSpace(_userSettings.LoginName)
                ? "root"
                : _userSettings.LoginName;
            Password = UnprotectSecretOrDefault(
                _userSettings.EncryptedPassword,
                "innovator",
                "Innovator");
            SqlLoginName = string.IsNullOrWhiteSpace(_userSettings.SqlLoginName)
                ? "sa"
                : _userSettings.SqlLoginName;
            SqlPassword = UnprotectSecretOrDefault(
                _userSettings.EncryptedSqlPassword,
                string.Empty,
                "SQL");
            CopySourceDbName = _userSettings.CopySourceDbName;

            AutoLoadSetupCmd();
            AutoLoadInnovatorConfig();
        }
        catch (Exception exception)
        {
            _logger.Error(
                exception,
                "Restore user settings failed {SettingsFilePath}",
                SettingsFilePath);
            _messageDialogService.ShowError($"載入使用者設定失敗：{exception.Message}");
            _userSettings = new UserSettings();
        }
        finally
        {
            _isRestoringSettings = false;
        }
    }

    private void AutoLoadSetupCmd()
    {
        var savedPath = _userSettings.SetupDefaultsTemplatePath;
        if (string.IsNullOrWhiteSpace(savedPath))
        {
            return;
        }

        try
        {
            if (!_fileSystem.FileExists(savedPath))
            {
                _messageDialogService.ShowError(
                    $"上次選取的 SETUP CMD 不存在，畫面已留白：{savedPath}");
                return;
            }

            _logger.Information("Auto load setup cmd started {SetupCmdPath}", savedPath);
            if (LoadSetupCmdTemplate(savedPath, saveSettings: false))
            {
                _logger.Information("Auto load setup cmd completed {SetupCmdPath}", savedPath);
            }
            else
            {
                _logger.Error("Auto load setup cmd failed {SetupCmdPath}", savedPath);
            }
        }
        catch (Exception exception)
        {
            _logger.Error(
                exception,
                "Auto load setup cmd failed {SetupCmdPath}",
                savedPath);
            _messageDialogService.ShowError(
                $"自動載入 SETUP CMD 失敗：{savedPath}{Environment.NewLine}{exception.Message}");
        }
    }

    private void AutoLoadInnovatorConfig()
    {
        var savedPath = _userSettings.InnovatorServerConfigPath;
        if (string.IsNullOrWhiteSpace(savedPath))
        {
            return;
        }

        try
        {
            if (!_fileSystem.FileExists(savedPath))
            {
                _messageDialogService.ShowError(
                    $"上次選取的 InnovatorServerConfig.xml 不存在，畫面已留白：{savedPath}");
                return;
            }

            _logger.Information("Auto load innovator config started {ConfigPath}", savedPath);
            if (LoadInnovatorConfig(savedPath, saveSettings: false))
            {
                _logger.Information("Auto load innovator config completed {ConfigPath}", savedPath);
            }
            else
            {
                _logger.Error("Auto load innovator config failed {ConfigPath}", savedPath);
            }
        }
        catch (Exception exception)
        {
            _logger.Error(
                exception,
                "Auto load innovator config failed {ConfigPath}",
                savedPath);
            _messageDialogService.ShowError(
                $"自動載入 InnovatorServerConfig.xml 失敗：{savedPath}{Environment.NewLine}{exception.Message}");
        }
    }

    private DatabaseConnectionOption? ResolvePreferredConnection()
    {
        if (!string.IsNullOrWhiteSpace(_userSettings.SelectedDatabaseName))
        {
            var byDatabase = DatabaseConnections.FirstOrDefault(connection =>
                string.Equals(
                    connection.Database,
                    _userSettings.SelectedDatabaseName,
                    StringComparison.OrdinalIgnoreCase));
            if (byDatabase is not null)
            {
                return byDatabase;
            }
        }

        if (!string.IsNullOrWhiteSpace(_userSettings.SelectedDatabaseId))
        {
            var byLabel = DatabaseConnections.FirstOrDefault(connection =>
                string.Equals(
                    connection.Label,
                    _userSettings.SelectedDatabaseId,
                    StringComparison.OrdinalIgnoreCase));
            if (byLabel is not null)
            {
                return byLabel;
            }
        }

        return DatabaseConnections.FirstOrDefault();
    }

    private void PersistSelectedDatabase()
    {
        _userSettings.SelectedDatabaseId = SelectedConnection?.Label ?? string.Empty;
        _userSettings.SelectedDatabaseName = SelectedConnection?.Database ?? string.Empty;
        _userSettings.CopySourceDbName = CopySourceDbName;
    }

    private void AutoFillCopySourceDbName(string databaseName)
    {
        if (!string.IsNullOrWhiteSpace(CopySourceDbName) ||
            string.IsNullOrWhiteSpace(databaseName))
        {
            return;
        }

        CopySourceDbName = databaseName;
        _logger.Information(
            "COPY_SOURCE_DB_NAME auto-filled from selected database");
    }

    private void SetAllSectionsExpanded(bool isExpanded)
    {
        IsSetupPathSectionExpanded = isExpanded;
        IsInnovatorSectionExpanded = isExpanded;
        IsSqlLoginSectionExpanded = isExpanded;
        IsPreviewSectionExpanded = isExpanded;
        IsResultSectionExpanded = isExpanded;
    }

    private string ProtectSecretOrShowError(string plainText, string secretName)
    {
        try
        {
            var protectedText = _secretProtectionService.Protect(plainText);
            _logger.Information(
                "Password encryption completed {SecretName} {HasValue}",
                secretName,
                !string.IsNullOrEmpty(plainText));
            return protectedText;
        }
        catch (Exception exception)
        {
            _logger.Error(
                exception,
                "Password encryption failed {SecretName}",
                secretName);
            _messageDialogService.ShowError(
                $"{secretName} 密碼加密失敗，請重新輸入。");
            return string.Empty;
        }
    }

    private string UnprotectSecretOrDefault(
        string protectedText,
        string defaultValue,
        string secretName)
    {
        if (string.IsNullOrWhiteSpace(protectedText))
        {
            _logger.Information(
                "Password decryption skipped {SecretName} {HasSavedValue}",
                secretName,
                false);
            return defaultValue;
        }

        try
        {
            var plainText = _secretProtectionService.Unprotect(protectedText);
            _logger.Information(
                "Password decryption completed {SecretName}",
                secretName);
            return plainText;
        }
        catch (Exception exception)
        {
            _logger.Error(
                exception,
                "Password decryption failed {SecretName}",
                secretName);
            _messageDialogService.ShowError(
                $"已儲存的 {secretName} 密碼無法解密，請重新輸入。");
            return defaultValue;
        }
    }

    private void SaveUserSettings()
    {
        if (_isRestoringSettings || _suppressSettingsSave)
        {
            return;
        }

        _userSettingsService.Save(_userSettings);
    }

    private void ClearSetupSelection()
    {
        UpgradePaths = null;
        SetupCmdPath = string.Empty;
        InvalidateCompletion();
        NotifyPathProperties();
        UpdatePreview();
    }

    private void ClearInnovatorConfigSelection()
    {
        _configuration = null;
        InnovatorConfigPath = string.Empty;
        DatabaseConnections.Clear();
        _suppressSettingsSave = true;
        SelectedConnection = null;
        _suppressSettingsSave = false;
        InvalidateCompletion();
        NotifyInnovatorProperties();
        UpdatePreview();
    }

    private bool TryValidate(out string error)
    {
        if (UpgradePaths is null)
        {
            error = "請先選擇 SETUP CMD。";
            return false;
        }

        if (_configuration is null)
        {
            error = "請先選擇 InnovatorServerConfig.xml。";
            return false;
        }

        if (SelectedConnection is null)
        {
            error = "請先選擇資料庫。";
            return false;
        }

        if (SelectedConnection is null)
        {
            error = "請選擇資料庫。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(CopySourceDbName))
        {
            error = "請輸入來源資料庫（COPY_SOURCE_DB_NAME）。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(SelectedConnection.Server))
        {
            error = "DB-Connection 未定義 server，請確認 InnovatorServerConfig.xml。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(LoginName))
        {
            error = "使用者名稱不可空白。";
            return false;
        }

        if (string.IsNullOrEmpty(Password))
        {
            error = "密碼不可空白。";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private IReadOnlyDictionary<string, string> BuildValues() =>
        BuildValues(LoadExistingCmdVariables(), out _);

    private IReadOnlyDictionary<string, string> BuildValues(
        IReadOnlyDictionary<string, string> existingCmdVariables,
        out TargetIomDllResolution targetIomDll)
    {
        var values = new Dictionary<string, string>(
            _variableBuilder.Build(
                UpgradePaths!,
                SelectedConnection!,
                _configuration!.ConfigPath,
                _configuration.ServerPrefix,
                LoginName,
                Password,
                SqlLoginName,
                SqlPassword,
                CopySourceDbName,
                existingCmdVariables),
            StringComparer.OrdinalIgnoreCase);

        targetIomDll = ResolveTargetIomDll(existingCmdVariables);
        if (targetIomDll.Exists &&
            !string.IsNullOrWhiteSpace(targetIomDll.SelectedPath))
        {
            values["TARGET_IOM_DLL"] = targetIomDll.SelectedPath;
        }

        return values;
    }

    private IReadOnlyDictionary<string, string> BuildValues(
        IReadOnlyDictionary<string, string> existingCmdVariables) =>
        BuildValues(existingCmdVariables, out _);

    private TargetIomDllResolution ResolveTargetIomDll(
        IReadOnlyDictionary<string, string> existingCmdVariables)
    {
        if (!existingCmdVariables.ContainsKey("TARGET_IOM_DLL"))
        {
            return TargetIomDllResolution.Skipped;
        }

        var foundPath = FindIomDllPathForCurrentSupportRoot();
        return string.IsNullOrWhiteSpace(foundPath)
            ? TargetIomDllResolution.Warning(
                "找不到 IOM.dll，請確認 SolutionUpgrade 目錄。")
            : TargetIomDllResolution.Updated(foundPath);
    }

    private string? FindIomDllPathForCurrentSupportRoot()
    {
        if (UpgradePaths is null)
        {
            return null;
        }

        if (_hasCachedIomDllSearchResult &&
            string.Equals(
                _cachedIomDllSupportRoot,
                UpgradePaths.SupportRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            return _cachedIomDllPath;
        }

        _cachedIomDllSupportRoot = UpgradePaths.SupportRoot;
        _cachedIomDllPath = _iomDllLocatorService.FindIomDllPath(UpgradePaths.SupportRoot);
        _hasCachedIomDllSearchResult = true;
        return _cachedIomDllPath;
    }

    private void ClearIomDllSearchCache()
    {
        _hasCachedIomDllSearchResult = false;
        _cachedIomDllSupportRoot = string.Empty;
        _cachedIomDllPath = null;
    }

    private void UpdatePreview()
    {
        VariablePreview.Clear();
        PreviewWarningMessage = string.Empty;
        if (UpgradePaths is null || _configuration is null || SelectedConnection is null)
        {
            return;
        }

        var existingValues = LoadExistingCmdVariables();
        var values = BuildValues(existingValues, out var targetIomDll);
        PreviewWarningMessage = targetIomDll.WarningMessage ?? string.Empty;

        foreach (var pair in values)
        {
            existingValues.TryGetValue(pair.Key, out var originalValue);
            var isSecret = IsSecretVariable(pair.Key);
            VariablePreview.Add(new CmdVariablePreview(
                pair.Key,
                isSecret
                    ? PasswordMask.Create(pair.Value)
                    : pair.Value,
                originalValue is null
                    ? string.Empty
                    : isSecret
                        ? PasswordMask.Create(originalValue)
                        : originalValue,
                GetPreviewStatus(pair.Key, originalValue, pair.Value, targetIomDll)));
        }

        if (targetIomDll.IsSkipped)
        {
            VariablePreview.Add(new CmdVariablePreview(
                "TARGET_IOM_DLL",
                string.Empty,
                string.Empty,
                "Skipped"));
        }
        else if (targetIomDll.Exists &&
                 !values.ContainsKey("TARGET_IOM_DLL") &&
                 existingValues.TryGetValue("TARGET_IOM_DLL", out var originalValue))
        {
            VariablePreview.Add(new CmdVariablePreview(
                "TARGET_IOM_DLL",
                originalValue,
                originalValue,
                "Warning"));
        }
    }

    private static string GetPreviewStatus(
        string variableName,
        string? originalValue,
        string newValue,
        TargetIomDllResolution targetIomDll)
    {
        if (string.Equals(
                variableName,
                "TARGET_IOM_DLL",
                StringComparison.OrdinalIgnoreCase))
        {
            return targetIomDll.Status;
        }

        return originalValue is null
            ? "New"
            : string.Equals(originalValue, newValue, StringComparison.Ordinal)
                ? "Unchanged"
                : "Updated";
    }

    private IReadOnlyDictionary<string, string> LoadExistingCmdVariables()
    {
        if (UpgradePaths is null ||
            string.IsNullOrWhiteSpace(UpgradePaths.SetupCmdPath) ||
            !File.Exists(UpgradePaths.SetupCmdPath))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            return CmdGenerationService.ExtractSetVariables(
                File.ReadAllText(UpgradePaths.SetupCmdPath));
        }
        catch (Exception exception)
        {
            _logger.Error(
                exception,
                "Read SETUP CMD variables failed {SetupCmdPath}",
                UpgradePaths.SetupCmdPath);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static CmdVariableChangeDisplay ToDisplayChange(CmdVariableChange change)
    {
        var isPassword = IsSecretVariable(change.Name);
        return new CmdVariableChangeDisplay(
            change.Name,
            change.Action,
            isPassword && change.OldValue is not null
                ? PasswordMask.Create(change.OldValue)
                : change.OldValue ?? string.Empty,
            isPassword ? PasswordMask.Create(change.NewValue) : change.NewValue);
    }

    private static bool IsSecretVariable(string name) =>
        string.Equals(name, "AMLRUN_PASSWORD", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "SOURCE_SA_PASS", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "TARGET_SA_PASS", StringComparison.OrdinalIgnoreCase);

    private void NotifyPathProperties()
    {
        OnPropertyChanged(nameof(CommandFolder));
        OnPropertyChanged(nameof(SupportRoot));
        OnPropertyChanged(nameof(UpgradeRoot));
        OnPropertyChanged(nameof(Version));
        OnPropertyChanged(nameof(VersionCode));
        OnPropertyChanged(nameof(ReferencePath));
    }

    private void NotifyInnovatorProperties()
    {
        OnPropertyChanged(nameof(ApServerRoot));
        OnPropertyChanged(nameof(VaultConfigPath));
        OnPropertyChanged(nameof(ServerPrefix));
        OnPropertyChanged(nameof(WebUrl));
        NotifySelectedConnectionProperties();
    }

    private void NotifySelectedConnectionProperties()
    {
        OnPropertyChanged(nameof(SelectedSqlServer));
        OnPropertyChanged(nameof(SelectedDatabaseName));
    }

    private void InvalidateCompletion()
    {
        if (!IsCompleted && string.IsNullOrEmpty(GeneratedTargetPath))
        {
            return;
        }

        IsCompleted = false;
        GeneratedTargetPath = string.Empty;
        GenerationChanges.Clear();
        SetupInvalidated?.Invoke(this, EventArgs.Empty);
    }

    private sealed record TargetIomDllResolution(
        bool Exists,
        bool IsSkipped,
        string Status,
        string? SelectedPath,
        string? WarningMessage)
    {
        public static TargetIomDllResolution Skipped { get; } =
            new(false, true, "Skipped", null, null);

        public static TargetIomDllResolution Updated(string selectedPath) =>
            new(true, false, "Updated", selectedPath, null);

        public static TargetIomDllResolution Warning(string warningMessage) =>
            new(true, false, "Warning", null, warningMessage);
    }
}
