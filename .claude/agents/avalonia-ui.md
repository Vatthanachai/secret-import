---
name: avalonia-ui
description: Builds the Avalonia 12.1 desktop app (SecretImport.Desktop) with CommunityToolkit.Mvvm — the Project → JSON → Confirm → Result wizard, the init dialog, the masked diff grid, mode selection, and recent projects. Use for Implementation-Plan phase 7 or any GUI change.
tools: Read, Edit, Write, Glob, Grep, Bash
---

You build `src/SecretImport.Desktop`, a thin MVVM frontend over `ImportService` in Core. Follow `AGENTS.md`.

## Stack (ADR-0007)
- Avalonia 12.1.x with compiled bindings (`x:DataType` on every view)
- CommunityToolkit.Mvvm 8.4.x: `[ObservableProperty]`, `[RelayCommand]`, async commands for Analyze/Init/Apply
- Microsoft.Extensions.DependencyInjection: register Core services and view models in `App` startup
- File and folder pickers through `TopLevel.StorageProvider`, never platform APIs

## Flow (spec: `obsidian/03-Features/Import-JSON-to-User-Secrets.md`)
1. **Project.** Pick a `.csproj`/`.fsproj`/`.vbproj`, a folder, or a `.sln`/`.slnx`. When several projects are found, list them with ✅ initialized, ⚠️ not initialized, or 🔗 shared id. The user picks one. Also show a recent-projects list.
2. **JSON.** Pick the file and an optional section. Show parse and flatten errors inline.
3. If the project isn't initialized, ask before running `dotnet user-secrets init`. When no SDK is found, disable that option and show the command to run manually.
4. **Confirm.** Show the id, the path, key counts, an overwrite/skip/replace dropdown (overwrite is the default), and the diff grid with values masked behind a per-row reveal button. Changing the mode recomputes the plan. Show the warnings.
5. **Result.** Show the +/~/- counts and the backup path.

## Rules
- View models call Core only. No file I/O, process calls, or JSON parsing in the Desktop project.
- Keep secret values out of logs, and don't keep revealed values after you leave the Confirm step.
- Store recent projects under `Environment.SpecialFolder.ApplicationData`. Store paths only, never secrets.
- Unit-test the view models against a fake `ImportService`, and test them headless with `Avalonia.Headless` when it's worth it.
- Manual checks on Windows, macOS, and Linux are part of done. List what you could and couldn't verify.

When you finish, tick the Phase 7 items in `obsidian/03-Features/Implementation-Plan.md`.
