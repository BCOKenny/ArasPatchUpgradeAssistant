# Aras Innovator Patches 升級助手

`ArasPatchUpgradeAssistant` 是 .NET 8 WPF 桌面工具，用來輔助 Aras Innovator Patches 升級前的設定與檢查。

目前版本仍是前置輔助工具，不會執行任何 BAT / CMD / SQL 升級命令，也不會測試 PLM 或資料庫連線。

## 目前完成範圍

Wizard 目前包含：

1. 基本設定 / 產生 SETUP CMD
2. 升級目錄檢查
3. 升級選項 / BAT 執行計畫
4. 執行前檢查（尚未實作）
5. 執行升級命令（尚未實作）
6. Log / Report 檢視（尚未實作）

第 1 步完成並產生 `SETUP-DEFAULTS-{MachineName}.CMD` 後，才允許進入第 2 步與第 3 步。
第 3 步不需等待第 2 步完成，可直接依 SETUP CMD 所在的 commands 目錄掃描 BAT。

## 第 1 步：基本設定 / 產生 SETUP CMD

支援功能：

- 選取 `SETUP-DEFAULTS-MACHINENAME.CMD`。
- 從 SETUP CMD 路徑推導 Version、Command Folder、Upgrade Root、Support Root。
- Version 顯示值會從 Upgrade Root 最後一層資料夾名稱取簡短版，例如 `R38 (14.38.0)` 顯示為 `R38`。
- 選取 `InnovatorServerConfig.xml`。
- 從 `InnovatorServerConfig.xml` 所在目錄推導 `{AP Server Root}\VaultServer\vault.config`。
- 從 `vault.config` 讀取 `InnovatorServerUrl` 並產生 `AMLRUN_SERVERPREFIX`。
- 解析 `DB-Connection` 的 `id`、`database`、`server`。
- 顯示 SQL Server 與目標資料庫（`COPY_TARGET_DB_NAME`）。
- 可手動維護來源資料庫（`COPY_SOURCE_DB_NAME`）。
- 可輸入 Innovator login/password 與 SQL login/password。
- 產生 `SETUP-DEFAULTS-{MachineName}.CMD`，並保留原始註解、順序與 CRLF。

## UI 區塊

基本設定畫面區塊可展開 / 收合：

- SETUP CMD 與升級路徑
- Innovator Server 與登入資訊
- SQL Server 登入資訊
- 寫入預覽
- 產生結果 / 訊息

畫面上方提供：

- 全部展開
- 全部收合

## 資料庫欄位規則

資料庫連線下拉選單來自 `InnovatorServerConfig.xml` 的 `DB-Connection`。

欄位對應：

- SQL Server：目前選取 DB-Connection 的 `server`，唯讀。
- 目標資料庫（`COPY_TARGET_DB_NAME`）：目前選取 DB-Connection 的 `database`，唯讀。
- 來源資料庫（`COPY_SOURCE_DB_NAME`）：使用者可手動輸入與修改。

來源資料庫自動帶入規則：

- 若目前是空值，選取 DB-Connection 時會自動帶入該連線的 `database`。
- 若目前已有值，切換 DB-Connection 不會覆蓋。
- 來源資料庫會儲存到 settings.json。

## SETUP CMD 目前會更新的變數

路徑與 AML 相關：

- `TOOLS_FOLDER`
- `CONSOLEUPGRADE_FOLDER`
- `IMPORTS_FOLDER`
- `SOLUTIONS_FOLDER`
- `BACKUPS_FOLDER`
- `LOGS_FOLDER`
- `UPGRADE_DB_NAME`
- `INNOVATOR_SERVER_CONFIG`
- `AMLRUN_SERVERPREFIX`
- `AMLRUN_DATABASE`
- `AMLRUN_LOGINNAME`
- `AMLRUN_PASSWORD`
- `CORE_PRE_PATCHES`
- `CORE_PRE_CATALOG`
- `CORE_POST_PATCHES`
- `CORE_POST_CATALOG`
- `PE_PRE_PATCHES`
- `PE_PRE_CATALOG`
- `PE_POST_PATCHES`
- `PE_POST_CATALOG`

