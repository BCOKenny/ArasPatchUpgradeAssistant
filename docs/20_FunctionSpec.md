# Function Spec

## Setup Step

### Inputs

- `SETUP-DEFAULTS-MACHINENAME.CMD`
- `InnovatorServerConfig.xml`
- Optional user-entered login, database, and SQL credential values

### Behavior

- Validate the selected setup defaults template.
- Derive `SupportRoot`, `UpgradeRoot`, command folder, version, patch folders, solution folders, backup folder, and log folder.
- Locate `VaultServer\vault.config` from the selected Innovator server config path.
- Parse `InnovatorServerUrl` and derive the server prefix.
- Parse `DB-Connection` entries and expose them for selection.
- Generate `SETUP-DEFAULTS-{MachineName}.CMD` using CRLF line endings.
- Mask password values in UI previews and generated change summaries.

### Acceptance

- Generated command values preserve existing template format where possible.
- Generated machine-specific setup files are local artifacts and must not be committed.
- Plaintext passwords are not stored in settings or logs.

## Directory Validation Step

### Inputs

- Path information derived from the setup step

### Behavior

- Validate the expected support, commands, tools, patches, solutions, logs, backup, and generated setup CMD paths.
- Report each item as OK, Missing, or Warning.
- Allow creation of missing folders where the workflow supports it.

### Acceptance

- Required folders and files are shown with clear status.
- Folder creation is scoped to missing folders and does not create files unexpectedly.

## Upgrade Options And Patch Explanation

### Inputs

- Patch, catalog, BAT, CMD, XML, and SQL-related files from the local upgrade workspace
- Optional OpenAI API key stored in encrypted settings

### Behavior

- Analyze patch-related files for user-facing upgrade assistance.
- Save AI patch description settings without plaintext API keys.
- Report API key decryption failures without exposing the key value.

### Acceptance

- Generated patch notes and external files remain outside Git.
- API keys are never committed in plaintext.

## Settings And Logging

### Behavior

- Store local user settings under `%LocalAppData%\ArasPatchUpgradeAssistant\settings.json`.
- Store logs under `%LocalAppData%\ArasPatchUpgradeAssistant\logs`.
- Use encrypted settings fields for passwords and OpenAI API keys.

### Acceptance

- `settings.json`, `ai-settings.json`, logs, and development-only settings are ignored by Git.
- Logs include operational errors without plaintext passwords, API keys, or full secret dictionaries.

## Codex Task Boundaries

Codex tasks should follow `AGENTS.md`: make the smallest safe change, avoid broad refactoring, avoid unrelated files, do not run `dotnet test` unless explicitly authorized, and run `dotnet build` at most once unless the user overrides the rule.
