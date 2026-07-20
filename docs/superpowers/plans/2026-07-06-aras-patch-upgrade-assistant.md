# Aras Patch Upgrade Assistant Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** 建立可編譯、可測試的 .NET 8 WPF 升級助手，完成 SETUP CMD 產生與升級目錄檢查兩個 Wizard 步驟。

**Architecture:** 正式專案採 MVVM，將路徑解析、XML 解析、CMD 轉換與目錄檢查拆成無 UI 相依的服務；ViewModel 只協調狀態、命令與對話框。檔案系統操作透過小型介面隔離，讓錯誤路徑與目錄狀態可用 xUnit 決定性測試。

**Tech Stack:** C# 12、.NET 8 WPF、CommunityToolkit.Mvvm、xUnit、Microsoft.NET.Test.Sdk

---

## File structure

- `ArasPatchUpgradeAssistant.sln`：Solution 入口。
- `ArasPatchUpgradeAssistant/ArasPatchUpgradeAssistant.csproj`：WPF 正式專案。
- `ArasPatchUpgradeAssistant/Models/*.cs`：路徑、資料庫、CMD 與目錄檢查資料模型。
- `ArasPatchUpgradeAssistant/Services/*.cs`：純服務、檔案系統介面與 WPF 對話框 adapter。
- `ArasPatchUpgradeAssistant/ViewModels/*.cs`：Wizard、步驟一與步驟二狀態。
- `ArasPatchUpgradeAssistant/Views/*.xaml`：主視窗與兩個步驟。
- `ArasPatchUpgradeAssistant/Helpers/PasswordBoxAssistant.cs`：PasswordBox 雙向 binding。
- `ArasPatchUpgradeAssistant.Tests/**/*.cs`：與正式服務、ViewModel 一一對應的測試。
- `README.md`：範圍、操作、建置與安全限制。

### Task 1: 建立 Solution 與可執行骨架

**Files:**
- Create: `ArasPatchUpgradeAssistant.sln`
- Create: `ArasPatchUpgradeAssistant/ArasPatchUpgradeAssistant.csproj`
- Create: `ArasPatchUpgradeAssistant/App.xaml`
- Create: `ArasPatchUpgradeAssistant/App.xaml.cs`
- Create: `ArasPatchUpgradeAssistant/Views/MainWindow.xaml`
- Create: `ArasPatchUpgradeAssistant/Views/MainWindow.xaml.cs`
- Create: `ArasPatchUpgradeAssistant.Tests/ArasPatchUpgradeAssistant.Tests.csproj`
- Create: `ArasPatchUpgradeAssistant.Tests/SmokeTests.cs`

- [x] **Step 1: 建立失敗的 solution smoke test**

```csharp
namespace ArasPatchUpgradeAssistant.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void ApplicationAssembly_CanBeLoaded()
    {
        Assert.NotNull(typeof(App).Assembly);
    }
}
```

- [x] **Step 2: 執行測試並確認因專案尚未存在而失敗**

Run: `dotnet test ArasPatchUpgradeAssistant.sln`
Expected: FAIL，找不到 solution 或 `App` 型別。

- [x] **Step 3: 建立 `net8.0-windows` WPF 與 xUnit 專案**

正式專案設定：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
  </ItemGroup>
</Project>
```

測試專案參考正式專案，並加入 xUnit 與 test SDK。`App.xaml` 的 `StartupUri` 指向 `Views/MainWindow.xaml`，MainWindow 先顯示應用程式名稱。

- [x] **Step 4: 執行骨架測試與建置**

Run: `dotnet test ArasPatchUpgradeAssistant.sln`
Expected: PASS，1 test。

- [x] **Step 5: Commit**

```powershell
git add ArasPatchUpgradeAssistant.sln ArasPatchUpgradeAssistant ArasPatchUpgradeAssistant.Tests
git commit -m "build: scaffold WPF solution"
```

### Task 2: SETUP CMD 路徑解析

**Files:**
- Create: `ArasPatchUpgradeAssistant/Models/UpgradePathInfo.cs`
- Create: `ArasPatchUpgradeAssistant/Services/ISetupPathParser.cs`
- Create: `ArasPatchUpgradeAssistant/Services/SetupPathParser.cs`
- Create: `ArasPatchUpgradeAssistant.Tests/Services/SetupPathParserTests.cs`

- [x] **Step 1: 寫入路徑推導的失敗測試**

```csharp
[Fact]
public void Parse_ValidSetupPath_DerivesAllParts()
{
    var path = Path.Combine("K:\\", "10.Upgrades", "12SP18", "Support",
        "commands", "01-Upgrade", "SETUP-DEFAULTS-MACHINENAME.CMD");

    var result = new SetupPathParser().Parse(path);

    Assert.Equal("01-Upgrade", result.CommandFolder);
    Assert.Equal("12SP18", result.Version);
    Assert.Equal("120", result.VersionCode);
    Assert.EndsWith(Path.Combine("12SP18", "Support"), result.SupportRoot);
}

