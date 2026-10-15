# AGENTS.md

These are the instructions for AI coding agents working in this repository. `CLAUDE.md` imports this file, so update this one and don't duplicate it there.

## What this is

A tool that imports a JSON file into a .NET project's `dotnet user-secrets` store. It ships as an **Avalonia** desktop app and a **CLI** (`secret-import` dotnet tool) and must work on Windows, macOS, and Linux. MAUI was considered and rejected because it has no Linux support (ADR-0001).

## Current state: Phase 0 scaffold done

`SecretImport.slnx` contains `src/SecretImport.Core`, `src/SecretImport.Cli`, `src/SecretImport.Desktop` and a matching `tests/*.Tests` project for each (all `net10.0`). Core holds the public contracts only; `IImportService` has no implementation yet. The design lives in the Obsidian vault in `obsidian/` (open that folder as the vault, not the repo root):

- `obsidian/03-Features/Import-JSON-to-User-Secrets.md` — spec and acceptance criteria
- `obsidian/01-Architecture/Overview.md` — solution layout, Core public contracts and components, and the Analyze → Apply flow
- `obsidian/03-Features/Implementation-Plan.md` — phased plan (Phase 0–8); follow it in order and tick the checkboxes as you finish items
- `obsidian/02-Decisions/ADR-*.md` — accepted decisions; don't contradict these without a new ADR

The stack is `net10.0` (SDK pinned via `global.json`), Avalonia 12.1 + CommunityToolkit.Mvvm, System.CommandLine 2.0 + Spectre.Console, and SolutionPersistence. Package versions are pinned centrally in `Directory.Packages.props` (ADR-0007, ADR-0011). The CLI contract and exit codes are in ADR-0008 (plus `--json` and `--exit-code`, exit code 4, in ADR-0014), Unix file permissions in ADR-0009, and distribution (GitHub Releases only, no nuget.org) in ADR-0010.

## Build and test commands

Run from the repo root (or your worktree root):

```bash
dotnet build -c Release                                      # must have zero warnings (TreatWarningsAsErrors)
dotnet test -c Release                                       # all tests
dotnet test -c Release -p:CollectCoverage=true               # tests + coverage gate (90% line and branch per test project)
dotnet test --filter "FullyQualifiedName~<TestName>"         # a single test or class, e.g. ~ImportPlanTests
dotnet test tests/SecretImport.Core.Tests                    # one test project
dotnet test --filter "Category!=Integration"                 # skip tests that run the real dotnet SDK

# HTML coverage report (after a CollectCoverage run)
dotnet tool restore
dotnet reportgenerator "-reports:TestResults/coverage/*/coverage.cobertura.xml" "-targetdir:TestResults/coverage/report" "-reporttypes:Html;TextSummary"

dotnet run --project src/SecretImport.Cli -- --help          # CLI
dotnet run --project src/SecretImport.Desktop                # desktop app
```

- Coverage results go to `TestResults/coverage/<TestProject>/coverage.cobertura.xml` (git-ignored). Each test project measures only its project under test (`SecretImport.X.Tests` → `SecretImport.X`).
- Tests use xUnit v3 through the VSTest adapter (`IsTestingPlatformApplication=false` in `tests/Directory.Build.props`), because `coverlet.msbuild` hooks the VSTest target.
- Desktop UI tests run on Avalonia's headless platform through the `HeadlessSession` assembly fixture (`await headless.RunAsync(() => ...)`). `Avalonia.Headless.XUnit` 12.1.x doesn't work with xUnit v3 4.0, so it isn't used.

## Testing, coverage, and branches (ADR-0011, ADR-0012)

- Every `src/` project has a matching `tests/` project (xUnit v3). Coverage must be at least **90% line and 90% branch** in each test project, enforced by coverlet.msbuild thresholds in `tests/Directory.Build.props`. A build below that fails.
- Exclusions are limited to generated code, `Program.cs`, `App.axaml.cs`, and view code-behind that only calls `InitializeComponent()`. Never put `[ExcludeFromCodeCoverage]` on logic.
- Branching uses **git flow** (git-flow-next; `main` = production, `develop` = integration). Branches are created and merged only through `git flow feature|bugfix|release|hotfix start/finish`. Agents work in the worktree they're given and never run `finish`, merge, or switch branches.
- Nothing merges into `develop` until the QA gate passes: Release build with no warnings, all tests green, the coverage threshold met, and a review against the ADRs. Never touch `main` or start a release unless the user asks.
- The Core public contracts (`IImportService` and its records) are defined in `obsidian/01-Architecture/Overview.md`. Cli and Desktop code against them with fakes. Only `core-tdd` changes them.

## Architecture rules that span multiple files

- All logic lives in `SecretImport.Core`. `SecretImport.Cli` and `SecretImport.Desktop` are thin frontends over the same `ImportService` (Analyze → confirm → Apply). Core must not reference Avalonia or any CLI library.
- Library boundaries: System.CommandLine and Spectre live only in `SecretImport.Cli`, and Avalonia and the MVVM toolkit only in `SecretImport.Desktop`.
- Detect `UserSecretsId` with `dotnet msbuild -getProperty:UserSecretsId`, falling back to XML parsing when the SDK is missing. Initialize projects only through `dotnet user-secrets init`, so the tool never edits `.csproj` itself.
- Read and write `secrets.json` directly. Resolve its path with `PathHelper.GetSecretsPathFromSecretsId` and never hand-build it. Always write a `.bak` first, then write atomically (temp file + rename).
- On Unix, create temp files with mode `600` (`UnixCreateMode`) and copy the existing file's mode before renaming. Never loosen or silently tighten the mode of an existing `secrets.json`.
- Every call to `dotnet` goes through `IDotnetCli` so tests can fake it.
- Never print or log secret values unless the user explicitly asks to reveal them.

## Docs workflow

- When a decision changes, add or amend an ADR in `obsidian/02-Decisions/`, list it in that folder's `README.md`, and update the spec, plan, and this file if they're affected.
- Vault notes are written in Thai with English technical terms. Code, commit messages, and this file are in English.

## Coding conventions (from `.editorconfig`)

- Indentation: 4 spaces for `.cs`; 2 spaces for XML, project files (`.csproj`, `.slnx`, etc.), `.props`/`.targets`/`.config`, and JSON.
- File-scoped namespaces; namespaces should match folder structure.
- `using` directives outside the namespace, `System.*` first, with groups separated by blank lines.
- Explicit types preferred over `var` (all `csharp_style_var_*` are `false`).
- Always use braces; no `this.` qualification; use language keywords (`int`, `string`) over BCL type names.
- Accessibility modifiers required on non-interface members.
- No file header template; no trailing newline enforced (`insert_final_newline = false`).
- `.sln` files use CRLF line endings (`.gitattributes`).
