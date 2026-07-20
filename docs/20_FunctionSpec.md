# ArasPatchUpgradeAssistant FunctionSpec.md

> 文件定位：功能細項規格與 Codex 實作依據。Codex 每次只讀本次任務相關章節，不應一次實作整份文件。

## 1. 通用技術規格

| 項目 | 規格 |
|---|---|
| 程式 | WPF Desktop |
| Framework | .NET 8 / `net8.0-windows` |
| 架構 | MVVM |
| MVVM | CommunityToolkit.Mvvm |
| XML | XDocument / LINQ to XML |
| JSON | System.Text.Json |
| Log | Serilog |
| 加密 | Windows DPAPI，CurrentUser |

## 2. 安全限制

- 不自動執行 BAT / CMD / SQL / AML。
- 不執行 Patch XML Body。
- 不修改原廠 BAT / Catalog XML / Patch XML。
- 不記錄密碼、API Key、Authorization header、完整 Patch Body、完整 OpenAI request JSON。
- AI 失敗不得導致 Markdown 產生失敗。

## 3. 第 1 步：基本設定 / 產生 CMD

### 3.1 欄位
| 欄位 | 類型 | 說明 |
|---|---|---|
| SetupDefaultsTemplatePath | File | `SETUP-DEFAULTS-MACHINENAME.CMD` |
| InnovatorServerConfigPath | File | `InnovatorServerConfig.xml` |
| SelectedDatabase | ComboBox | DB-Connection |
| LoginName / Password | Text / PasswordBox | Innovator 帳密 |
| SqlLoginName / SqlPassword | Text / PasswordBox | SQL 帳密 |
| CopySourceDbName | Text | 來源資料庫 |
| CopyTargetDbName | Readonly | 目標資料庫 |

### 3.2 路徑解析
格式：

```text
{UpgradeRoot}\Support\commands\{CommandFolder}\SETUP-DEFAULTS-MACHINENAME.CMD
```

推導：CommandFolder、SupportRoot、UpgradeRoot、Version。Version 從 UpgradeRoot 最後一層依空白、`(`、`_`、`-` 分割取第 0 個 token。

### 3.3 vault.config
位置：

```text
{AP Server Root}\VaultServer\vault.config
```

支援：

```xml
<add key="InnovatorServerUrl" value="http://localhost/APV8/Server/InnovatorServer.aspx" />
```

規則：
- 使用 LocalName，忽略 namespace。
- add/key/value 大小寫不敏感。
- 移除 `/Server/InnovatorServer.aspx` 與尾端 `/`。
- localhost / 127.0.0.1 / ::1 改成本機名稱。
- 保留 protocol、port、path。

### 3.4 DB-Connection
解析 attribute：`id`、`database`、`server`。無 id 或 database 不列入；server 缺少時不可中斷。

### 3.5 COPY DB
- COPY_TARGET_DB_NAME = selected database。
- COPY_SOURCE_DB_NAME 空白且第一次選 DB 時自動帶入。
- 使用者手動修改後切換 DB 不覆蓋。
- 需保存至 settings。

### 3.6 settings.json
位置：

```text
%LocalAppData%\ArasPatchUpgradeAssistant\settings.json
```

欄位：

```json
{
  "setupDefaultsTemplatePath": "",
  "innovatorServerConfigPath": "",
  "selectedDatabaseId": "",
  "selectedDatabaseName": "",
  "copySourceDbName": "",
  "loginName": "root",
  "encryptedPassword": "",
  "sqlLoginName": "sa",
  "encryptedSqlPassword": ""
}
```

啟動還原時需重新執行解析流程，不可只回填 TextBox。

