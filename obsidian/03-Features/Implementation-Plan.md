---
status: in-progress
created: 2026-10-14
tags: [plan]
---
# Implementation Plan — Import JSON → user-secrets

Spec: [[Import-JSON-to-User-Secrets]] · Architecture: [[01-Architecture/Overview]]

แต่ละ phase ทำบน git flow feature และ worktree ของตัวเอง ต้องผ่าน QA gate ([[ADR-0012-branching-and-qa]]) ก่อน merge เข้า `develop`
ทุก phase ต้องมี unit test และ coverage line/branch ≥ 90% ([[ADR-0011-testing-and-coverage]])

## Phase 0 — Scaffold + contracts (`core-tdd`, `git flow feature start p0-scaffold`) — ต้องเสร็จก่อน phase อื่น
- [x] `global.json`: SDK `10.0.400`, `rollForward: latestFeature`
- [x] สร้าง `SecretImport.slnx` และโปรเจค Core, Cli, Desktop, Core.Tests, Cli.Tests, Desktop.Tests (`net10.0`)
- [x] `Directory.Build.props`: `Nullable` enable, `TreatWarningsAsErrors`, `LangVersion` latest
- [x] `Directory.Packages.props`: ปักเวอร์ชันตาม [[ADR-0007-tech-stack]] และ [[ADR-0011-testing-and-coverage]]
- [x] `tests/Directory.Build.props`: xUnit v3, coverlet.msbuild, threshold 90 line + branch และกำหนดสิ่งที่ไม่นับใน coverage
- [x] `.config/dotnet-tools.json`: reportgenerator
- [x] Desktop: ใช้ template ของ Avalonia 12.1 + CommunityToolkit.Mvvm + DI และตรวจว่า `Avalonia.Headless.XUnit` ใช้กับ xUnit v3 ได้หรือไม่
- [x] Core: public contracts ตาม [[01-Architecture/Overview#Public contracts ของ Core (สร้างใน Phase 0)]] (ยังไม่มี implementation ของ `IImportService`) พร้อม test ของ `ImportPlan.HasChanges` และ `JsonInput.FromFile`
- [x] แก้ `AGENTS.md` ให้มีคำสั่ง build/test/coverage จริง
- ✅ `dotnet build -c Release` ไม่มี warning และ `dotnet test -p:CollectCoverage=true` ผ่านเกณฑ์

หลัง Phase 0 merge แล้ว ทำพร้อมกันได้: Phase 1–5 (`core-tdd`), Phase 6 (`cli-builder`), Phase 7 (`avalonia-ui`), Phase 8 (`release-ci`)

## Phase 1 — JsonFlattener (TDD)
- [x] กรณี object ซ้อน, array, number/bool, null (เตือน), string ว่าง
- [x] JSONC: comment และ trailing comma
- [x] key ซ้ำ (ไม่สนตัวพิมพ์เล็ก/ใหญ่) → error ที่ระบุ key
- [x] `--section` เจอ / ไม่เจอ
- [x] root ไม่ใช่ object → error
- [x] golden test: เทียบผลกับ `ConfigurationBuilder().AddJsonFile()`

## Phase 2 — ImportPlanner (TDD)
- [x] diff Added / Changed / Unchanged / Removed / Skipped
- [x] โหมด overwrite / skip / replace ตาม [[ADR-0003-import-merge-modes]]
- [x] ไม่มีการเปลี่ยนแปลง → `HasChanges == false`

## Phase 3 — SecretsStore
- [x] หา path ด้วย `PathHelper.GetSecretsPathFromSecretsId`
- [x] อ่าน: ไฟล์ไม่มี → ได้ค่าว่าง, ไฟล์ซ้อน → flatten, ไฟล์เสีย → error
- [x] เขียน: สร้างโฟลเดอร์, `.bak`, ไฟล์ชั่วคราว → rename, ตรวจ hash ว่าไฟล์เปลี่ยนไปหรือไม่
- [x] Unix permission ตาม [[ADR-0009-unix-file-permissions]] (ไฟล์ชั่วคราว 600 → ใช้ permission ของไฟล์เดิม, ไฟล์ใหม่ 600, โฟลเดอร์ 700, เตือนเรื่อง group/other) และให้ test ส่วนนี้ข้ามไปเมื่อรันบน Windows
- [x] test โดยกำหนด root ใน temp dir

## Phase 4 — Project discovery & dotnet CLI
- [x] `IDotnetCli` + ตัว implement ที่ใช้ process จริง (มี timeout, ตรวจว่ามี SDK)
- [x] `ProjectLocator`: csproj/fsproj/vbproj, โฟลเดอร์, sln/slnx ผ่าน `SolutionPersistence` และเตือนเรื่องโปรเจคที่ไม่มีไฟล์
- [x] `UserSecretsIdResolver`: ใช้ msbuild ถ้าไม่ได้ถอยไปอ่าน XML (รวม `Directory.Build.props`)
- [x] ตรวจ id ร่วมกันภายในชุดโปรเจคเดียวกัน
- [x] `SecretsInitializer`
- [x] integration test ที่สร้างโปรเจคชั่วคราวแล้วรัน `dotnet` จริง (แยก trait ไว้ ข้ามได้ถ้าเครื่องไม่มี SDK)

## Phase 5 — ImportService
- [x] `AnalyzeAsync` → `ImportAnalysis`
- [x] `InitAsync`, `ApplyAsync` (ตรวจว่าไฟล์เปลี่ยนไปหรือไม่ก่อนเขียน)

## Phase 6 — CLI
- [x] options ตาม [[ADR-0008-cli-contract]] ด้วย System.CommandLine 2.0
- [x] `--file -` (stdin) ต้องมี `--yes` เว้นแต่เป็น `--dry-run`
- [x] prompt ของ Spectre สำหรับ init และ confirm, ตรวจกรณี non-interactive, `--yes`, `--init`, `--dry-run`
- [x] ตาราง diff ที่ซ่อนค่าไว้, `--show-values` และ exit codes `0`/`1`/`2`/`3`
- [x] pack เป็น `dotnet tool` (`ToolCommandName=secret-import`, `RollForward=Major`)

## Phase 7 — Avalonia GUI
- [x] wizard: Project → JSON → Confirm → Result
- [x] เลือกไฟล์และโฟลเดอร์ด้วย `StorageProvider`
- [x] dialog ถามเรื่อง init, ตาราง diff พร้อมปุ่มดูค่า, dropdown เลือกโหมด
- [x] recent projects
- [ ] ทดสอบด้วยมือบน Windows, macOS และ Linux (Windows: เปิดแอปได้และ render ทุกขั้นผ่าน headless แล้ว ส่วน macOS/Linux ยังไม่ได้ทดสอบ)

## Phase 8 — Packaging & CI
- [x] `ci.yml` (เขียนเสร็จ YAML ผ่าน ยังไม่เคยรันบน GitHub เพราะยังไม่มี remote): build และ test บน `windows-latest`, `macos-latest`, `ubuntu-latest`
- [x] `release.yml` (ทำงานเมื่อ push tag `v*`) ตาม [[ADR-0010-distribution]]
  - GUI แบบ self-contained single-file: `win-x64`, `win-arm64`, `osx-arm64`, `osx-x64`, `linux-x64`, `linux-arm64`
  - macOS `.app` bundle แบบไม่ sign
  - CLI `.nupkg`
  - แนบทุกไฟล์ไว้ใน GitHub Release (ไม่ขึ้น nuget.org)
- [x] readme: วิธีติดตั้ง CLI ด้วย `--add-source` และวิธีเปิดแอปผ่าน Gatekeeper / SmartScreen

## สถานะ (as of 2026-10-14)
ทุก phase merge เข้า `develop` แล้ว ทุก feature ผ่าน QA gate
- Core.Tests 165 ผ่าน + 5 ข้าม (เฉพาะ Unix ซึ่งผ่านบน WSL แล้ว) coverage 97.45% / 95.08%
- Cli.Tests 67 ตัว 100% / 98.73%
- Desktop.Tests 73 ตัว 100% / 100%
- CLI ทดสอบ end-to-end กับโปรเจคจริงแล้ว (dry-run, ปฏิเสธเมื่อรัน non-interactive, `--init --yes`, replace ผ่าน stdin + `.bak`)
- แอป Desktop เปิดได้บน Windows

ยังค้าง: ทดสอบด้วยมือบน macOS/Linux, รัน CI/release บน GitHub และงานต่อจาก QA ใน [[Import-JSON-to-User-Secrets#Open questions]]
