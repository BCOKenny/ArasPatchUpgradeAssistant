# ArasPatchUpgradeAssistant 00_Requirement_Handoff.md

> 文件定位：Chat → Work 的需求交接文件。不是最終實作規格；用來交給 Work 產生 `10_Spec.md`、`20_FunctionSpec.md`、`TestPlan.md` 與 Codex Prompt。

## 1. 專案
| 項目 | 內容 |
|---|---|
| 專案英文名稱 | `ArasPatchUpgradeAssistant` |
| 專案中文名稱 | Aras Innovator Patches 升級助手 |
| 類型 | Windows WPF Desktop Tool |
| 用途 | 輔助 Aras Innovator Patches / Support Commands 升級設定、目錄檢查、BAT 執行計畫、外部修正、Patch 說明與 AI 中文說明 |

## 2. Chat / Work / Codex 分工

### Chat
- 釐清需求、限制、例外與錯誤原因。
- 討論 Aras Patches、SETUP CMD、vault.config、DB-Connection、Catalog XML、Patch XML。
- 分析 API / Log / UI 問題。
- 產出本文件 `00_Requirement_Handoff.md`。

### Work
- 將需求轉成正式文件與可執行計畫。
- 產出 `10_Spec.md`、`20_FunctionSpec.md`、`TestPlan.md`、`ImplementationPlan.md`、`CodexPrompt.md`。
- 做 Requirement Gap Analysis，確認是否缺少驗收規則或邊界條件。
- Codex 完成後整理 ChangeLog、ImplementationReport、BuildAndTestResult、ReleaseNote。

### Codex
- 依 `AGENTS.md` 與正式規格小步驟實作。
- 一次只做一個小任務。
- 預設不建立 git repository / branch / worktree，不執行 `git commit`。
- 預設不執行 `dotnet test`，最多只執行一次 `dotnet build`。

## 3. 目前已確認功能範圍

Wizard 步驟：
1. 基本設定 / 產生 CMD
2. 升級目錄檢查
3. 升級選項 / BAT 執行計畫
4. 執行前檢查（後續）
5. 執行升級命令（後續）
6. Log / Report 檢視（後續）

目前核心：第 1、2、3 步、外部修正、Patch 說明、AI 中文說明、Patch 說明 ToolTip / 點擊預覽、批量產生全部 Patch 說明。

## 4. 第 1 步需求：基本設定 / 產生 CMD

- 選取 `SETUP-DEFAULTS-MACHINENAME.CMD`，產生 `SETUP-DEFAULTS-{MachineName}.CMD`。
- 推導 UpgradeRoot、SupportRoot、CommandFolder、Version。
- 選取 `InnovatorServerConfig.xml`。
- 推導 `{AP Server Root}\VaultServer\vault.config`。
- 解析 `vault.config` 的 `appSettings/add key="InnovatorServerUrl" value="..."`。
- 將 `/Server/InnovatorServer.aspx` 移除。
- host 為 `localhost`、`127.0.0.1`、`::1` 時改成本機電腦名稱。
- 解析 DB-Connection 的 id、database、server。
- 顯示 SQL Server、目標資料庫（COPY_TARGET_DB_NAME）、來源資料庫（COPY_SOURCE_DB_NAME）。
- COPY_TARGET_DB_NAME 來自 selected database。
- COPY_SOURCE_DB_NAME 可手動維護；空值時第一次選 DB 自動帶入。
- Innovator / SQL 密碼以 Windows DPAPI 加密保存。
- settings.json 儲存於 `%LocalAppData%\ArasPatchUpgradeAssistant\settings.json`。
- Support 相關 CMD 變數需依目前 SupportRoot remap。
- TARGET_IOM_DLL 需從 `{SupportRoot}\tools\SolutionUpgrade` 往下找 `IOM.dll`，優先 `consoleUpgrade`。

## 5. 第 2 步需求：升級目錄檢查

