# ArasPatchUpgradeAssistant Spec.md

> 文件定位：Work 產出的正式專案規格書。描述背景、目標、範圍、資料來源、輸出位置、整體架構與高階驗收。功能細節請見 `FunctionSpec.md`。

## 1. 專案資訊

| 項目 | 內容 |
|---|---|
| 專案英文名稱 | `ArasPatchUpgradeAssistant` |
| 專案中文名稱 | Aras Innovator Patches 升級助手 |
| 類型 | Windows WPF Desktop Tool |
| 技術 | .NET 8、WPF、MVVM、CommunityToolkit.Mvvm、Serilog、System.Text.Json |
| 主要用途 | 升級前置設定、目錄檢查、BAT 執行計畫、外部修正、Patch 說明、AI 中文說明 |

## 2. 背景

Aras Innovator 升級常需操作 Support commands，例如 `Support\commands\Upgrade-12-38` 或 `Support\commands\Upgrade-110SP15-12`。commands 目錄包含 BAT、CMD、XML、SQL 等檔案，其中數字前綴 BAT 代表升級順序。傳統人工修改 SETUP CMD、檢查 Support 路徑、確認 Patches / Solutions、插入客製修正與追蹤 Patch 說明容易出錯，因此需要標準化工具。

## 3. 目標

1. 提供 Wizard 式升級助手。
2. 自動產生 `SETUP-DEFAULTS-{MachineName}.CMD`。
3. 解析 `InnovatorServerConfig.xml`、`vault.config`、DB-Connection。
4. Remap Support 相關路徑。
5. 檢查 Patches / Solutions 與必要目錄。
6. 掃描可執行升級 BAT。
7. 解析 Catalog XML update 子項。
8. 建立 BAT / update 執行計畫。
9. 支援外部修正 overlay。
10. 產生 Patch 說明 Markdown。
11. 支援 AI 產生中文說明。
12. 以 ToolTip 顯示 Patch 說明狀態。
13. 保存 settings、ai-settings、upgrade-plan。
14. 後續延伸執行前檢查、執行升級、Log / Report。

## 4. Chat / Work / Codex 分工

- Chat：需求釐清、問題分析、決策整理，輸出 `Requirement.md`。
- Work：正式規格、測試計畫、實作計畫與 Codex Prompt，輸出 `Spec.md`、`FunctionSpec.md` 等。
- Codex：依文件小範圍實作，遵守 `AGENTS.md`，完成後回報修改與驗證結果。

## 5. 範圍

### 目前範圍
- 第 1 步：基本設定 / 產生 CMD
- 第 2 步：升級目錄檢查
- 第 3 步：升級選項 / BAT 執行計畫
- 外部修正檔案插入
- Patch 說明產生
- AI Patch 中文說明
- Patch 說明狀態 ToolTip

### 後續範圍
- 第 4 步：執行前檢查
- 第 5 步：執行升級命令
- 第 6 步：Log / Report 檢視

### 非目標
- 不作為完整自動升級引擎。
- 不取代工程師升級判斷。
- 不直接修改原廠 BAT / Catalog XML / Patch XML。
- 不涵蓋 Package 差異分析或 CoreTree 比對。

## 6. 使用者角色

| 角色 | 說明 |
|---|---|
| 升級工程師 | 設定 commands、確認 BAT、產生 Patch 說明、插入修正 |
| 技術負責人 | 檢查計畫、Patch 說明、風險 |
| 客戶環境導入人員 | 依確認後計畫在正式環境操作 |
| 維護工程師 | 依 Log / Report 追蹤問題 |

## 7. 工具流程

```text
選取 SETUP CMD 範本
 → 推導 UpgradeRoot / SupportRoot / CommandFolder
 → 選取 InnovatorServerConfig.xml
 → 解析 vault.config / DB-Connection
 → 設定 Innovator / SQL / AI
 → 產生 SETUP CMD
 → 檢查升級目錄
 → 掃描 BAT
 → 解析 Catalog update
 → 勾選計畫 / 插入外部修正
 → 產生 Patch 說明
 → 可選 AI 中文說明
 → 顯示 ToolTip 狀態
 → 保存 settings / ai-settings / upgrade-plan
```

