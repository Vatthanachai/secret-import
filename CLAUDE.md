# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Current state

This repository is a freshly initialized .NET project with no source code, solution, or project files yet. It contains only:

- `.editorconfig`, `.gitattributes`, `.gitignore` (standard `dotnet new` templates)
- an empty `readme.md`
- `obsidian/` — an Obsidian vault for project notes (open this folder, not the repo root, as the vault). Entry point is `obsidian/Home.md`; numbered folders hold inbox, architecture, ADRs, features, platform notes, and research; templates are in `obsidian/99-Templates`. `.obsidianignore` at the repo root keeps MAUI build output, platform assets, and secrets out of vault indexing.

There are no build, test, or lint commands yet. Once a solution exists, update this file with the real commands (e.g. `dotnet build`, `dotnet test`, running a single test with `dotnet test --filter "FullyQualifiedName~<TestName>"`) and an architecture overview.

## Coding conventions (from `.editorconfig`)

- Indentation: 4 spaces for `.cs`; 2 spaces for XML, project files (`.csproj`, `.slnx`, etc.), `.props`/`.targets`/`.config`, and JSON.
- File-scoped namespaces; namespaces should match folder structure.
- `using` directives outside the namespace, `System.*` first, with groups separated by blank lines.
- Explicit types preferred over `var` (all `csharp_style_var_*` are `false`).
- Always use braces; no `this.` qualification; use language keywords (`int`, `string`) over BCL type names.
- Accessibility modifiers required on non-interface members.
- No file header template; no trailing newline enforced (`insert_final_newline = false`).
- `.sln` files use CRLF line endings (`.gitattributes`).
