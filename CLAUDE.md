# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A tool that imports a JSON file into a .NET project's `dotnet user-secrets` store. It ships as an **Avalonia** desktop app and a **CLI** (`secret-import` dotnet tool) and must work on Windows, macOS, and Linux. MAUI was considered and rejected because it has no Linux support (see ADR-0001).

## Current state: planning, no code yet

There are no source files and no build/test commands yet. The design lives in the Obsidian vault in `obsidian/` (open that folder as the vault, not the repo root):

- `obsidian/03-Features/Import-JSON-to-User-Secrets.md` — spec and acceptance criteria
- `obsidian/01-Architecture/Overview.md` — planned solution layout, Core components, and the Analyze → Apply flow
- `obsidian/03-Features/Implementation-Plan.md` — phased plan; follow it in order
- `obsidian/02-Decisions/ADR-*.md` — accepted decisions; don't contradict these without a new ADR

Items marked **(TBC)** in the vault (exact .NET/Avalonia versions, MVVM/CLI libraries, packaging) are not decided yet. Confirm them with the user before you lock them in.

After Phase 0 (scaffold), replace this section with the real `dotnet build` / `dotnet test` commands, including how to run a single test (`dotnet test --filter "FullyQualifiedName~<TestName>"`).

## Architecture rules that span multiple files

- All logic lives in `SecretImport.Core`. `SecretImport.Cli` and `SecretImport.Desktop` are thin frontends over the same `ImportService` (Analyze → confirm → Apply). Core must not reference Avalonia or any CLI library.
- Detect `UserSecretsId` with `dotnet msbuild -getProperty:UserSecretsId`, falling back to XML parsing when the SDK is missing. Initialize projects only through `dotnet user-secrets init`, so the tool never edits `.csproj` itself.
- Read and write `secrets.json` directly. Resolve its path with `PathHelper.GetSecretsPathFromSecretsId` and never hand-build it. Always write a `.bak` first, then write atomically (temp file + rename).
- Every call to `dotnet` goes through `IDotnetCli` so tests can fake it.
- Never print or log secret values unless the user explicitly asks to reveal them.

## Coding conventions (from `.editorconfig`)

- Indentation: 4 spaces for `.cs`; 2 spaces for XML, project files (`.csproj`, `.slnx`, etc.), `.props`/`.targets`/`.config`, and JSON.
- File-scoped namespaces; namespaces should match folder structure.
- `using` directives outside the namespace, `System.*` first, with groups separated by blank lines.
- Explicit types preferred over `var` (all `csharp_style_var_*` are `false`).
- Always use braces; no `this.` qualification; use language keywords (`int`, `string`) over BCL type names.
- Accessibility modifiers required on non-interface members.
- No file header template; no trailing newline enforced (`insert_final_newline = false`).
- `.sln` files use CRLF line endings (`.gitattributes`).
