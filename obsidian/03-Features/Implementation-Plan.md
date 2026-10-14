---
status: planned
created: 2026-10-14
tags: [plan]
---
# Implementation Plan — Import JSON → user-secrets

Spec: [[Import-JSON-to-User-Secrets]] · Architecture: [[01-Architecture/Overview]]

แต่ละ phase จบด้วย build และ test ที่ผ่าน แล้ว commit แยก

## Phase 0 — Scaffold
- [ ] `global.json`: SDK `10.0.400`, `rollForward: latestFeature`
- [ ] สร้าง `SecretImport.slnx` และโปรเจค Core, Cli, Desktop, Core.Tests (`net10.0`)
- [ ] `Directory.Build.props`: `Nullable` enable, `TreatWarningsAsErrors`, `LangVersion` latest
- [ ] `Directory.Packages.props`: ปักเวอร์ชันตาม [[ADR-0007-tech-stack]]
- [ ] Desktop: ใช้ template ของ Avalonia 12.1 + CommunityToolkit.Mvvm + DI
- [ ] แก้ `CLAUDE.md` ให้มีคำสั่ง build/test จริง
- ✅ `dotnet build` และ `dotnet test` ผ่าน

## Phase 1 — JsonFlattener (TDD)
- [ ] กรณี object ซ้อน, array, number/bool, null (เตือน), string ว่าง
- [ ] JSONC: comment และ trailing comma
- [ ] key ซ้ำ (ไม่สนตัวพิมพ์เล็ก/ใหญ่) → error ที่ระบุ key
- [ ] `--section` เจอ / ไม่เจอ
- [ ] root ไม่ใช่ object → error
- [ ] golden test: เทียบผลกับ `ConfigurationBuilder().AddJsonFile()`

## Phase 2 — ImportPlanner (TDD)
- [ ] diff Added / Changed / Unchanged / Removed / Skipped
- [ ] โหมด overwrite / skip / replace ตาม [[ADR-0003-import-merge-modes]]
- [ ] ไม่มีการเปลี่ยนแปลง → `HasChanges == false`

## Phase 3 — SecretsStore
- [ ] หา path ด้วย `PathHelper.GetSecretsPathFromSecretsId`
- [ ] อ่าน: ไฟล์ไม่มี → ได้ค่าว่าง, ไฟล์ซ้อน → flatten, ไฟล์เสีย → error
- [ ] เขียน: สร้างโฟลเดอร์, `.bak`, ไฟล์ชั่วคราว → rename, ตรวจ hash ว่าไฟล์เปลี่ยนไปหรือไม่
- [ ] Unix permission ตาม [[ADR-0009-unix-file-permissions]] (ไฟล์ชั่วคราว 600 → ใช้ permission ของไฟล์เดิม, ไฟล์ใหม่ 600, โฟลเดอร์ 700, เตือนเรื่อง group/other) และให้ test ส่วนนี้ข้ามไปเมื่อรันบน Windows
- [ ] test โดยกำหนด root ใน temp dir

## Phase 4 — Project discovery & dotnet CLI
- [ ] `IDotnetCli` + ตัว implement ที่ใช้ process จริง (มี timeout, ตรวจว่ามี SDK)
- [ ] `ProjectLocator`: csproj/fsproj/vbproj, โฟลเดอร์, sln/slnx ผ่าน `SolutionPersistence` และเตือนเรื่องโปรเจคที่ไม่มีไฟล์
- [ ] `UserSecretsIdResolver`: ใช้ msbuild ถ้าไม่ได้ถอยไปอ่าน XML (รวม `Directory.Build.props`)
- [ ] ตรวจ id ร่วมกันภายในชุดโปรเจคเดียวกัน
- [ ] `SecretsInitializer`
- [ ] integration test ที่สร้างโปรเจคชั่วคราวแล้วรัน `dotnet` จริง (แยก trait ไว้ ข้ามได้ถ้าเครื่องไม่มี SDK)

## Phase 5 — ImportService
- [ ] `AnalyzeAsync` → `ImportAnalysis`
- [ ] `InitAsync`, `ApplyAsync` (ตรวจว่าไฟล์เปลี่ยนไปหรือไม่ก่อนเขียน)

## Phase 6 — CLI
- [ ] options ตาม [[ADR-0008-cli-contract]] ด้วย System.CommandLine 2.0
- [ ] `--file -` (stdin) ต้องมี `--yes` เว้นแต่เป็น `--dry-run`
- [ ] prompt ของ Spectre สำหรับ init และ confirm, ตรวจกรณี non-interactive, `--yes`, `--init`, `--dry-run`
- [ ] ตาราง diff ที่ซ่อนค่าไว้, `--show-values` และ exit codes `0`/`1`/`2`/`3`
- [ ] pack เป็น `dotnet tool` (`ToolCommandName=secret-import`, `RollForward=Major`)

## Phase 7 — Avalonia GUI
- [ ] wizard: Project → JSON → Confirm → Result
- [ ] เลือกไฟล์และโฟลเดอร์ด้วย `StorageProvider`
- [ ] dialog ถามเรื่อง init, ตาราง diff พร้อมปุ่มดูค่า, dropdown เลือกโหมด
- [ ] recent projects
- [ ] ทดสอบด้วยมือบน Windows, macOS และ Linux

## Phase 8 — Packaging & CI
- [ ] `ci.yml`: build และ test บน `windows-latest`, `macos-latest`, `ubuntu-latest`
- [ ] `release.yml` (ทำงานเมื่อ push tag `v*`) ตาม [[ADR-0010-distribution]]
  - GUI แบบ self-contained single-file: `win-x64`, `win-arm64`, `osx-arm64`, `osx-x64`, `linux-x64`, `linux-arm64`
  - macOS `.app` bundle แบบไม่ sign
  - CLI `.nupkg`
  - แนบทุกไฟล์ไว้ใน GitHub Release (ไม่ขึ้น nuget.org)
- [ ] readme: วิธีติดตั้ง CLI ด้วย `--add-source` และวิธีเปิดแอปผ่าน Gatekeeper / SmartScreen