資料庫與 SQL 相關：

- `COPY_SOURCE_DB_NAME`
- `COPY_TARGET_DB_NAME`
- `SOURCE_DB_SERV`
- `TARGET_DB_SERV`
- `SOURCE_SA_USER`
- `TARGET_SA_USER`
- `SOURCE_SA_PASS`
- `TARGET_SA_PASS`

本版仍不會自動覆蓋：

- `SOURCE_DB_USER`
- `SOURCE_DB_PASS`
- `SOURCE_RE_USER`
- `SOURCE_RE_PASS`
- `TARGET_DB_USER`
- `TARGET_DB_PASS`

## 第 2 步：升級目錄檢查

支援功能：

- 顯示 Support Root、commands、tools、Patches、Solutions、LOGS、backup 與產生後 SETUP CMD 等檢查項目。
- Patches Base 會從 `{SupportRoot}\Patches` 實際往下偵測，不再用 Version 固定組出路徑。
- Solutions Base 會從 `{SupportRoot}\Solutions` 實際往下偵測 `.mf` 檔案位置。
- 資料夾存在顯示 `OK`，不存在顯示 `Missing`。
- Missing 的 Folder 可用資料夾圖示按鈕手動建立，建立後重新整理檢查結果。

## 第 3 步：升級選項 / BAT 執行計畫

目前支援功能：

- 從第 1 步完成資訊取得：
  - SETUP CMD 路徑
  - commands 目錄
  - Command Folder
  - Support Root
  - SETUP CMD `@SET` 變數解析結果
  - Patches Base
  - Solutions Base
- 顯示 commands 目錄路徑、Patches Base、Solutions Base、SETUP CMD 變數數量與 BAT 檔案數量。
- 掃描 commands 目錄下的 `.bat` / `.BAT` 檔案。
- BAT 清單顯示：
  - Checkbox
  - 順序
  - BAT 檔名
  - 類型
  - 狀態
  - 子項目數量
  - Catalog Path
  - 訊息
- 工具列提供：
  - 重新掃描
  - 全選
  - 全不選
  - 展開全部
  - 收合全部

注意：目前第 3 步只建立 BAT 執行計畫與預覽清單，不會執行任何 BAT / CMD / SQL 命令。

## settings.json

設定檔固定儲存在：

```text
%LocalAppData%\ArasPatchUpgradeAssistant\settings.json
```

會儲存：

- `setupDefaultsTemplatePath`
- `innovatorServerConfigPath`
- `selectedDatabaseId`
- `selectedDatabaseName`
- `copySourceDbName`
- `loginName`
- `encryptedPassword`
- `sqlLoginName`
- `encryptedSqlPassword`

密碼欄位使用 Windows DPAPI 加密：

- API：`System.Security.Cryptography.ProtectedData`
- Scope：`DataProtectionScope.CurrentUser`
- 格式：Base64 字串

settings.json 不會儲存密碼明文，Serilog 也不會記錄密碼明文。

## Log

Serilog rolling log 目錄：

```text
%LocalAppData%\ArasPatchUpgradeAssistant\logs
```

本工具會記錄基本操作事件，例如設定載入/儲存、版本名稱 normalization、`COPY_SOURCE_DB_NAME` 變更、自動帶入，以及區塊全部展開/收合。

## 使用方式

```powershell
dotnet restore ArasPatchUpgradeAssistant.sln
dotnet build ArasPatchUpgradeAssistant.sln -c Release --no-restore
dotnet run --project ArasPatchUpgradeAssistant
```

測試指令：

```powershell
dotnet test ArasPatchUpgradeAssistant.sln -c Release
```
