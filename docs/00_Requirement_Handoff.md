# Requirement Handoff

## Purpose

This document is the Chat-level handoff for ArasPatchUpgradeAssistant. It captures the confirmed product intent and repository hygiene rules so implementation tasks can stay small and safe.

## Product Goal

ArasPatchUpgradeAssistant is a .NET 8 WPF assistant for Aras Innovator patch upgrade preparation. It helps an upgrade engineer derive setup paths, read Innovator configuration, generate machine-specific SETUP CMD files, validate upgrade folders, and prepare patch-related execution information without exposing local secrets.

## Confirmed Scope

- Select a `SETUP-DEFAULTS-MACHINENAME.CMD` template.
- Select an `InnovatorServerConfig.xml` file.
- Derive support, command, upgrade, patch, solution, backup, and log paths from the selected files.
- Read DB connection metadata from Innovator configuration and vault URL information from `vault.config`.
- Let the user choose or enter database/login values needed for generated setup commands.
- Generate `SETUP-DEFAULTS-{MachineName}.CMD` as a local runtime artifact.
- Validate expected upgrade folders and files before later patch work.
- Store user settings under the user's local application data folder.
- Protect saved passwords and OpenAI API keys with the local secret protection service.
- Write application logs locally without plaintext secrets.

## Out Of Scope For Git

The repository must not include local settings, API keys, passwords, logs, build outputs, generated setup files, generated patch notes, external customer files, customer upgrade packages, backups, or database dumps.

## Document Roles

- `docs/00_Requirement_Handoff.md`: Chat and requirement handoff.
- `docs/10_Spec.md`: Work-level design and implementation orientation.
- `docs/20_FunctionSpec.md`: Codex-ready functional boundaries and acceptance points.
- `docs/superpowers/**`: historical design and implementation planning references.

## Canonical Docs

Use the numbered docs set as the canonical handoff path:

- `docs/00_Requirement_Handoff.md`
- `docs/10_Spec.md`
- `docs/20_FunctionSpec.md`

