# AGENTS.md

## Project / 專案

This repository uses Codex for controlled, small-scope implementation tasks.  
本專案使用 Codex 協助進行小範圍、可控的程式修改與功能實作。

Project name: `ArasPatchUpgradeAssistant`

---

## Canonical Documents / 主文件

Current canonical project documents are the numbered Chat / Work / Codex handoff files:

```text
docs/00_Requirement_Handoff.md
docs/10_Spec.md
docs/20_FunctionSpec.md
```

Roles:

- `docs/00_Requirement_Handoff.md`: Requirement handoff from Chat discussion.
- `docs/10_Spec.md`: Project specification for the Work stage.
- `docs/20_FunctionSpec.md`: Function-level implementation reference for Codex.

Legacy or alternate names such as `docs/Requirement.md`, `docs/Spec.md`, and `docs/FunctionSpec.md` may exist as reference copies, but the numbered files above are the current canonical docs unless the user explicitly says otherwise.

Documents under `docs/superpowers/plans/` or `docs/superpowers/specs/` are historical or task-level references. They are not the latest main specification unless the user explicitly names them as the source for the current task.

---

## Read First / 優先閱讀

Before editing files:

1. Read the user's current task prompt carefully.
   先仔細閱讀使用者本次任務。
2. If the task concerns requirement handoff, read only the relevant sections of `docs/00_Requirement_Handoff.md`.
   若任務與需求交接相關，只閱讀 `docs/00_Requirement_Handoff.md` 的相關章節。
3. If the task concerns architecture, scope, or workflow, read only the relevant sections of `docs/10_Spec.md`.
   若任務與架構、範圍或流程相關，只閱讀 `docs/10_Spec.md` 的相關章節。
4. If the task concerns implementation details, read only the relevant sections of `docs/20_FunctionSpec.md`.
   若任務與實作細節相關，只閱讀 `docs/20_FunctionSpec.md` 的相關章節。
5. If a referenced document does not exist, do not guess its contents.
   如果參考文件不存在，不要自行假設內容。

---

## Operating Rules / 作業規則

Unless the user explicitly says otherwise:

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
7. Do not perform broad refactoring.  
   不要進行大範圍重構。
8. Do not modify unrelated features.  
   不要修改與本次任務無關的功能。
9. Do not add new packages unless necessary.  
   除非必要，不要新增套件。
10. If the task is unclear or too broad, propose a short phased plan and stop.  
    如果任務不清楚或範圍太大，請先提出簡短分階段計畫並停止。
11. Complete only the requested task and then stop.  
    只完成本次指定任務，完成後停止。

### Git Operation Exception / Git 操作例外

The Git restrictions above may be temporarily overridden only when the user's current task explicitly asks to initialize Git, connect a remote repository, commit, push, or create a pull request.  
只有當使用者本次任務明確要求初始化 Git、連接遠端 Repository、commit、push 或建立 PR 時，才可暫時覆蓋上述 Git 限制。

When Git operations are explicitly authorized:

1. Confirm `.gitignore` excludes local settings, secrets, logs, build outputs, and generated upgrade artifacts.  
   先確認 `.gitignore` 已排除本機設定、密鑰、Log、編譯輸出與升級產生物。
2. Run `git status --short` before `git add`.  
   執行 `git add` 前，先執行 `git status --short`。
3. Do not stage or commit API keys, passwords, `settings.json`, `ai-settings.json`, logs, `bin/`, `obj/`, `.vs/`, `TestResults/`, `patch-notes/`, `external-files/`, `upgrade-plan.json`, or customer upgrade packages.
   不可 stage 或 commit API Key、密碼、`settings.json`、`ai-settings.json`、Log、`bin/`、`obj/`、`.vs/`、`TestResults/`、`patch-notes/`、`external-files/`、`upgrade-plan.json` 或客戶升級包資料。
4. If suspected secrets or customer data are found, stop and report before committing.  
   若發現疑似密鑰或客戶資料，commit 前必須停止並回報。
5. Use only the remote URL explicitly provided by the user.  
   只能使用使用者明確提供的 remote URL。
6. Do not create extra branches or worktrees unless explicitly requested.  
   除非使用者明確要求，不要建立額外 branch 或 worktree。
7. After `git add`, show staged files before committing.  
   `git add` 後，commit 前需顯示 staged files。
8. After push, report remote URL, branch, commit id, and build status.  
   push 後需回報 remote URL、branch、commit id 與 build 狀態。

---

## Safety Rules / 安全規則

For this project:

1. Do not execute upgrade BAT/CMD files unless the user explicitly asks in a future execution feature.
2. Do not execute SQL.
3. Do not execute AML.
4. Do not execute Patch XML Body.
5. Do not modify original vendor BAT files.
6. Do not modify original Catalog XML files.
7. Do not modify original Patch XML files.
8. Do not log passwords.
9. Do not log OpenAI API keys.
10. Do not log Authorization headers.
11. Do not log full Patch Body.
12. Do not log full OpenAI request JSON.

---

## Patch Note / AI Rules

When modifying Patch note or AI Chinese description features:

1. Official update items may use `{Name}.xml` to find Patch XML.
2. External fix items must analyze their own stored file and must not search `{Name}.xml`.
3. First AI version should use `SourceMode=DescriptionOnly`.
4. Do not send full Patch XML, full Body, full SQL, full CSharp, or full AML to AI.
5. If AI fails, fallback to manual-confirmation text and do not block Markdown generation.
6. AI failure must not stop Markdown generation.
7. Diagnostic logs may record status code, error message/type/code, request size, source mode, and x-request-id, but never secrets.

---

## Codex Prompt Skill / Codex Prompt 技能

When the user asks to generate a Codex prompt, use the repo skill if available:

- `.agents/skills/codex-task-prompt/SKILL.md`

The generated prompt must be small, explicit, and safe to execute.  
產生的 Prompt 必須小範圍、明確、可驗收，並且避免 Codex 一次修改過多內容。

---

## Completion Response / 完成後回覆格式

After completing a task, respond with:

1. Modified files / 修改檔案
2. Added files / 新增檔案
3. Summary of changes / 修改摘要
4. Manual test steps / 手動測試方式
5. Whether `dotnet build` was executed / 是否有執行 `dotnet build`
6. Known limitations or follow-up items / 已知限制或後續事項
