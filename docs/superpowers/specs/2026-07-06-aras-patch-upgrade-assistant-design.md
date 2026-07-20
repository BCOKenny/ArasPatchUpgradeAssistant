# Aras Innovator Patches 升級助手：第 1、2 步設計規格

## 1. 目標與範圍

建立可編譯執行的 .NET 8 WPF 桌面工具 `ArasPatchUpgradeAssistant`，中文名稱為「Aras Innovator Patches 升級助手」。

第一版實作 Wizard 第 1 步「基本設定 / 產生 SETUP CMD」與第 2 步「升級目錄檢查」。

第 1 步：

- 選取並解析 `SETUP-DEFAULTS-MACHINENAME.CMD` 路徑。
- 選取並解析 `InnovatorServerConfig.xml`。
- 從 `VaultServer\vault.config` 取得 `InnovatorServerUrl`。
- 選擇資料庫、輸入登入名稱與密碼。
- 預覽將寫入的設定。
- 複製來源 CMD，建立 `SETUP-DEFAULTS-{MachineName}.CMD`。
- 只更新指定的 `@SET` 變數，並顯示更新摘要。

第 2 步：

- 第 1 步完成後才允許進入。
- 顯示由 SETUP CMD 路徑推導出的 12 個重要 Folder 與產生後的 1 個 SETUP CMD File。
- 以快照方式檢查存在性與目錄內容，並提供手動重新整理。
- 只在使用者點擊並確認後建立缺少的 Folder，不自動建立任何目錄。

第一版明確不執行 CMD、BAT 或 SQL，不測試 PLM 連線，也不測試資料庫連線。

## 2. Solution 與分層

Solution 包含兩個專案：

1. `ArasPatchUpgradeAssistant`
   - 單一正式 WPF 專案，目標框架為 `net8.0-windows`。
   - 使用 CommunityToolkit.Mvvm。
   - 依責任分為 `Views`、`ViewModels`、`Models`、`Services`、`Helpers`。
2. `ArasPatchUpgradeAssistant.Tests`
   - xUnit 測試專案。
   - 直接參考 WPF 專案，測試不依賴 UI 的服務、模型與 Helper。

主要責任：

- `Views`：MainWindow、第 1 步與第 2 步畫面，僅負責呈現與必要的 WPF binding glue。
- `ViewModels`：畫面狀態、命令、驗證流程與服務協調。
- `Models`：升級路徑資訊、資料庫連線選項、CMD 變更項目、產生結果與目錄檢查項目。
- `Services`：路徑推導、XML 解析、CMD 內容更新與檔案產生、目錄驗證、檔案對話框及訊息對話框。
- `Helpers`：PasswordBox binding 等 WPF 輔助功能。

## 3. Wizard 與 UI

主畫面採左側步驟導覽、右側步驟內容：

1. 基本設定 / 產生 SETUP CMD
2. 升級目錄檢查
3. Patches / Catalog 檢查
4. 執行前檢查
5. 執行升級命令
6. Log / Report 檢視

第 1 步完成前，點擊第 2 至第 6 步會顯示「請先完成基本設定」。第 1 步完成後可進入第 2 步；點擊第 3 至第 6 步會顯示「此步驟尚未在第一版實作」。

第 1 步包含：

- SETUP CMD 路徑與瀏覽按鈕。
- 推導出的版本、命令資料夾、Upgrade Root、Support Root 與參考路徑。
- `InnovatorServerConfig.xml` 路徑與瀏覽按鈕。
- AP Server Root 與 Web URL。
- 以 DB-Connection `id` 為顯示文字、`database` 為實際值的 ComboBox。
- 預設 `root` 的使用者名稱。
- 預設 `innovator` 的密碼；畫面以 PasswordBox 呈現，摘要遮罩密碼。
- 產生前設定預覽。
- 確認 / 產生按鈕。
- 產生後路徑與「更新／新增」摘要。

第 2 步包含：

- Upgrade Root、版本與最後檢查時間。
- `OK`、`Missing`、`Warning` 數量摘要。
- 13 項檢查結果表格。
- 手動「重新整理」按鈕。
- 每個缺少 Folder 各自的「建立資料夾」按鈕。