- 偵測 Patches Base，不硬寫版本號。
- 支援 `Support\Patches\core` / `Support\Patches\PE` 與 `Support\Patches\120\core` / `Support\Patches\120\PE`。
- 偵測 Solutions Base，不硬寫版本號。
- 支援 `Support\Solutions\core_imports.mf` 與 `Support\Solutions\120\core_imports.mf`。
- 顯示 OK / Missing / Warning。
- Missing Folder 顯示建立資料夾圖示，Tooltip 顯示「建立資料夾」。
- 建立完成後重新檢查。

## 6. 第 3 步需求：升級選項 / BAT 執行計畫

- 掃描 SETUP CMD 所在 commands 目錄。
- 只列符合 `^\d+-.+\.BAT$` 的數字前綴 BAT。
- 排除非數字 BAT，例如 `DBOBJCHOWN.BAT`、`EXPORT-UPGRADE.BAT`。
- 顯示 commands 目錄檔案數量與可執行升級 BAT。
- 判斷 BAT 類型：CORE PRE、CORE POST、PE PRE、PE POST、BAT。
- 以 PatchesBase 找 Catalog XML。
- 支援 `pre-patches.xml`、`post-patches.xml`、`pre_patches.xml`、`post_patches.xml`、`pre-patches.manifest.xml`、`post-patches.manifest.xml`。
- 解析 `<updates><update>` 的 UpNumber、Name、Order、Generation、SoftwareVersion、DbTargetVersion。
- 父階 BAT 可展開 / 收合。
- 子階 DataGrid 需有獨立 ScrollBar。
- 父子勾選需三態同步。
- 選取狀態與外部修正儲存至 `{SupportRoot}\UpgradeAssistant\{CommandFolder}\upgrade-plan.json`。

## 7. 外部修正檔案需求

- 支援右鍵插入目前所選項目前的修正。
- 支援刪除、上移、下移外部修正。
- 外部檔案複製至 `{SupportRoot}\UpgradeAssistant\{CommandFolder}\external-files`。
- 外部檔案命名建議：`yyyyMMddHHmmssfff-{原始檔名}`。
- 刪除外部修正只從計畫移除，不刪除實體檔案。
- 外部子項參與父階三態勾選。
- 下次開啟第 3 步需還原插入位置。

## 8. Patch 說明產生需求

### 官方 update
- 子項右鍵「產生 Patch 說明」。
- 依 BAT 類型找實體 Patch XML 目錄：
  - CORE PRE：`{PatchesBase}\core\pre`
  - CORE POST：`{PatchesBase}\core\post`
  - PE PRE：`{PatchesBase}\PE\pre`
  - PE POST：`{PatchesBase}\PE\post`
- Patch XML 檔名為 `{Name}.xml`。
- Markdown 儲存至 `{SupportRoot}\UpgradeAssistant\{CommandFolder}\patch-notes\{BatFileNameWithoutExtension}\{UpNumber}.md`。
- 找不到 Patch XML 時仍產生 Missing 說明，不中斷。
- 產生完成後，該 update 子項需立即更新說明狀態圖示、ToolTip、Markdown path、AI 狀態與 fallback 狀態，不要求使用者重新掃描或重新展開。

### 外部修正
- 不使用 `{Name}.xml` 尋找。
- 直接分析外部檔案本身。
- 依副檔名 `.sql`、`.xml`、`.aml`、`.cmd`、`.bat` 產生說明。
- 不應顯示 `Patch XML Missing`。

## 9. AI 中文說明需求

- WPF 工具接 OpenAI-compatible API。
- 產生中文說明、中文摘要、影響範圍、風險提示。
- 第一版只傳 Description 與少量 metadata；不傳完整 Patch XML / Body / SQL / CSharp / AML。
- 預設 SourceMode：`DescriptionOnly`。
- API Key 以 DPAPI 加密保存。
- AI 失敗 fallback 為「待人工確認」，且不得中斷 Markdown 產生。
- Log 需記錄安全診斷資訊：BaseUrl、Model、SourceMode、DescriptionChars、BodyIncluded、PromptChars、RequestJsonChars、HTTP status、error.message/type/code、x-request-id。
- 不得記錄 API Key、Authorization header、完整 request JSON、完整 Patch Body。