[Theory]
[InlineData("wrong.cmd")]
[InlineData("SETUP-DEFAULTS-MACHINENAME.txt")]
public void Parse_InvalidFilename_Throws(string filename)
{
    Assert.Throws<ArgumentException>(() => new SetupPathParser().Parse(filename));
}
```

- [x] **Step 2: 執行測試並確認缺少型別**

Run: `dotnet test --filter FullyQualifiedName~SetupPathParserTests`
Expected: FAIL，找不到 `SetupPathParser`。

- [x] **Step 3: 實作不可變路徑模型與 parser**

```csharp
public sealed record UpgradePathInfo(
    string SetupCmdPath,
    string CommandFolder,
    string SupportRoot,
    string UpgradeRoot,
    string Version,
    string VersionCode);

public interface ISetupPathParser
{
    UpgradePathInfo Parse(string setupCmdPath);
}
```

`SetupPathParser.Parse` 先驗證完整路徑與檔名，再由父目錄向上精確比對 `commands`、`Support`，最後以 `^(?<major>\d+)SP(?<sp>\d+)$` 驗證版本，並以 major 加 `0` 產生版本代碼。

- [x] **Step 4: 補齊錯誤檔名、錯誤結構、錯誤版本與大小寫測試並執行**

Run: `dotnet test --filter FullyQualifiedName~SetupPathParserTests`
Expected: PASS。

- [x] **Step 5: Commit**

```powershell
git add ArasPatchUpgradeAssistant/Models ArasPatchUpgradeAssistant/Services ArasPatchUpgradeAssistant.Tests/Services
git commit -m "feat: parse upgrade paths from setup command"
```

### Task 3: Innovator XML 與 Web URL 解析

**Files:**
- Create: `ArasPatchUpgradeAssistant/Models/DatabaseConnectionOption.cs`
- Create: `ArasPatchUpgradeAssistant/Models/InnovatorConfiguration.cs`
- Create: `ArasPatchUpgradeAssistant/Services/IInnovatorConfigService.cs`
- Create: `ArasPatchUpgradeAssistant/Services/InnovatorConfigService.cs`
- Create: `ArasPatchUpgradeAssistant.Tests/Services/InnovatorConfigServiceTests.cs`

- [x] **Step 1: 寫入 DB connection、vault 兩種格式與 URL 正規化測試**

```csharp
[Fact]
public void Load_ValidFiles_ReturnsConnectionsAndPrefix()
{
    var configPath = Fixture.Write("InnovatorServerConfig.xml",
        "<Config><DB-Connection id=\"Main\" database=\"Innovator\" /></Config>");
    Fixture.Write(Path.Combine("VaultServer", "vault.config"),
        "<Config><InnovatorServerUrl value=\"http://localhost/InnovatorServer/Server/InnovatorServer.aspx/\" /></Config>");

    var result = new InnovatorConfigService().Load(configPath);

    Assert.Equal(new DatabaseConnectionOption("Main", "Innovator"), result.Connections.Single());
    Assert.Equal("http://localhost/InnovatorServer", result.ServerPrefix);
}
```

- [x] **Step 2: 執行測試並確認缺少服務**

Run: `dotnet test --filter FullyQualifiedName~InnovatorConfigServiceTests`
Expected: FAIL，找不到 `InnovatorConfigService`。

- [x] **Step 3: 實作安全的 XML 讀取**

```csharp
public sealed record DatabaseConnectionOption(string Label, string Database);
public sealed record InnovatorConfiguration(
    string ConfigPath,
    string ApServerRoot,
    IReadOnlyList<DatabaseConnectionOption> Connections,
    string ServerPrefix);

