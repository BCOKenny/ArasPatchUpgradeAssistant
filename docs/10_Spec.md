# Spec

## Solution Layout

The solution contains:

- `ArasPatchUpgradeAssistant`: WPF application targeting `net8.0-windows`.
- `ArasPatchUpgradeAssistant.Tests`: xUnit test project for services, view models, helpers, and smoke coverage.

Main application folders:

- `Views`: WPF screens and view glue.
- `ViewModels`: wizard state, commands, validation status, and user-facing workflow logic.
- `Models`: data objects for settings, path derivation, directory validation, command generation, patch note generation, and AI settings.
- `Services`: parsing, file system access, settings, secret protection, logging, command generation, and patch explanation services.
- `Helpers`: WPF helper behavior such as password binding and masking.

## Workflow Overview

1. Setup selection
   - User selects the setup defaults template and Innovator server config.
   - The app derives upgrade/support paths, command folder, AP server root, vault config path, server URL, and DB connections.

2. Settings and secrets
   - User selections are saved to `%LocalAppData%\ArasPatchUpgradeAssistant\settings.json`.
   - Password values and OpenAI API key values are stored only in encrypted fields.
   - UI summaries and logs must not expose plaintext secrets.

3. SETUP CMD generation
   - The app writes a local `SETUP-DEFAULTS-{MachineName}.CMD` file.
   - The template `SETUP-DEFAULTS-MACHINENAME.CMD` may be tracked, but generated machine-specific setup files are ignored.

4. Directory validation
   - The app validates expected folders and files under the upgrade support layout.
   - Missing folders can be created through the app workflow when appropriate.

5. Patch assistance
   - Patch-related services analyze patch XML and provide patch explanation/description support.
   - Generated notes and external customer files remain local runtime artifacts.

## Repository Hygiene

The repository should track source code, tests, docs, solution/project files, and safe templates. It should ignore:

- Visual Studio state and .NET build outputs.
- Local settings and secret files.
- Logs and TestResults.
- Generated setup files except the machine-name template.
- Generated patch notes, external files, upgrade plans, customer packages, backups, and DB dumps.

## Reference Docs

Historical design details are preserved under `docs/superpowers/specs/` and `docs/superpowers/plans/`. The numbered docs in `docs/` are the preferred canonical path for future Chat / Work / Codex handoff.