## 10. Patch 說明 ToolTip / 點擊預覽需求

- 子項 DataGrid 增加「說明」狀態欄。
- 未產生：空白或灰色圖示。
- 已產生：📝。
- AI fallback：⚠️。
- 滑鼠移到圖示時只顯示簡短 ToolTip，內容包含 Markdown 路徑、AI 狀態、說明來源、產生時間、fallback 原因。
- 點選圖示時顯示中文說明浮動提示卡，不只依賴 hover 自動彈出完整內容。
- 浮動提示卡內容包含中文說明、中文摘要、影響範圍、風險提示、Markdown 檔案路徑。
- 浮動提示卡動畫：點選後 Fade In，顯示約 10 秒，Fade Out 後自動關閉。
- 產生 Patch 說明後即時更新該子項狀態，至少包含說明狀態圖示、ToolTip、Markdown path、AI 狀態與 fallback 狀態。
- 重新載入第 3 步時，若 Markdown 已存在，需自動顯示已產生。

## 11. 產生全部 Patch 說明需求

- 第 3 步工具列在「展開全部 / 收合全部」旁新增「產生全部 Patch 說明」按鈕。
- 批量產生範圍為目前 PatchesBase 可掃描到的官方 update：
  - CORE PRE：`{PatchesBase}\core\pre-patches.xml`
  - CORE POST：`{PatchesBase}\core\post-patches.xml`
  - PE PRE：`{PatchesBase}\PE\pre-patches.xml`
  - PE POST：`{PatchesBase}\PE\post-patches.xml`
- 同時支援既有命名：`pre_patches.xml`、`post_patches.xml`、`pre-patches.manifest.xml`、`post-patches.manifest.xml`。
- 預設只產生尚未存在的 Patch 說明；已存在的 `.md` 預設跳過，避免重複花 API 費用。
- 未來若需要，可再加「強制重新產生」選項。
- 批量產生需逐筆執行，不平行大量呼叫 AI。
- 需顯示進度：目前第幾筆 / 總筆數 / 成功 / 跳過 / 失敗。
- AI 失敗時仍產生 fallback Markdown，不中斷整批。
- 每筆完成、跳過或 fallback 後，畫面上對應 update 子項需立即刷新說明狀態。

## 12. 安全限制

- 不自動執行 BAT / CMD / SQL / AML。
- 不執行 Patch XML Body。
- 不修改原廠 BAT / Catalog XML / Patch XML。
- 密碼與 API Key 不明文保存。
- Log 不記錄密碼或 API Key 明文。

## 13. 初步驗收

- 可產生 SETUP CMD。
- 可檢查升級目錄。
- 可掃描 BAT 與解析 Catalog update。
- 可插入外部修正並保存 upgrade-plan.json。
- 可產生官方 update 與外部修正的 Patch 說明。
- 產生 Patch 說明完成後，該 row 圖示、ToolTip、Markdown path、AI 狀態與 fallback 狀態立即更新。
- AI 啟用時可呼叫 API；失敗時 fallback。
- ToolTip 可顯示 Patch 說明狀態，點選圖示可顯示中文說明浮動提示卡。
- 可從第 3 步批量產生尚未存在的官方 Patch 說明，已存在 Markdown 會跳過，AI 失敗不會中斷整批。
- 不執行任何升級命令或 SQL。

## 14. 尚未確認

- 第 4 步「執行前檢查」是否優先實作。
- 是否新增「強制重新產生 Patch 說明」選項。
- 是否建立 TestPlan、ImplementationPlan 與 CodexPrompt。