public interface IInnovatorConfigService
{
    InnovatorConfiguration Load(string configPath);
}
```

服務使用 `XDocument.Load`；只保留同時具有非空白 `id`、`database` 的 `DB-Connection`。Vault URL 先讀 `value` attribute，再讀節點文字，移除尾端斜線後大小寫不敏感地移除 `/Server/InnovatorServer.aspx`。

- [x] **Step 4: 補齊多筆、無效 connection、缺少 vault、節點文字與 malformed XML 測試**

Run: `dotnet test --filter FullyQualifiedName~InnovatorConfigServiceTests`
Expected: PASS。

- [x] **Step 5: Commit**

```powershell
git add ArasPatchUpgradeAssistant/Models ArasPatchUpgradeAssistant/Services ArasPatchUpgradeAssistant.Tests/Services
git commit -m "feat: parse Innovator server configuration"
```

### Task 4: CMD 變數產生、轉換與安全寫入

**Files:**
- Create: `ArasPatchUpgradeAssistant/Models/CmdVariableChange.cs`
- Create: `ArasPatchUpgradeAssistant/Models/CmdGenerationResult.cs`
- Create: `ArasPatchUpgradeAssistant/Services/CmdVariableBuilder.cs`
- Create: `ArasPatchUpgradeAssistant/Services/ICmdGenerationService.cs`
- Create: `ArasPatchUpgradeAssistant/Services/CmdGenerationService.cs`
- Create: `ArasPatchUpgradeAssistant.Tests/Services/CmdVariableBuilderTests.cs`
- Create: `ArasPatchUpgradeAssistant.Tests/Services/CmdGenerationServiceTests.cs`

- [x] **Step 1: 寫入 20 個變數、兩種 SET 格式、保留註解、追加與來源不變測試**

```csharp
[Fact]
public void Transform_UpdatesQuotedAndPlainSet_AndAppendsMissing()
{
    const string source = "@REM keep\r\n@SET TOOLS_FOLDER=old\r\n@SET \"AMLRUN_LOGINNAME=old-user\"\r\n";
    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["TOOLS_FOLDER"] = @"K:\Support\tools\DBUpdateTool",
        ["AMLRUN_LOGINNAME"] = "root",
        ["AMLRUN_PASSWORD"] = "innovator"
    };

    var result = CmdGenerationService.Transform(source, values);

    Assert.Contains("@REM keep\r\n", result.Content);
    Assert.Contains("@SET TOOLS_FOLDER=K:\\Support\\tools\\DBUpdateTool\r\n", result.Content);
    Assert.Contains("@SET \"AMLRUN_LOGINNAME=root\"\r\n", result.Content);
    Assert.EndsWith("@SET AMLRUN_PASSWORD=innovator\r\n", result.Content);
    Assert.Equal(["更新", "更新", "新增"], result.Changes.Select(x => x.Action));
}
```

- [x] **Step 2: 執行測試並確認缺少服務**

Run: `dotnet test --filter "FullyQualifiedName~CmdVariableBuilderTests|FullyQualifiedName~CmdGenerationServiceTests"`
Expected: FAIL。

- [x] **Step 3: 實作值建構與純文字轉換核心**

```csharp
public sealed record CmdVariableChange(string Name, string Action, string? OldValue, string NewValue);
public sealed record CmdGenerationResult(string TargetPath, IReadOnlyList<CmdVariableChange> Changes);
```

`CmdVariableBuilder.Build` 依規格固定順序建立 20 個值。`CmdGenerationService.Transform` 以逐行、大小寫不敏感 regex 更新既有變數，只改等號與結尾引號間的值；未命中的目標變數在末端以 `@SET NAME=value` 追加，輸出一律 CRLF。

- [x] **Step 4: 實作保留 BOM/編碼的 `Generate`**

讀取前 3 bytes 判斷 UTF-8 BOM，前 2 bytes 判斷 UTF-16 LE/BE；無 BOM 時以 `Encoding.Default` 解碼與編碼。先完整轉換，再寫入機器名稱目標檔；來源與目標路徑必須不同，且目標覆蓋由 ViewModel 在呼叫前確認。

- [x] **Step 5: 執行 CMD 服務完整測試**

Run: `dotnet test --filter "FullyQualifiedName~CmdVariableBuilderTests|FullyQualifiedName~CmdGenerationServiceTests"`
Expected: PASS，並驗證 CRLF、BOM、無 BOM、機器名稱與來源未改寫。

- [x] **Step 6: Commit**

```powershell
git add ArasPatchUpgradeAssistant/Models ArasPatchUpgradeAssistant/Services ArasPatchUpgradeAssistant.Tests/Services
git commit -m "feat: generate machine-specific setup command"
```

### Task 5: 目錄快照檢查與建立

**Files:**
- Create: `ArasPatchUpgradeAssistant/Models/DirectoryValidationItem.cs`
- Create: `ArasPatchUpgradeAssistant/Models/DirectoryValidationSnapshot.cs`
- Create: `ArasPatchUpgradeAssistant/Services/IFileSystem.cs`
- Create: `ArasPatchUpgradeAssistant/Services/SystemFileSystem.cs`
- Create: `ArasPatchUpgradeAssistant/Services/IDirectoryValidationService.cs`
- Create: `ArasPatchUpgradeAssistant/Services/DirectoryValidationService.cs`
- Create: `ArasPatchUpgradeAssistant.Tests/Services/DirectoryValidationServiceTests.cs`
- Create: `ArasPatchUpgradeAssistant.Tests/TestDoubles/FakeFileSystem.cs`

- [x] **Step 1: 寫入固定順序、路徑、狀態與建立資料夾測試**

```csharp
[Fact]
public void Validate_ReturnsTwelveFoldersAndGeneratedFileInFixedOrder()
{
    var snapshot = CreateService().Validate(Paths, GeneratedCmd);

    Assert.Equal(13, snapshot.Items.Count);
    Assert.Equal("Support Root", snapshot.Items[0].Name);
    Assert.Equal("backup", snapshot.Items[11].Name);
    Assert.Equal(DirectoryItemKind.File, snapshot.Items[12].Kind);
}

