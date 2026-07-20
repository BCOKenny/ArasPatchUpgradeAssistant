# AGENTS.md

## Project / 專案

This repository uses Codex for controlled, small-scope implementation tasks.  
本專案使用 Codex 協助進行小範圍、可控的程式修改與功能實作。

Main specification files may exist under `docs/`.  
主要規格文件通常放在 `docs/` 目錄。

Common examples:

- `docs/<ProjectName>_Spec.md`
- `docs/<ProjectName>_FunctionSpec.md`
- `docs/codex/TASK_PROMPT_TEMPLATE.md`

If a referenced document does not exist, do not guess its contents.  
如果上述文件不存在，不要自行假設內容；請依使用者本次 Prompt 與現有程式碼進行，必要時先提出需確認事項。

---

## Read First / 優先閱讀

Before editing files:

1. Read the user’s current task prompt carefully.  
   先仔細閱讀使用者本次任務。

2. If `docs/<ProjectName>_FunctionSpec.md` exists and is relevant, read only the relevant section.  
   如果 `docs/<ProjectName>_FunctionSpec.md` 存在且與本次任務相關，只閱讀相關章節。

3. If `docs/codex/TASK_PROMPT_TEMPLATE.md` exists and the user asks to create a Codex prompt, use it as a format reference.  
   如果 `docs/codex/TASK_PROMPT_TEMPLATE.md` 存在，且使用者要求產生 Codex Prompt，才使用它作為格式參考。

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
當 Git 操作已被明確授權時：

1. Confirm `.gitignore` excludes local settings, secrets, logs, build outputs, and generated upgrade artifacts.  
   先確認 `.gitignore` 已排除本機設定、密鑰、Log、編譯輸出與升級產生物。

2. Run `git status --short` before `git add`.  
   執行 `git add` 前，先執行 `git status --short`。

3. Do not stage or commit API keys, passwords, `settings.json`, `ai-settings.json`, logs, `bin/`, `obj/`, `.vs/`, `TestResults/`, `patch-notes/`, `external-files/`, or customer upgrade packages.  
   不可 stage 或 commit API Key、密碼、`settings.json`、`ai-settings.json`、Log、`bin/`、`obj/`、`.vs/`、`TestResults/`、`patch-notes/`、`external-files/` 或客戶升級包資料。

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

## Task Execution Rules / 任務執行規則

For every task:

1. Identify the smallest safe change.  
   先判斷最小且安全的修改範圍。

2. Modify only the required files.  
   只修改必要檔案。

3. Keep existing behavior unchanged unless the user explicitly requests a change.  
   除非使用者明確要求，否則保留既有行為。

4. Preserve existing UI, naming, paths, settings, and JSON formats unless the current task requires changing them.  
   除非本次任務需要，否則保留既有 UI、命名、路徑、設定與 JSON 格式。

5. Avoid rewriting large files unless necessary.  
   除非必要，不要重寫大型檔案。

6. Avoid changing tests unless the user asks.  
   除非使用者要求，不要修改測試。

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
