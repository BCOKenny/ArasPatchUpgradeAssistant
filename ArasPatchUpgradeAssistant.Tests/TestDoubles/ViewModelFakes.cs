using ArasPatchUpgradeAssistant.Models;
using ArasPatchUpgradeAssistant.Services;

namespace ArasPatchUpgradeAssistant.Tests.TestDoubles;

public sealed class FakeFileDialogService : IFileDialogService
{
    public string? SetupPath { get; set; }
    public string? ConfigPath { get; set; }

    public string? SelectSetupCommand() => SetupPath;

    public string? SelectInnovatorConfig() => ConfigPath;
}

public sealed class FakeMessageDialogService : IMessageDialogService
{
    public bool ConfirmResult { get; set; } = true;
    public List<string> Errors { get; } = [];
    public List<string> Confirmations { get; } = [];

    public void ShowError(string message) => Errors.Add(message);

    public bool Confirm(string title, string message)
    {
        Confirmations.Add($"{title}: {message}");
        return ConfirmResult;
    }
}

public sealed class FakeUserSettingsService : IUserSettingsService
{
    public string SettingsFilePath { get; set; } =
        @"C:\Users\kenny\AppData\Local\ArasPatchUpgradeAssistant\settings.json";

    public UserSettings SettingsToLoad { get; set; } = new();

    public UserSettings? SavedSettings { get; private set; }

    public int LoadCallCount { get; private set; }

    public int SaveCallCount { get; private set; }

    public UserSettings Load()
    {
        LoadCallCount++;
        return SettingsToLoad;
    }

    public void Save(UserSettings settings)
    {
        SaveCallCount++;
        SavedSettings = new UserSettings
        {
            SetupDefaultsTemplatePath = settings.SetupDefaultsTemplatePath,
            InnovatorServerConfigPath = settings.InnovatorServerConfigPath,
            SelectedDatabaseId = settings.SelectedDatabaseId,
            SelectedDatabaseName = settings.SelectedDatabaseName,
            CopySourceDbName = settings.CopySourceDbName,
            LoginName = settings.LoginName,
            EncryptedPassword = settings.EncryptedPassword,
            SqlLoginName = settings.SqlLoginName,
            EncryptedSqlPassword = settings.EncryptedSqlPassword
        };
    }
}

public sealed class FakeSecretProtectionService : ISecretProtectionService
{
    public bool ThrowOnUnprotect { get; set; }

    public List<string> ProtectedPlainTexts { get; } = [];

    public string Protect(string plainText)
    {
        ProtectedPlainTexts.Add(plainText);
        return string.IsNullOrEmpty(plainText)
            ? string.Empty
            : Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes($"protected:{plainText}"));
    }

    public string Unprotect(string protectedText)
    {
        if (ThrowOnUnprotect)
        {
            throw new InvalidOperationException("cannot decrypt");
        }

        var decoded = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(protectedText));
        return decoded.StartsWith("protected:", StringComparison.Ordinal)
            ? decoded["protected:".Length..]
            : decoded;
    }
}

public sealed class StubSetupPathParser(UpgradePathInfo paths) : ISetupPathParser
{
    public UpgradePathInfo Parse(string setupCmdPath) => paths;
}

public sealed class ConfigurableSetupPathParser(UpgradePathInfo paths) : ISetupPathParser
{
    public Exception? Error { get; set; }

    public UpgradePathInfo Parse(string setupCmdPath) =>
        Error is null ? paths : throw Error;
}

public sealed class StubInnovatorConfigService(InnovatorConfiguration configuration)
    : IInnovatorConfigService
{
    public InnovatorConfiguration Load(string configPath) => configuration;
}

public sealed class StubCmdGenerationService(string targetPath) : ICmdGenerationService
{
    public int CallCount { get; private set; }
    public IReadOnlyDictionary<string, string>? LastValues { get; private set; }

    public CmdGenerationResult Generate(
        string sourcePath,
        string machineName,
        IReadOnlyDictionary<string, string> values)
    {
        CallCount++;
        LastValues = values;
        return new CmdGenerationResult(
            targetPath,
            [
                new CmdVariableChange("AMLRUN_LOGINNAME", "更新", "old", values["AMLRUN_LOGINNAME"]),
                new CmdVariableChange("AMLRUN_PASSWORD", "更新", "old-secret", values["AMLRUN_PASSWORD"]),
                new CmdVariableChange("SOURCE_SA_PASS", "更新", "old-secret", values["SOURCE_SA_PASS"]),
                new CmdVariableChange("TARGET_SA_PASS", "更新", "old-secret", values["TARGET_SA_PASS"])
            ]);
    }
}

public sealed class StubIomDllLocatorService : IIomDllLocatorService
{
    public int CallCount { get; private set; }
    public string? LastSupportRoot { get; private set; }
    public string? PathToReturn { get; set; }

    public string? FindIomDllPath(string supportRoot)
    {
        CallCount++;
        LastSupportRoot = supportRoot;
        return PathToReturn;
    }
}

public sealed class FakeDirectoryValidationService : IDirectoryValidationService
{
    public int ValidateCallCount { get; private set; }
    public List<string> CreatedPaths { get; } = [];
    public IReadOnlyList<DirectoryValidationItem> Items { get; set; } = [];

    public DirectoryValidationSnapshot Validate(
        UpgradePathInfo paths,
        string generatedCmdPath)
    {
        ValidateCallCount++;
        return new DirectoryValidationSnapshot(
            new DateTimeOffset(2026, 7, 6, 13, 0, 0, TimeSpan.FromHours(8)),
            Items);
    }

    public void CreateDirectory(DirectoryValidationItem item) =>
        CreatedPaths.Add(item.FullPath);
}
