---
name: codex-task-prompt
description: Create a small, safe Codex task prompt from a FunctionSpec, bug report, UI issue, or implementation request. Use when the user wants a Codex-ready prompt for one bounded task.
---

# Codex Task Prompt Skill / Codex 任務 Prompt 技能

## Goal / 目的

Create a small, safe, executable Codex prompt for one task only.  
產生一份「一次只做一件事」的小範圍 Codex Prompt。

The prompt should help Codex implement or fix a clearly bounded feature without changing unrelated areas, running unnecessary commands, or consuming excessive time.  
Prompt 應協助 Codex 只處理明確範圍內的功能或 bug，避免改到無關功能、執行不必要命令，或消耗過多時間。

---

## When To Use / 何時使用

Use this skill when the user asks for:

- 依 FunctionSpec 產生 Codex Prompt
- 產生給 Codex 執行的小 Prompt
- 將需求拆成 Codex 可執行任務
- 依 bug、截圖、UI 問題產生 Codex Prompt
- 將 `_FunctionSpec.md` 的某章節轉成實作 Prompt
- 將大規格拆成分階段 Prompt

---

## Required Inputs / 建議輸入資訊

When possible, identify these items:

1. Project name / 專案名稱
2. FunctionSpec path, if available / FunctionSpec 路徑，如有
3. Target feature, bug, or section / 目標功能、bug 或章節
4. Current behavior / 目前狀況
5. Expected behavior / 預期結果
6. Allowed files or areas / 允許修改範圍
7. Forbidden files or areas / 不可修改範圍
8. Completion criteria / 完成條件

If the FunctionSpec or template file does not exist, do not invent its content.  
若 FunctionSpec 或模板不存在，不要自行假設其內容。請依使用者提供的需求與現有程式碼產生 Prompt，並在 Prompt 中註明「若規格文件不存在，請以本次 Prompt 為準」。

---

## Always Include Safety Rules / 必須包含的安全限制

Every generated Codex prompt must include:

1. Do not create a Git repository.  
   不要建立 Git repository。

2. Do not create a branch.  
   不要建立 branch。

3. Do not create a worktree.  
   不要建立 worktree。

4. Do not run `git commit`.  
   不要執行 `git commit`。

5. Do not run `dotnet test`.  
   不要執行 `dotnet test`。

6. Run `dotnet build` at most once.  
   最多只執行一次 `dotnet build`。

7. Do not refactor the entire project.  
   不要重構整個專案。

8. Do not modify unrelated features.  
   不要修改與本次任務無關的功能。

9. Do not add new packages unless necessary.  
   除非必要，不要新增套件。

10. Complete only the requested task and then stop.  
    只完成本次指定任務，完成後停止。

---

## Prompt Structure / Prompt 結構

Generate the Codex prompt using this structure:

```markdown
請先閱讀：

1. `AGENTS.md`
2. `docs/<ProjectName>_FunctionSpec.md`，若此檔案存在且與本次任務相關

若 `docs/<ProjectName>_FunctionSpec.md` 不存在，請以本次 Prompt 與目前程式碼為準，不要自行假設規格。

本專案目前不使用 Git、不使用分支、不使用 worktree。
請直接在目前專案資料夾修改檔案。

---

## 本次任務

【清楚描述本次只要做的一個功能或一個修正】

---

## 目前狀況

【描述目前畫面、錯誤、行為或限制】

---

## 預期結果

【描述完成後應該變成什麼樣子】

---

## 參考規格章節

請只參考：

- `docs/<ProjectName>_FunctionSpec.md` 第【章節】章：【章節名稱】

若上述文件或章節不存在，請不要猜測，改依本次 Prompt 執行。

不要延伸實作其他章節。

---

## 修改範圍

本次允許修改：

1. 【允許修改的 View / ViewModel / Service / Model】
2. 【必要檔案】

---

## 不可修改範圍

本次不要修改：

1. 【不相關功能 1】
2. 【不相關功能 2】
3. 【不相關設定 / JSON / 解析邏輯】

---

## 限制

1. 不要建立 Git repository。
2. 不要建立 branch。
3. 不要建立 worktree。
4. 不要執行 `git commit`。
5. 不要執行 `dotnet test`。
6. 最多只執行 `dotnet build` 一次。
7. 不要重構整個專案。
8. 不要修改與本次任務無關的功能。
9. 不要自行新增未要求的功能。
10. 完成後停止，不要繼續延伸優化。

---

## 完成條件

1. 【可驗收條件 1】
2. 【可驗收條件 2】
3. 【可驗收條件 3】

---

## 完成後請回覆

請提供：

1. 修改檔案清單
2. 新增檔案清單
3. 主要修改內容
4. 手動測試方式
5. 是否有執行 `dotnet build`
6. 是否有未完成或需人工確認事項
```

---

## Output Rules / 輸出規則

When generating a Codex prompt:

1. Keep the scope small.  
   範圍要小。

2. Prefer one feature or one bug fix per prompt.  
   每個 Prompt 優先只處理一個功能或一個 bug。

3. Be explicit about what Codex must not touch.  
   明確列出不可修改範圍。

4. Include a clear stop condition.  
   必須包含完成後停止。

5. Include manual test steps in the completion response request.  
   要求 Codex 回覆手動測試方式。

6. Avoid vague wording such as “optimize everything” or “complete all remaining items”.  
   避免「全部優化」、「完成剩下所有功能」這類模糊描述。

7. Do not ask Codex to implement an entire FunctionSpec in one task.  
   不要要求 Codex 一次實作整份 FunctionSpec。

8. If the user’s request is too broad, split it into phases and ask the user to choose one phase.  
   若需求太大，先拆分階段，請使用者選擇其中一階段。

---

## Phasing Rules / 分階段規則

If the user asks to implement a large feature or an entire specification, do not generate one giant prompt.

Instead, produce a phased plan:

1. Phase 0: UI shell / navigation only  
   畫面骨架與導覽

2. Phase 1: UI fields only  
   欄位與版面

3. Phase 2: parsing / service logic only  
   解析與 Service 邏輯

4. Phase 3: settings persistence only  
   設定檔保存

5. Phase 4: validation only  
   驗證與檢查

6. Phase 5: integration and manual verification  
   整合與手動驗證

Each phase should include:

- Goal / 目標
- Scope / 範圍
- Forbidden scope / 不可修改範圍
- Completion criteria / 完成條件
- Stop point / 停止點

---

## Common FunctionSpec Paths / 常見規格路徑

Use the matching FunctionSpec path when available:

- `docs/ArasPatchUpgradeAssistant_FunctionSpec.md`
- `docs/ArasUpgradeDiffKit_FunctionSpec.md`
- `docs/ArasUpgradeAssistant_FunctionSpec.md`
- `docs/ArasAPV9SourceDocGenerator_FunctionSpec.md`

If the file does not exist, do not fail the task only because of the missing file.  
如果檔案不存在，不要只因缺少規格檔就停止；請依使用者本次 Prompt 與現有專案內容執行，並在輸出中提醒缺少規格檔。

---

## Final Reminder / 最後提醒

The FunctionSpec is the full blueprint.  
FunctionSpec 是完整藍圖。

The Codex prompt is the construction order for one small task.  
Codex Prompt 是一次只施工一小段的施工單。

Never ask Codex to implement the whole FunctionSpec in a single task unless the user explicitly confirms the risk.  
除非使用者明確確認風險，否則不要要求 Codex 一次實作整份 FunctionSpec。
