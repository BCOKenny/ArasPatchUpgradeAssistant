# Vault 路徑、使用者設定與 Serilog 補強設計

## 1. 目標與範圍

本次只補強既有第 1、2 步：

1. 將 vault.config 路徑推導與解析拆分為獨立服務，並在第 1 步顯示路徑。
2. 以本機 JSON 設定檔保存使用者上次選擇。
3. 以 Serilog 記錄應用程式與主要操作事件。

本次不新增任何升級執行功能，不執行 BAT、CMD、SQL，不測試 PLM 連線，也不測試資料庫連線。

## 2. 架構

沿用單一 WPF 正式專案與獨立 xUnit 測試專案。採輕量 App 啟動器與明確服務注入，不引入 `Microsoft.Extensions.Hosting`。

新增或調整的主要元件：

- `Models/UserSettings.cs`
  - 只包含可安全持久化的路徑、DB 選項與 loginName。
- `Services/IUserSettingsService.cs`
  - 提供可替換、可測試的設定服務合約。
- `Services/UserSettingsService.cs`
  - 實作 `Load()`、`Save()` 與 `SettingsFilePath`。
- `Services/IVaultConfigService.cs`
  - 提供可替換、可測試的 Vault 服務合約。
- `Services/VaultConfigService.cs`
  - 實作 `GetVaultConfigPath()` 與 `ParseInnovatorServerUrl()`。
- `Services/LoggingConfigurator.cs`
  - 建立 Serilog logger，集中 rolling file 與 debug sink 設定。
- `App.xaml.cs`
  - 初始化 logger、建立服務與 ViewModel、載入 settings、顯示 MainWindow、結束時 flush。
- `SetupStepViewModel`
  - 協調設定還原、立即儲存、Vault 路徑顯示與安全事件紀錄。

`MainWindow` 改由 App 手動建立並注入 MainWindowViewModel；`App.xaml` 不再使用 `StartupUri`。

## 3. VaultConfigService

### 3.1 路徑推導

`GetVaultConfigPath(string innovatorServerConfigPath)`：

1. 驗證輸入不為空。
2. 取得完整路徑。
3. 取得 `InnovatorServerConfig.xml` 所在目錄作為 AP Server Root。
4. 只回傳：

```text
{AP Server Root}\VaultServer\vault.config
```

範例：

```text
輸入：
C:\Program Files (x86)\Aras\1209\InnovatorServerConfig.xml

輸出：
C:\Program Files (x86)\Aras\1209\VaultServer\vault.config
```

不得搜尋或使用其他固定目錄。

### 3.2 URL 解析

`ParseInnovatorServerUrl(string vaultConfigPath)`：

- 驗證 vault.config 存在。
- 記錄解析 started。
- 主要解析標準 .NET appSettings 結構：
  - 以 `element.Name.LocalName` 且忽略大小寫尋找 `add` 元素。
  - 以忽略大小寫的屬性名稱取得 `key`。
  - 找出 `key` 值忽略大小寫等於 `InnovatorServerUrl` 的 `add` 元素。
  - 以忽略大小寫的屬性名稱取得 `value`。
- 為相容既有測試資料，也保留直接 `InnovatorServerUrl` 元素的 fallback，支援 `value` attribute 或節點文字；元素與屬性名稱同樣忽略大小寫並使用 LocalName。
- 找不到有效值時拋出可由 UI 捕捉的資料格式錯誤；錯誤訊息必須包含實際 vault.config 完整路徑。
- 記錄 completed 或 failed；failed 記錄 exception，但不造成應用程式未處理例外。

實際支援範例：

```xml
<configuration>
  <appSettings>
    <add key="InnovatorServerUrl"
         value="http://localhost/APV8/Server/InnovatorServer.aspx"></add>
  </appSettings>
</configuration>
```

解析結果：

```text
InnovatorServerUrl = http://localhost/APV8/Server/InnovatorServer.aspx
ServerPrefix       = http://localhost/APV8
```

`InnovatorConfigService` 注入 IVaultConfigService，負責：

- 解析 DB-Connection。
- 取得 VaultConfigPath。
- 取得原始 InnovatorServerUrl。
- 將 URL 正規化為 ServerPrefix。

