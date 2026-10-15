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
secret-import --project <path> --file <json|-> [--mode overwrite|skip|replace]
              [--section <path>] [--scope <path>] [--dry-run [--exit-code]] [--yes] [--init]
              [--show-values] [--json]
```

- `--file -` reads JSON from stdin and needs `--yes` unless you also pass `--dry-run`.
- `--exit-code` works only with `--dry-run`: it exits `4` when there are changes to import and `0` when there are none, so CI can check that secrets are in sync.
- `--json` prints one JSON object to stdout for every outcome and nothing to stderr. It never prompts, so pass `--yes` (and `--init`) to import. Secret values appear only with `--show-values`.
- Exit codes: `0` success, `1` invalid input, `2` cancelled or confirmation required, `3` environment or I/O error, `4` `--dry-run --exit-code` found changes.

```console
$ secret-import --project src/Api --file secrets.json --dry-run --json
{
  "exitCode": 0,
  "status": "dryRun",
  "project": { "name": "Api", "path": "/repo/src/Api/Api.csproj" },
  "userSecretsId": "abc-123",
  "secretsPath": "/home/me/.microsoft/usersecrets/abc-123/secrets.json",
  "mode": "overwrite",
  "section": null,
  "scope": "/repo/src/Api",
  "initialized": false,
  "summary": { "added": 1, "changed": 0, "removed": 0, "unchanged": 2, "skipped": 0 },
  "changes": [ { "key": "Db:Conn", "kind": "added" }, ... ],
  "warnings": [],
  "backupPath": null,
  "error": null
}
```

## Install
All downloads are on the project's GitHub Releases page. Nothing is published to nuget.org. Each release also includes a `SHA256SUMS.txt` file for checking downloads.

The tool runs `dotnet user-secrets` and `dotnet msbuild`, so the .NET 10 SDK must be installed either way.

### CLI (`secret-import`)
1. Download `SecretImport.Cli.<version>.nupkg` from the release into an empty folder, for example `~/Downloads/secret-import`.
2. Install it as a global tool from that folder:

   ```bash
   dotnet tool install -g SecretImport.Cli --add-source ~/Downloads/secret-import
   secret-import --help
   ```

   To upgrade, download the new `.nupkg` into the same folder and run `dotnet tool update -g SecretImport.Cli --add-source ~/Downloads/secret-import`. To remove it, run `dotnet tool uninstall -g SecretImport.Cli`.

The CLI targets .NET 10 and also runs on later .NET runtimes.

### Desktop app
Download the archive for your platform. The app is self-contained, so you don't need a separate .NET runtime to launch it.

| Platform | File |
|---|---|
| Windows x64 / Arm64 | `SecretImport-<version>-win-x64.zip` / `-win-arm64.zip` |
| macOS Apple Silicon / Intel | `SecretImport-<version>-osx-arm64.tar.gz` / `-osx-x64.tar.gz` |
| Linux x64 / Arm64 | `SecretImport-<version>-linux-x64.tar.gz` / `-linux-arm64.tar.gz` |

The builds aren't code-signed or notarized yet, so the OS warns the first time you open the app.

- **Windows:** extract the zip and run `SecretImport.Desktop.exe`. If SmartScreen shows "Windows protected your PC", click **More info**, then **Run anyway**. You can also right-click the zip before extracting it, choose **Properties**, and tick **Unblock**.
- **macOS:** extract the archive (`tar -xzf SecretImport-<version>-osx-arm64.tar.gz`) and move `SecretImport.app` to `/Applications`. The first time, right-click (or Control-click) the app, choose **Open**, then click **Open** in the dialog. On macOS 15 and later, if there's no **Open** button, try to open the app once, then go to **System Settings → Privacy & Security** and click **Open Anyway**. You can also clear the quarantine flag from the terminal: `xattr -dr com.apple.quarantine /Applications/SecretImport.app`.
- **Linux:** extract the archive (`tar -xzf SecretImport-<version>-linux-x64.tar.gz`) and run `./SecretImport.Desktop` from the extracted folder. The app needs an X11 or XWayland session and the usual desktop libraries (fontconfig, libICE, libSM).

## Releasing
`.github/workflows/release.yml` builds a release when a `v*` tag is pushed. The tag comes from `git flow release finish <version> -T v<version>`; pass `-T` because git flow names the tag without the `v` prefix by default. Run `git push origin v1.0.0` to start the build. A tag with a suffix, such as `v1.0.0-rc.1`, is published as a prerelease.

`.github/workflows/ci.yml` builds and tests every push and pull request on Windows, macOS and Linux, and enforces the 90% coverage gate.