[Fact]
public void CreateDirectory_RejectsFileItem()
{
    var file = new DirectoryValidationItem("Generated SETUP CMD", DirectoryItemKind.File,
        GeneratedCmd, false, DirectoryValidationStatus.Missing, "", false);

    Assert.Throws<InvalidOperationException>(() => CreateService().CreateDirectory(file));
}
```

- [x] **Step 2: 執行測試並確認缺少型別**

Run: `dotnet test --filter FullyQualifiedName~DirectoryValidationServiceTests`
Expected: FAIL。

- [x] **Step 3: 實作模型、檔案系統介面與 13 項快照**

```csharp
public enum DirectoryItemKind { Folder, File }
public enum DirectoryValidationStatus { OK, Missing, Warning }
public sealed record DirectoryValidationItem(
    string Name,
    DirectoryItemKind Kind,
    string FullPath,
    bool Exists,
    DirectoryValidationStatus Status,
    string ErrorMessage,
    bool CanCreate);
public sealed record DirectoryValidationSnapshot(
    DateTimeOffset CheckedAt,
    IReadOnlyList<DirectoryValidationItem> Items);
```

`IFileSystem` 提供 `DirectoryExists`、`FileExists`、`EnumerateFileSystemEntries`、`CreateDirectory`。服務固定建立規格中的 13 個 descriptor，逐項 try/catch；一般空目錄為 Warning，LOGS/backup 空目錄仍為 OK，例外轉繁體中文訊息並繼續。

- [x] **Step 4: 實作建立缺少 Folder**

`CreateDirectory` 必須再次檢查 `Kind == Folder`；Folder 已存在直接成功，否則呼叫檔案系統建立。File 項目一律擲出 `InvalidOperationException`。

- [x] **Step 5: 補齊 Missing、空目錄、允許空目錄、單項例外隔離與 idempotent 建立測試**

Run: `dotnet test --filter FullyQualifiedName~DirectoryValidationServiceTests`
Expected: PASS。

- [x] **Step 6: Commit**

```powershell
git add ArasPatchUpgradeAssistant/Models ArasPatchUpgradeAssistant/Services ArasPatchUpgradeAssistant.Tests
git commit -m "feat: validate and create upgrade directories"
```

### Task 6: 第 1 步 ViewModel

**Files:**
- Create: `ArasPatchUpgradeAssistant/Services/IFileDialogService.cs`
- Create: `ArasPatchUpgradeAssistant/Services/IMessageDialogService.cs`
- Create: `ArasPatchUpgradeAssistant/ViewModels/SetupStepViewModel.cs`
- Create: `ArasPatchUpgradeAssistant.Tests/ViewModels/SetupStepViewModelTests.cs`
- Create: `ArasPatchUpgradeAssistant.Tests/TestDoubles/DialogFakes.cs`

- [x] **Step 1: 寫入選檔、預設連線、遮罩預覽、取消覆蓋與成功事件測試**

```csharp
[Fact]
public void LoadInnovatorConfig_SelectsFirstConnection()
{
    var vm = CreateViewModel();

    vm.LoadInnovatorConfig(ConfigPath);

    Assert.Equal("Innovator", vm.SelectedConnection?.Database);
    Assert.Equal("********", vm.MaskedPassword);
}

