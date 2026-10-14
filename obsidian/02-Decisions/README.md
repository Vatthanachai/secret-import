# Decisions

Architecture decision records. Create new ones from the ADR template, named `ADR-NNNN-title`.

- [[ADR-0001-app-shape-and-ui-framework]] — Core + Avalonia GUI + CLI
- [[ADR-0002-secrets-access-strategy]] — ตรวจ id ด้วย msbuild, init ด้วย CLI, เขียน secrets.json เอง
- [[ADR-0003-import-merge-modes]] — overwrite / skip / replace, diff, backup
- [[ADR-0004-json-input-format]] — flatten, `--section`, JSONC, key ซ้ำ
- [[ADR-0005-init-and-confirm-flow]] — ถามก่อน init, confirm ก่อน import
- [[ADR-0006-project-selection]] — csproj/fsproj/vbproj, โฟลเดอร์, solution, ทีละโปรเจค
- [[ADR-0007-tech-stack]] — net10.0, Avalonia 12.1, CommunityToolkit.Mvvm, System.CommandLine + Spectre
- [[ADR-0008-cli-contract]] — options, `--file -`, exit codes
- [[ADR-0009-unix-file-permissions]] — ไฟล์ใหม่ 600 / ไฟล์เดิมใช้ permission เดิม
- [[ADR-0010-distribution]] — GitHub Releases เท่านั้น, self-contained 6 RID, `.nupkg`
- [[ADR-0011-testing-and-coverage]] — xUnit v3 + coverlet, line/branch ≥ 90% ทุกโปรเจค
- [[ADR-0012-branching-and-qa]] — git flow (feature → QA gate → develop), worktree ต่อ agent

Back to [[Home]]