## 4. 操作與資料流

### 4.1 選取 SETUP CMD

1. 使用 OpenFileDialog 選取檔案。
2. 驗證檔案存在。
3. 驗證檔名大小寫不敏感地等於 `SETUP-DEFAULTS-MACHINENAME.CMD`。
4. 從 `Support\commands\<Command Folder>` 結構推導：
   - Command Folder：CMD 所在資料夾名稱。
   - Support Root：路徑中的 `Support` 目錄。
   - Upgrade Root：Support Root 的父目錄。
   - Version：Upgrade Root 的目錄名稱，例如 `12SP18`。
5. Version 必須符合 `<major>SP<service-pack>`；`12SP18` 的 Solutions/Patches 版本資料夾為 `120`。
6. 建立所有由 Upgrade Root 與 Support Root 派生的 CMD 變數預覽。

### 4.2 選取 InnovatorServerConfig.xml

1. 驗證檔案存在。
2. AP Server Root 為 `InnovatorServerConfig.xml` 的所在目錄。
3. 解析所有 `DB-Connection`：
   - Label：`id` attribute。
   - Value：`database` attribute。
4. 若只有一個有效連線則自動選取第一個；多個連線仍預設第一個並允許使用者切換。
5. 從 `{AP Server Root}\VaultServer\vault.config` 解析 `InnovatorServerUrl`。
6. URL 可來自節點文字或 `value` attribute。
7. 將 `/Server/InnovatorServer.aspx` 尾碼與尾端斜線移除，得到 `AMLRUN_SERVERPREFIX`。例如：
   - 輸入：`http://localhost/InnovatorServer/Server/InnovatorServer.aspx`
   - 輸出：`http://localhost/InnovatorServer`

### 4.3 產生 CMD

1. 集中驗證所有必要輸入與推導結果。
2. 取得 `Environment.MachineName`。
3. 在來源檔相同目錄建立 `SETUP-DEFAULTS-{MachineName}.CMD`。
4. 若目標已存在，顯示覆蓋確認：
   - 確認：覆蓋目標檔。
   - 取消：不進行任何寫入。
5. 來源檔永遠不被覆蓋。
6. 成功後將第 1 步標記完成，顯示目標路徑及逐項變更摘要。

## 5. 升級目錄檢查

### 5.1 檢查方式

`DirectoryValidationService` 接收第 1 步產生的 `UpgradePathInfo` 與新 CMD 路徑，建立一次性的檢查結果快照。服務不使用 `FileSystemWatcher`。

在以下時機重新檢查全部項目：

- 使用者進入第 2 步。
- 使用者點擊「重新整理」。
- 使用者成功建立任一缺少的 Folder。
- 使用者回到第 1 步重新產生 CMD，之後再次進入第 2 步。

每個 `DirectoryValidationItem` 包含：

- 名稱。
- 類型：`Folder` 或 `File`。
- 完整路徑。
- 是否存在。
- 狀態：`OK`、`Missing` 或 `Warning`。
- 錯誤說明；正常時為空。
- 是否允許顯示建立按鈕。

### 5.2 固定檢查項目

依下列順序回傳 13 項：

| 名稱 | 類型 | 路徑 |
| --- | --- | --- |
| Support Root | Folder | `{Support Root}` |
| commands | Folder | `{Support Root}\commands` |
| DBUpdateTool | Folder | `{Support Root}\tools\DBUpdateTool` |
| consoleUpgrade | Folder | `{Support Root}\tools\SolutionUpgrade\consoleUpgrade` |
| Patches | Folder | `{Support Root}\Patches\{Version Code}` |
| Core Pre Patches | Folder | `{Support Root}\Patches\{Version Code}\core\pre` |
| Core Post Patches | Folder | `{Support Root}\Patches\{Version Code}\core\post` |
| PE Pre Patches | Folder | `{Support Root}\Patches\{Version Code}\PE\pre` |
| PE Post Patches | Folder | `{Support Root}\Patches\{Version Code}\PE\post` |
| Solutions | Folder | `{Support Root}\Solutions\{Version Code}` |
| LOGS | Folder | `{Support Root}\LOGS` |
| backup | Folder | `{Support Root}\backup` |
| Generated SETUP CMD | File | 第 1 步產生的 `SETUP-DEFAULTS-{MachineName}.CMD` |