[Fact]
public void Generate_Success_RaisesCompletionWithPathsAndResult()
{
    var vm = CreateValidViewModel();
    SetupCompletedEventArgs? completed = null;
    vm.SetupCompleted += (_, args) => completed = args;

    vm.GenerateCommand.Execute(null);

    Assert.NotNull(completed);
    Assert.True(vm.IsCompleted);
}
```

- [x] **Step 2: 執行測試並確認缺少 ViewModel**

Run: `dotnet test --filter FullyQualifiedName~SetupStepViewModelTests`
Expected: FAIL。

- [x] **Step 3: 以 CommunityToolkit.Mvvm 實作狀態與命令**

ViewModel 注入 parser、config service、variable builder、generation service 與兩個 dialog service。公開可 binding 的路徑、推導值、連線集合、使用者名稱、密碼、預覽、結果與 `IsCompleted`；所有服務錯誤捕捉後由訊息服務以繁體中文顯示。產生前集中驗證，目標存在時先確認覆蓋。

- [x] **Step 4: 執行 ViewModel 測試**

Run: `dotnet test --filter FullyQualifiedName~SetupStepViewModelTests`
Expected: PASS。

- [x] **Step 5: Commit**

```powershell
git add ArasPatchUpgradeAssistant/Services ArasPatchUpgradeAssistant/ViewModels ArasPatchUpgradeAssistant.Tests
git commit -m "feat: orchestrate setup command workflow"
```

### Task 7: 第 2 步與 Wizard 導覽 ViewModel

**Files:**
- Create: `ArasPatchUpgradeAssistant/ViewModels/DirectoryValidationStepViewModel.cs`
- Create: `ArasPatchUpgradeAssistant/ViewModels/MainWindowViewModel.cs`
- Create: `ArasPatchUpgradeAssistant.Tests/ViewModels/DirectoryValidationStepViewModelTests.cs`
- Create: `ArasPatchUpgradeAssistant.Tests/ViewModels/MainWindowViewModelTests.cs`

- [x] **Step 1: 寫入重新整理、建立後刷新與導覽 gate 測試**

```csharp
[Fact]
public void NavigateToDirectoryValidation_BeforeSetup_ShowsBlockingMessage()
{
    var vm = CreateMainViewModel(setupCompleted: false);

    vm.NavigateCommand.Execute(2);

    Assert.Equal(1, vm.CurrentStep);
    Assert.Contains("請先完成基本設定", Messages.Last());
}