`InnovatorConfiguration` 增加：

- `VaultConfigPath`
- `InnovatorServerUrl`

## 4. UserSettingsService

### 4.1 儲存位置與格式

正式設定檔固定為：

```text
%LocalAppData%\ArasPatchUpgradeAssistant\settings.json
```

JSON 使用 camelCase，欄位為：

- `setupDefaultsTemplatePath`
- `innovatorServerConfigPath`
- `selectedDatabaseId`
- `selectedDatabaseName`
- `loginName`

`UserSettings` 不含 Password 或 AMLRUN_PASSWORD 屬性。

### 4.2 Load

- 記錄 load started。
- settings.json 不存在時，回傳 loginName 為 `root` 的預設設定並記錄 completed。
- 檔案存在時，以 System.Text.Json 載入。
- null 或空白 loginName 正規化為 `root`。
- JSON 損壞、權限或 I/O 失敗時記錄 failed 並向呼叫端拋出；App 捕捉後顯示提示並以預設設定繼續啟動。

### 4.3 Save

- 記錄 save started。
- 建立 `%LocalAppData%\ArasPatchUpgradeAssistant` 目錄。
- 先寫入同目錄暫存檔，再以取代方式更新 settings.json。
- 記錄 completed 或 failed。
- failed 向 ViewModel 拋出；ViewModel 顯示提示，但不中止使用者原本的操作。
- 序列化輸出不得出現 Password、AMLRUN_PASSWORD 或密碼值。

測試可由建構式傳入替代 settings 路徑，避免寫入真實使用者目錄。

## 5. 啟動還原與即時儲存

### 5.1 啟動

App 初始化順序：

1. 決定 `%LocalAppData%\ArasPatchUpgradeAssistant\logs`。
2. 初始化 Serilog。
3. 記錄 `Application started`。
4. 建立 SystemFileSystem、VaultConfigService、InnovatorConfigService、UserSettingsService 與 ViewModels。
5. 呼叫 UserSettingsService.Load。
6. 呼叫 SetupStepViewModel.RestoreSettings。
7. 顯示 MainWindow。

RestoreSettings 在 `_isRestoringSettings` 防護期間執行，避免還原中的屬性變更反向覆蓋設定。

SETUP template：

- 設定值存在且檔案存在：重新解析並更新 Version、VersionCode、CommandFolder、UpgradeRoot、SupportRoot、ReferencePath。
- 設定值存在但檔案不存在：保留 settings 中的舊值，畫面 Setup 欄位保持空白，顯示提示。

Innovator config：

- 設定值存在且檔案存在：重新推導 VaultConfigPath、解析 URL、ServerPrefix 與 DB 清單。
- 設定值存在但檔案不存在：保留 settings 中的舊值，畫面 Innovator 欄位保持空白，顯示提示。
- DB 清單完成後，優先尋找 database 等於 `selectedDatabaseName` 且 id 等於 `selectedDatabaseId` 的項目，再 fallback 至 database 相同的項目，最後才選第一筆。

密碼每次啟動仍為 `innovator`，不從任何設定或 log 還原。

### 5.2 即時儲存

- 使用者由檔案選擇器選取 SETUP CMD 後，先更新並儲存該路徑，再執行解析。
- 使用者由檔案選擇器選取 InnovatorServerConfig.xml 後，先更新並儲存該路徑，再執行解析。
- 使用者改變 SelectedConnection 時，儲存 id 與 database。
- 使用者改變 LoginName 時，儲存 loginName。
- 程式化還原期間不觸發上述儲存。
- Save 失敗時顯示錯誤並記錄 exception；不清除目前畫面資料。

## 6. Serilog

加入：

- `Serilog`
- `Serilog.Sinks.File`
- `Serilog.Sinks.Debug`

正式 log 目錄：

```text
%LocalAppData%\ArasPatchUpgradeAssistant\logs
```

rolling file pattern：

```text
aras-patch-upgrade-assistant-.log
```

採每日 rolling，保留的每行格式包含：

- Timestamp
- Level
- Message
- Exception

記錄事件：