### 5.3 狀態規則

- 路徑不存在：`Missing`。
- 一般 Folder 存在但不含任何檔案或子目錄：`Warning`。
- `LOGS` 與 `backup` 存在即為 `OK`，允許為空。
- 其他 Folder 存在且非空：`OK`。
- Generated SETUP CMD 存在：`OK`；不存在：`Missing`。
- 檢查單一項目時發生未授權、I/O 或無效路徑錯誤：該項為 `Warning`，保留完整路徑並顯示繁體中文錯誤說明，其他項目繼續檢查。

### 5.4 建立缺少的 Folder

- 所有狀態為 `Missing` 的 Folder 都顯示「建立資料夾」按鈕。
- File 永遠不顯示建立按鈕。
- 點擊後顯示包含完整路徑的確認訊息。
- 使用者確認後，服務再次驗證項目類型為 Folder，再呼叫目錄建立功能。
- 若目錄已由外部程式建立，不視為錯誤，直接重新整理。
- 權限不足、無效路徑或 I/O 錯誤以繁體中文顯示，不影響其他項目。
- 成功建立後自動重新整理全部 13 項。

## 6. 路徑與變數規則

以下 `Support Root` 以 `K:\10.Upgrades\12SP18\Support`、版本資料夾代碼以 `120` 示意：

| 變數 | 值 |
| --- | --- |
| `TOOLS_FOLDER` | `{Support Root}\tools\DBUpdateTool` |
| `CONSOLEUPGRADE_FOLDER` | `{Support Root}\tools\SolutionUpgrade\consoleUpgrade` |
| `IMPORTS_FOLDER` | `{Support Root}\Solutions\120` |
| `SOLUTIONS_FOLDER` | `{Support Root}\Solutions\120` |
| `BACKUPS_FOLDER` | `{Support Root}\backup` |
| `LOGS_FOLDER` | `{Support Root}\LOGS\DBUPDATE` |
| `UPGRADE_DB_NAME` | 使用者所選 DB-Connection 的 `database` |
| `INNOVATOR_SERVER_CONFIG` | 所選 `InnovatorServerConfig.xml` 完整路徑 |
| `AMLRUN_SERVERPREFIX` | 轉換後的 Web URL |
| `AMLRUN_DATABASE` | `%UPGRADE_DB_NAME%` |
| `AMLRUN_LOGINNAME` | 使用者名稱，預設 `root` |
| `AMLRUN_PASSWORD` | 密碼，預設 `innovator` |
| `CORE_PRE_PATCHES` | `{Support Root}\Patches\120\core\pre` |
| `CORE_PRE_CATALOG` | `{Support Root}\Patches\120\core\pre-patches.xml` |
| `CORE_POST_PATCHES` | `{Support Root}\Patches\120\core\post` |
| `CORE_POST_CATALOG` | `{Support Root}\Patches\120\core\post-patches.xml` |
| `PE_PRE_PATCHES` | `{Support Root}\Patches\120\PE\pre` |
| `PE_PRE_CATALOG` | `{Support Root}\Patches\120\PE\pre_patches.xml` |
| `PE_POST_PATCHES` | `{Support Root}\Patches\120\PE\post` |
| `PE_POST_CATALOG` | `{Support Root}\Patches\120\PE\post_patches.xml` |

## 7. CMD 更新規則

- 大小寫不敏感地比對指定變數。
- 支援 `@SET NAME=value` 與 `@SET "NAME=value"`。
- 已存在的變數只替換等號後的值，保留原行位置、原有前綴格式、註解、順序與所有非目標行。
- 必要變數不存在時，在檔案末端追加 `@SET NAME=value`，摘要標示「新增」。
- 已存在的變數摘要標示「更新」，並記錄舊值與新值。
- 所有輸出使用 CRLF。
- 保留來源編碼與 BOM；來源無 BOM 時使用 Windows 目前 ANSI code page。

