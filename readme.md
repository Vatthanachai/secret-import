# net-secret-import

Import values from a JSON file into a .NET project's `dotnet user-secrets` store. It comes as an Avalonia desktop app and a `secret-import` CLI tool, and runs on Windows, macOS and Linux.

> Status: planning. See `obsidian/Home.md` for the spec, the architecture decisions and the implementation plan.

## Planned behavior
- You pick a target as a `.csproj`, a folder or a solution (`.sln` / `.slnx`), and import into one project at a time.
- The tool detects `UserSecretsId` with `dotnet msbuild -getProperty`. If no SDK is available, it reads the project XML instead.
- If the project isn't initialized yet, it offers to run `dotnet user-secrets init`. If it is, you must confirm before the import starts.
- Nested JSON is flattened the same way .NET configuration does it (`Parent:Child`, `Items:0`). JSONC is accepted, and `--section` imports a single section.
- Merge modes are `overwrite` (default), `skip` and `replace`. The tool shows a diff with values hidden, supports `--dry-run`, and writes atomically after saving a `secrets.json.bak` backup.

```
secret-import --project <path> --file <json> [--mode overwrite|skip|replace]
              [--section <path>] [--dry-run] [--yes] [--init] [--show-values]
```