1. Application started
2. Application exited
3. Load user settings started / completed / failed
4. Save user settings started / completed / failed
5. User selected SETUP CMD path
6. Derived upgrade path information
7. User selected InnovatorServerConfig.xml path
8. Derived vault.config path
9. Parse vault.config started / completed / failed
10. Parse DB-Connection started / completed / failed
11. Generate SETUP-DEFAULTS-{MachineName}.CMD started / completed / failed

Logging 安全規則：

- 不記錄 Password、AMLRUN_PASSWORD 或 ViewModel.Password。
- 不把完整 CMD 變數 dictionary、預覽集合或未遮罩的變更摘要傳給 logger。
- CMD 產生完成只記錄 target path 與變更數量。
- 需要顯示密碼相關名稱時，其值固定為 `******`。
- 測試使用特殊密碼字串並掃描 log，確認明文不存在。

App 結束時記錄 `Application exited`，再呼叫 `Log.CloseAndFlush()`。

## 7. UI

第 1 步增加或保留以下唯讀欄位：

- ReferencePath：等於已選取的 `SETUP-DEFAULTS-MACHINENAME.CMD` 完整路徑。
- VaultConfigPath：推導出的 vault.config 完整路徑。
- Web URL / AMLRUN_SERVERPREFIX：既有 ServerPrefix。
- SettingsFilePath：實際 settings.json 完整路徑。
- LogDirectory：實際 log 目錄。

所有使用 TextBox 顯示的唯讀欄位必須同時設定：

```xml
Text="{Binding Xxx, Mode=OneWay}"
IsReadOnly="True"
```

選取有效 Innovator config 後，ViewModel 先設定 InnovatorConfigPath、ApServerRoot 與 VaultConfigPath，再進行 Vault/DB 解析。vault.config 不存在或格式錯誤時，保留推導路徑並顯示錯誤，不讓程式終止。

## 8. 錯誤處理

- settings 不存在不是錯誤。
- settings 損壞、讀取失敗：提示後使用預設設定繼續。
- 儲存 settings 失敗：提示但不回復使用者剛完成的畫面選擇。
- 已儲存路徑失效：保留 JSON 舊值、畫面留白並提示。
- vault.config 不存在：顯示推導出的 VaultConfigPath 與錯誤提示。
- DB 或 Vault XML 格式錯誤：顯示錯誤，允許重新選取。
- 所有 failed log 都包含 exception；不得附帶 password 或變數值集合。

## 9. 測試

至少新增或更新：

1. Windows 範例 InnovatorServerConfig.xml 推導出同層 `VaultServer\vault.config`。
2. Vault URL 解析支援 `appSettings/add key="InnovatorServerUrl" value="..."`，並將 APV8 範例正規化為 `http://localhost/APV8`。
3. `add`、`key`、`value` 的元素或屬性名稱大小寫不同且 XML 含 namespace 時仍可解析。
4. Vault URL 解析保留直接 InnovatorServerUrl 元素的 value attribute 與節點文字相容性。
5. Vault URL 缺少時，exception message 包含實際 vault.config 完整路徑。
6. settings.json 不存在時回傳預設設定。
7. settings.json 存在時正確載入五個欄位。
8. settings 中路徑不存在時 RestoreSettings 不拋出未處理例外，畫面留白並提示。
9. Save 產生 camelCase JSON 且不含 Password 或 AMLRUN_PASSWORD。
10. 還原時按 database name/id 選取 DB。
11. 使用者選取路徑、DB 或修改 loginName 時呼叫 Save。
12. LoggingConfigurator 在測試暫存目錄建立 log 檔。
13. 執行含特殊明文密碼的產生流程後，log 不含該字串。
14. 所有 IsReadOnly TextBox binding 明確為 OneWay。
15. 既有完整測試維持通過。

## 10. README

README 更新：

- settings 實際位置與欄位。
- 密碼不保存。
- log 實際目錄、daily rolling 與安全限制。
- VaultConfigPath 推導規則。
- 啟動自動還原與失效路徑行為。
- 建置、執行與測試方式。
- 明確重申不執行 BAT、CMD、SQL，不測試 PLM 或 DB 連線。
