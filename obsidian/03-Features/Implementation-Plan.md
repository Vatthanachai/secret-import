---
status: planned
created: 2026-10-14
tags: [plan]
---
# Implementation Plan — Import JSON → user-secrets

Spec: [[Import-JSON-to-User-Secrets]] · Architecture: [[01-Architecture/Overview]]

แต่ละ phase จบด้วย build และ test ที่ผ่าน แล้ว commit แยก

## Phase 0 — Scaffold
- [ ] สร้าง `SecretImport.slnx` และโปรเจค Core, Cli, Desktop, Core.Tests (`net10.0`)
- [ ] `Directory.Build.props`: `Nullable` enable, `TreatWarningsAsErrors`, `LangVersion` latest
- [ ] `Directory.Packages.props` (central package management)
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
- [ ] test โดยกำหนด root ใน temp dir

## Phase 4 — Project discovery & dotnet CLI
- [ ] `IDotnetCli` + ตัว implement ที่ใช้ process จริง (มี timeout, ตรวจว่ามี SDK)
- [ ] `ProjectLocator`: csproj / โฟลเดอร์ / sln / slnx
- [ ] `UserSecretsIdResolver`: ใช้ msbuild ถ้าไม่ได้ถอยไปอ่าน XML (รวม `Directory.Build.props`)
- [ ] ตรวจ id ร่วมกันภายในชุดโปรเจคเดียวกัน
- [ ] `SecretsInitializer`
- [ ] integration test ที่สร้างโปรเจคชั่วคราวแล้วรัน `dotnet` จริง (แยก trait ไว้ ข้ามได้ถ้าเครื่องไม่มี SDK)

## Phase 5 — ImportService
- [ ] `AnalyzeAsync` → `ImportAnalysis`
- [ ] `InitAsync`, `ApplyAsync` (ตรวจว่าไฟล์เปลี่ยนไปหรือไม่ก่อนเขียน)

## Phase 6 — CLI
- [ ] options ตาม [[01-Architecture/Overview#CLI]]
- [ ] prompt สำหรับ init และ confirm, ตรวจกรณี non-interactive, `--yes`, `--init`, `--dry-run`
- [ ] แสดงตาราง diff ที่ซ่อนค่าไว้ และ exit codes
- [ ] pack เป็น `dotnet tool` (`ToolCommandName=secret-import`)

## Phase 7 — Avalonia GUI
- [ ] wizard: Project → JSON → Confirm → Result
- [ ] เลือกไฟล์และโฟลเดอร์ด้วย `StorageProvider`
- [ ] dialog ถามเรื่อง init, ตาราง diff พร้อมปุ่มดูค่า, dropdown เลือกโหมด
- [ ] recent projects
- [ ] ทดสอบด้วยมือบน Windows, macOS และ Linux

## Phase 8 — Packaging & CI
- [ ] GitHub Actions: build และ test บน `windows-latest`, `macos-latest`, `ubuntu-latest`
- [ ] publish GUI แบบ self-contained ต่อ RID (`win-x64`, `osx-arm64`, `linux-x64`) (TBC)