## 8. 錯誤處理

下列情況以繁體中文顯示可操作的錯誤訊息，且不寫入目標檔：

- SETUP CMD 不存在。
- 檔名不是 `SETUP-DEFAULTS-MACHINENAME.CMD`。
- 找不到 `Support\commands` 結構或無法推導 Upgrade Root。
- 版本目錄名稱無法轉成 Solutions/Patches 版本資料夾代碼。
- `InnovatorServerConfig.xml` 不存在。
- `vault.config` 不存在。
- 找不到有效的 `InnovatorServerUrl`。
- 找不到含 `id` 與 `database` 的 DB-Connection。
- 尚未選取資料庫。
- 目標 CMD 已存在但使用者取消覆蓋。
- 檔案存取、XML 格式或編碼錯誤。
- 升級目錄檢查時無法讀取特定路徑。
- 建立資料夾時權限不足、路徑無效或發生 I/O 錯誤。

若目標檔寫入失敗，錯誤會回報給使用者，不將第 1 步標記為完成。

## 9. 測試策略

以 xUnit 測試真實服務邏輯，不啟動 WPF 視窗。最低測試範圍：

1. 從範例 SETUP CMD 路徑推導 Upgrade Root、Support Root、Version 與 Command Folder。
2. 錯誤檔名及無法推導根目錄時回報錯誤。
3. 從 `InnovatorServerConfig.xml` 解析一筆與多筆 DB-Connection。
4. DB-Connection 缺少必要 attribute 時排除，完全無有效項目時回報錯誤。
5. 從 `vault.config` 的節點文字與 `value` attribute 解析 `InnovatorServerUrl`。
6. 將 InnovatorServerUrl 轉為不含 `/Server/InnovatorServer.aspx` 與尾端斜線的 Web URL。
7. 更新 CMD 中兩種受支援的 `@SET` 格式。
8. 保留非目標行、註解、順序與 CRLF。
9. 缺少必要變數時追加並標示「新增」。
10. 由 machine name 產生 `SETUP-DEFAULTS-{MachineName}.CMD`。
11. 來源與目標路徑不同，且來源檔不會被改寫。
12. DirectoryValidationService 依固定順序回傳 12 個 Folder 與 1 個 File。
13. 所有檢查項目的推導路徑正確。
14. Folder 與 File 存在、不存在時分別得到正確狀態。
15. 一般空 Folder 為 `Warning`，空的 `LOGS` 與 `backup` 為 `OK`。
16. 單一項目無法存取時為 `Warning`，且不妨礙其他項目完成檢查。
17. 可建立缺少的 Folder，已存在時保持成功。
18. 建立資料夾流程拒絕 File 項目。
19. 第 2 步 ViewModel 可重新整理，並在建立 Folder 後重新檢查。
20. 第 1 步未完成時阻擋第 2 步導覽，第 1 步完成後允許進入。

## 10. README 與完成條件

README 說明：

- 工具目的。
- 第一版支援第 1、2 步。
- 明確列出不執行升級、不測試 PLM/DB 連線。
- 環境需求、建置、執行及測試指令。
- 使用流程與覆蓋行為。
- 第 2 步採快照檢查，使用者可手動重新整理。
- 目錄不會自動建立；只有使用者按下並確認「建立資料夾」才會寫入檔案系統。
- 專案結構及後續 Wizard 步驟狀態。

完成條件：

- Solution 可在已安裝 .NET 8 Windows Desktop Runtime 的 Windows 環境建置與啟動。
- 所有自動化測試通過。
- 第 1 步能從有效輸入產生機器專屬 CMD。
- 第 1 步完成前無法進入第 2 步，完成後可查看 13 項目錄與檔案檢查結果。
- 第 2 步可手動重新整理，並可在使用者明確確認後建立缺少的 Folder。
- 產生內容符合本規格的變數、格式、換行與摘要要求。
- 程式內沒有執行 CMD、BAT、SQL 或測試外部連線的功能。