[Fact]
public void CreateFolder_WhenConfirmed_CreatesAndRefreshesAllItems()
{
    var vm = CreateDirectoryViewModel(confirm: true);

    vm.CreateFolderCommand.Execute(MissingFolder);

    Assert.Equal(2, ValidationService.ValidateCallCount);
    Assert.Contains(MissingFolder.FullPath, ValidationService.CreatedPaths);
}
```

- [x] **Step 2: 執行測試並確認缺少 ViewModel**

Run: `dotnet test --filter "FullyQualifiedName~DirectoryValidationStepViewModelTests|FullyQualifiedName~MainWindowViewModelTests"`
Expected: FAIL。

- [x] **Step 3: 實作快照摘要與建立命令**

第 2 步 ViewModel 持有目前 `UpgradePathInfo` 與 generated CMD 路徑；`Refresh` 替換完整 ObservableCollection、更新時間與 OK/Missing/Warning 數量。建立命令先確認完整路徑，確認後呼叫服務並重新整理；例外只顯示訊息。

- [x] **Step 4: 實作 Wizard gate**

Main ViewModel 建立六個步驟；第 1 步完成前只允許步驟 1，第 1 步完成事件保存 context 並允許步驟 2。步驟 3–6 顯示「此步驟尚未在第一版實作」且不切換；每次進入步驟 2 都刷新。

- [x] **Step 5: 執行導覽與步驟二測試**

Run: `dotnet test --filter "FullyQualifiedName~DirectoryValidationStepViewModelTests|FullyQualifiedName~MainWindowViewModelTests"`
Expected: PASS。

- [x] **Step 6: Commit**

```powershell
git add ArasPatchUpgradeAssistant/ViewModels ArasPatchUpgradeAssistant.Tests/ViewModels
git commit -m "feat: add directory validation wizard workflow"
```

### Task 8: WPF 畫面、PasswordBox 與服務組裝

**Files:**
- Create: `ArasPatchUpgradeAssistant/Helpers/PasswordBoxAssistant.cs`
- Create: `ArasPatchUpgradeAssistant/Services/WpfFileDialogService.cs`
- Create: `ArasPatchUpgradeAssistant/Services/WpfMessageDialogService.cs`
- Create: `ArasPatchUpgradeAssistant/Views/SetupStepView.xaml`
- Create: `ArasPatchUpgradeAssistant/Views/SetupStepView.xaml.cs`
- Create: `ArasPatchUpgradeAssistant/Views/DirectoryValidationStepView.xaml`
- Create: `ArasPatchUpgradeAssistant/Views/DirectoryValidationStepView.xaml.cs`
- Modify: `ArasPatchUpgradeAssistant/Views/MainWindow.xaml`
- Modify: `ArasPatchUpgradeAssistant/Views/MainWindow.xaml.cs`
- Create: `ArasPatchUpgradeAssistant.Tests/Helpers/PasswordMaskTests.cs`

- [x] **Step 1: 寫入密碼預覽永不回傳明碼的測試**

```csharp
[Theory]
[InlineData("")]
[InlineData("innovator")]
[InlineData("秘密")]
public void MaskPassword_NeverReturnsPlainText(string password)
{
    Assert.Equal(new string('*', password.Length), PasswordMask.Create(password));
}
```

- [x] **Step 2: 實作 PasswordBox binding 與 WPF dialogs**

`PasswordBoxAssistant` 用 attached dependency property 同步 `PasswordBox.Password`；file dialog 僅接受指定 CMD 或 XML；message dialog 分別提供 error 與 yes/no confirmation。

- [x] **Step 3: 建立第 1 步畫面**

使用 Grid 呈現兩個檔案選擇區、推導資訊、DB ComboBox、帳密、遮罩預覽、產生按鈕與變更摘要。敏感密碼只存在 PasswordBox binding 與 ViewModel，不出現在摘要 DataGrid。

- [x] **Step 4: 建立第 2 步畫面**

顯示根目錄、版本、檢查時間、三種計數及 13 項 DataGrid；建立按鈕以 `CanCreate` 控制可見性，CommandParameter 傳入該列 item。

- [x] **Step 5: 組裝主視窗**

左側建立六步導覽按鈕，右側依 `CurrentStep` 切換兩個 UserControl；MainWindow code-behind 只建立正式 service graph 並指派 DataContext。

- [x] **Step 6: 執行所有測試與 WPF 建置**

Run: `dotnet test ArasPatchUpgradeAssistant.sln`
Expected: PASS。

Run: `dotnet build ArasPatchUpgradeAssistant.sln -c Release`
Expected: Build succeeded，0 errors。

- [x] **Step 7: Commit**

```powershell
git add ArasPatchUpgradeAssistant ArasPatchUpgradeAssistant.Tests
git commit -m "feat: build two-step WPF wizard"
```

### Task 9: 文件、完整驗證與安全範圍檢查

**Files:**
- Create: `README.md`
- Modify: `docs/superpowers/plans/2026-07-06-aras-patch-upgrade-assistant.md`

- [x] **Step 1: 撰寫 README**

說明 Windows/.NET 8 需求、`dotnet build`、`dotnet run --project ArasPatchUpgradeAssistant`、`dotnet test`、兩步驟操作、覆蓋確認、快照刷新、明確建立目錄，以及「不執行 CMD/BAT/SQL、不連線 PLM/DB」。

- [x] **Step 2: 掃描禁止的執行 API**

Run: `rg "Process\\.Start|SqlConnection|DbConnection|Invoke-Expression|cmd\\.exe|powershell\\.exe" ArasPatchUpgradeAssistant`
Expected: 無結果。

- [x] **Step 3: 執行完整驗證**

Run: `dotnet test ArasPatchUpgradeAssistant.sln -c Release`
Expected: 全部 PASS。

Run: `dotnet build ArasPatchUpgradeAssistant.sln -c Release --no-restore`
Expected: Build succeeded，0 warnings，0 errors。

- [x] **Step 4: 檢查工作樹只含預期變更**

Run: `git status --short`
Expected: 僅 README 與本計畫勾選狀態（若實作 commits 已逐項提交則可能只剩 README）。

- [x] **Step 5: Commit**

```powershell
git add README.md docs/superpowers/plans/2026-07-06-aras-patch-upgrade-assistant.md
git commit -m "docs: add usage and safety guidance"
```