### 3.7 Support Path Remap
支援 TOOLS_FOLDER、CONSOLEUPGRADE_FOLDER、TARGET_IOM_DLL、PATCHES_FOLDER、SOLUTIONS_FOLDER、OLD_SOLUTIONS_FOLDER、IMPORTS_FOLDER、ES_UPGRADE_FOLDER、BACKUPS_FOLDER、LOGS_FOLDER、LOG_TRUNCATE_DEST、MS_DTS_LOG_DIR、UPDATES_FOLDER、UPDATES_CATALOG、POST_UPDATES_CATALOG、PLM_POST_PATCHES、PLM_POST_CATALOG、PROJECT_POST_PATCHES、PROJECT_POST_CATALOG、CORE_PRE_PATCHES、CORE_PRE_CATALOG、CORE_POST_PATCHES、CORE_POST_CATALOG、PE_PRE_PATCHES、PE_PRE_CATALOG、PE_POST_PATCHES、PE_POST_CATALOG。

Remap 保留 Support 後相對路徑，前接目前 SupportRoot。

### 3.8 TARGET_IOM_DLL
從 `{SupportRoot}\tools\SolutionUpgrade` 遞迴找 `IOM.dll`，優先：
1. `consoleUpgrade\IOM.dll`
2. `Import\IOM.dll`
3. 其他第一個

找不到時 fallback 一般 remap 並 Warning。

## 4. 第 2 步：升級目錄檢查

### Patches Base
起點 `{SupportRoot}\Patches`。直接子目錄含 core 或 PE 即為候選。優先同時有 core/PE、有 core、層級較淺、路徑排序。

### Solutions Base
起點 `{SupportRoot}\Solutions`。直接含任一 `.mf` 為候選。優先 `core_imports.mf`、其他 `.mf`、層級較淺、路徑排序。

### Missing Folder
Missing Folder 顯示建立資料夾圖示，Tooltip「建立資料夾」。File 項目不可建立。

## 5. 第 3 步：BAT 執行計畫

### BAT 篩選
只列：

```regex
^\d+-.+\.BAT$
```

依數字前綴升冪。

### BAT 類型
CORE+PRE → CORE PRE；CORE+POST → CORE POST；PE+PRE → PE PRE；PE+POST → PE POST；其他 BAT。

### Catalog XML
依 PatchesBase 尋找 pre/post XML，支援 `pre-patches.xml`、`pre_patches.xml`、manifest、post variants。

### Catalog update
解析 UpNumber、Name、Order、Generation、SoftwareVersion、DbTargetVersion、Source=Official、Status。

### UI
- 父階 RowDetails 綁定 IsExpanded。
- 子階 DataGrid 有獨立 ScrollBar，MaxHeight 建議 320~360。
- 父子三態勾選：true / false / null，外部子項也參與。

## 6. 外部修正

- 右鍵插入目前項目前修正。
- 只允許外部項目刪除 / 上移 / 下移。
- 儲存至 `{SupportRoot}\UpgradeAssistant\{CommandFolder}\external-files`。
- 定義檔 `{SupportRoot}\UpgradeAssistant\{CommandFolder}\upgrade-plan.json`。
- 刪除只移除 plan，不刪實體檔。
- 上移 / 下移不可跨 BAT。
- 重新載入找不到 insert target 時放同層最後並 Warning。

## 7. Patch 說明產生

### 7.1 輸出
官方 update：

```text
{SupportRoot}\UpgradeAssistant\{CommandFolder}\patch-notes\{BatFileNameWithoutExtension}\{UpNumber}.md
```

### 7.2 官方 update 找 Patch XML
- CORE PRE → `{PatchesBase}\core\pre`
- CORE POST → `{PatchesBase}\core\post`
- PE PRE → `{PatchesBase}\PE\pre`
- PE POST → `{PatchesBase}\PE\post`

Patch XML：`{Name}.xml`。

### 7.3 Patch XML 解析
解析 Description、Type、Body、Data、是否有 SQL / AML / CSharp、Body 行數、SQL 動作與關鍵字。不可執行 Body。

### 7.4 外部修正說明
Source=External 時：
- 不找 `{Name}.xml`。
- 直接分析 external stored file。
- Markdown 顯示外部修正、原始路徑、保存路徑、分析狀態。
- 不顯示 Patch XML Missing。