## 8. 主要資料來源

| 資料來源 | 用途 |
|---|---|
| `SETUP-DEFAULTS-MACHINENAME.CMD` | SETUP CMD 範本 |
| `InnovatorServerConfig.xml` | DB-Connection、SQL Server、Database |
| `VaultServer\vault.config` | InnovatorServerUrl |
| `Support\commands\<CommandFolder>` | BAT / CMD / XML / SQL 掃描 |
| `Support\Patches` | PatchesBase、Catalog XML、Patch XML |
| `Support\Solutions` | SolutionsBase |
| `settings.json` | 本機使用者設定 |
| `ai-settings.json` | AI 設定 |
| `upgrade-plan.json` | 升級計畫與外部修正 |
| `patch-notes/` | Patch 說明 Markdown |
| Serilog Log | 操作與診斷紀錄 |

## 9. 主要輸出位置

本機使用者設定：

```text
%LocalAppData%\ArasPatchUpgradeAssistant\settings.json
%LocalAppData%\ArasPatchUpgradeAssistant\ai-settings.json
%LocalAppData%\ArasPatchUpgradeAssistant\logs\
```

升級包內輸出：

```text
{SupportRoot}\UpgradeAssistant\{CommandFolder}\upgrade-plan.json
{SupportRoot}\UpgradeAssistant\{CommandFolder}\external-files\
{SupportRoot}\UpgradeAssistant\{CommandFolder}\patch-notes\
```

文件建議位置：

```text
docs/Requirement.md
docs/Spec.md
docs/FunctionSpec.md
docs/TestPlan.md
docs/ImplementationPlan.md
prompts/CodexPrompt.md
```

若採流水號流程：

```text
docs/00_Requirement_Handoff.md
docs/10_Spec.md
docs/20_FunctionSpec.md
docs/30_TestPlan.md
docs/40_ImplementationPlan.md
prompts/50_CodexPrompt.md
```

## 10. 核心設計原則

- 不修改原廠 BAT / Catalog XML / Patch XML。
- 外部修正採 overlay。
- 密碼與 API Key 使用 DPAPI。
- AI 失敗不影響 Markdown 產生。
- Log 不記錄敏感資訊。
- 第 5 步未實作前不執行升級命令。
- Codex 一次只做小範圍修改。

## 11. 技術架構

| 項目 | 規格 |
|---|---|
| UI | WPF |
| 架構 | MVVM |
| XML | XDocument |
| JSON | System.Text.Json |
| Log | Serilog |
| 安全 | Windows DPAPI |
| AI | OpenAI-compatible `/v1/chat/completions` |
| 主要控制項 | DataGrid、RowDetails、ContextMenu、ToolTip、Expander、PasswordBox |

## 12. 建議目錄

```text
ArasPatchUpgradeAssistant/
├─ AGENTS.md
├─ README.md
├─ docs/
│  ├─ Requirement.md
│  ├─ Spec.md
│  ├─ FunctionSpec.md
│  ├─ TestPlan.md
│  ├─ ImplementationPlan.md
│  └─ ReleaseNote.md
├─ prompts/
│  └─ CodexPrompt.md
├─ src/
├─ tests/
└─ samples/
```

## 13. 高階驗收

- 可產生 SETUP CMD。
- 可解析 vault.config appSettings。
- 可解析 DB-Connection。
- 可保存與還原設定。
- 可偵測 R38 / 12SP18 Patches 與 Solutions。
- 可列出數字前綴 BAT。
- 可解析 Catalog update。
- 可插入外部修正。
- 可產生 Patch 說明。
- AI 啟用時可產生中文說明，失敗時 fallback。
- ToolTip 可顯示 Patch 說明狀態。
- 不執行 BAT / CMD / SQL / AML。
- 不記錄密碼或 API Key。