### 7.5 Markdown 區塊
包含 Patch 基本資訊、原始 Description、中文說明、中文摘要、影響範圍、風險提示、Patch XML 解析資訊、靜態分析摘要、AI 產生狀態。

## 8. AI Patch 中文說明

### 8.1 ai-settings.json
位置：

```text
%LocalAppData%\ArasPatchUpgradeAssistant\ai-settings.json
```

欄位：

```json
{
  "enableAiPatchDescription": false,
  "openAiBaseUrl": "https://api.openai.com/v1",
  "openAiModel": "gpt-4.1-mini",
  "encryptedOpenAiApiKey": "",
  "requestTimeoutSeconds": 60,
  "maxBodyPreviewLines": 80,
  "enableAiRequestDebugLog": false,
  "sourceMode": "DescriptionOnly"
}
```

### 8.2 SourceMode
第一版預設 `DescriptionOnly`。只傳 UpNumber、Name、Type、Original Description、BAT 類型、Order、Generation、SoftwareVersion、DbTargetVersion。不傳完整 XML / Body / CSharp / SQL / AML。

### 8.3 Prompt 原則
請 AI 以繁體中文產生中文說明、摘要、影響範圍、風險提示；不可編造未提供的 Body 細節；不確定需標示待工程師確認。

### 8.4 fallback
AI 未啟用、設定不完整、HTTP 失敗、timeout、格式錯誤時，中文說明 / 摘要 / 影響範圍 / 風險提示顯示「待人工確認」，Markdown 照常產生。

### 8.5 AI Log
Request 前記錄 BaseUrl、Model、Endpoint、EnableAiPatchDescription、SourceMode、UpNumber、Name、DescriptionChars、BodyChars、BodyIncluded、BodyPreviewLines、PromptChars、RequestJsonChars、MaxTokens、Temperature。

失敗時記錄 status code、error.message、error.type、error.param、error.code、x-request-id、retry-after、x-ratelimit headers。不得記錄 API Key、Authorization、完整 JSON、完整 Body。

Debug Log 預設 false；true 時 prompt preview 最多 1500 字。

## 9. Patch 說明 ToolTip

子項 ViewModel 建議欄位：HasPatchNote、PatchNotePath、PatchNoteGeneratedAt、PatchNoteAiStatus、PatchNoteSourceMode、PatchNoteErrorMessage、PatchNoteIcon、PatchNoteToolTip。

圖示：
- 未產生：空白 / 灰色
- 已產生：📝
- AI fallback / error：⚠️
- 產生失敗：❌

重新載入第 3 步時，檢查對應 Markdown 是否存在，若存在即顯示已產生。

## 10. Serilog

位置：

```text
%LocalAppData%\ArasPatchUpgradeAssistant\logs\
```

需記錄 Application started/exited、settings、ai-settings、vault、DB、SETUP CMD、Patches/Solutions、BAT scan、Catalog、upgrade-plan、external item、Patch note、AI request、Tooltip status。

## 11. AGENTS.md 需求

AGENTS.md 應指定 canonical docs：
- `docs/Requirement.md`
- `docs/Spec.md`
- `docs/FunctionSpec.md`

並說明 `docs/superpowers/plans`、`docs/superpowers/specs` 為歷史 / task-level 參考，不是最新 canonical 文件，除非任務明確指定。

## 12. 驗收

- 第 1 步可產生 SETUP CMD 並保存設定。
- 第 2 步可偵測 Patches/Solutions 並建立 Missing Folder。
- 第 3 步只列數字 BAT，Catalog 解析與三態勾選正確。
- 外部修正可插入、刪除、移動、保存、還原。
- 官方 update 與外部修正可產生 Patch 說明。
- AI DescriptionOnly 時 BodyIncluded=false。
- API 失敗有安全 Log 並 fallback。
- Tooltip 可顯示 Patch 說明狀態。
