---
tags: [architecture]
updated: 2026-10-14
---
# Architecture Overview

## Solution layout (แผน)
```
SecretImport.slnx
global.json                 SDK 10.0.400, rollForward latestFeature
Directory.Build.props
Directory.Packages.props    central package versions
src/
  SecretImport.Core/        net10.0 class library — logic ทั้งหมด
  SecretImport.Cli/         net10.0 dotnet tool `secret-import`
  SecretImport.Desktop/     Avalonia app (Windows / macOS / Linux)
tests/
  Directory.Build.props     coverage threshold 90% (line + branch)
  SecretImport.Core.Tests/
  SecretImport.Cli.Tests/
  SecretImport.Desktop.Tests/
```
test และ coverage ดูที่ [[ADR-0011-testing-and-coverage]] ส่วนการแบ่งงานและ QA ดูที่ [[ADR-0012-branching-and-qa]]

## Public contracts ของ Core (สร้างใน Phase 0)
Cli และ Desktop เขียนโค้ดเทียบกับ contract ชุดนี้ ถ้าจะเปลี่ยนต้องผ่าน `core-tdd` และ QA ก่อน
```csharp
namespace SecretImport.Core;

public enum ImportMode { Overwrite, Skip, Replace }
public enum SecretChangeKind { Added, Changed, Unchanged, Removed, Skipped }
public enum SecretsIdSource { None, MsBuild, XmlFallback }
public enum ImportWarningKind { NullValueSkipped, SharedSecretsId, XmlFallbackUsed, InsecureFilePermissions, MissingProjectFile }
public enum ImportErrorKind { InvalidInput, NoProjectFound, MultipleProjects, SdkUnavailable, ConcurrentModification, IoFailure }

public sealed record ProjectRef(string Path, string Name);
public sealed record ProjectInfo(ProjectRef Project, string? UserSecretsId, SecretsIdSource Source, IReadOnlyList<ProjectRef> SharedWith);

public sealed record JsonInput(string DisplayName, string Content)
{
    public static JsonInput FromFile(string path);      // UTF-8 (ตัด BOM ออก), DisplayName = full path, ไม่มีไฟล์ → InvalidInput, อ่านไม่ได้ → IoFailure
}

public sealed record ImportRequest(string ProjectPath, JsonInput Json, ImportMode Mode, string? Section, string? ScopePath = null);  // ADR-0013
public sealed record DiscoveryResult(IReadOnlyList<ProjectInfo> Projects, IReadOnlyList<ImportWarning> Warnings);
public sealed class SolutionScopeResolver   // ADR-0013 Amendment 1, ใช้ร่วมกันทั้ง CLI และ Desktop
{
    public string? Resolve(string projectPath, string? explicitScope);
    public static bool IsProjectFile(string path);   // ตรวจนามสกุล .csproj/.fsproj/.vbproj (Desktop ใช้ตัดสินว่าค้นหาจากไฟล์โปรเจคหรือไม่)
}

public sealed record SecretChange(string Key, SecretChangeKind Kind, string? OldValue, string? NewValue);
public sealed record ImportPlan(IReadOnlyList<SecretChange> Changes, IReadOnlyDictionary<string, string> Result)
{
    public bool HasChanges { get; }                      // true ถ้ามี Added/Changed/Removed
}
public sealed record ImportWarning(ImportWarningKind Kind, string Message);   // Message ห้ามมีค่า secret

public sealed record ImportAnalysis(
    ProjectInfo Project, ImportRequest Request, string? SecretsPath,
    bool NeedsInit, bool CanInit, int ExistingCount, int IncomingCount,
    ImportPlan Plan, IReadOnlyList<ImportWarning> Warnings, string? ExistingFileHash);

public sealed record ImportResult(int Added, int Changed, int Removed, string SecretsPath, string? BackupPath);

public sealed record ExportRequest(string ProjectPath, string? Section = null, bool Flat = false);   // ADR-0015
public sealed record ExportResult(ProjectInfo Project, string SecretsPath, int KeyCount, string Json, IReadOnlyList<ImportWarning> Warnings);

public sealed class ImportException : Exception
{
    public ImportException(ImportErrorKind kind, string message, IReadOnlyList<ProjectRef>? candidates = null, Exception? innerException = null);
    public ImportErrorKind Kind { get; }
    public IReadOnlyList<ProjectRef> Candidates { get; }  // ใช้ตอน MultipleProjects ถ้าไม่ใช่จะเป็นรายการว่าง
}

public interface IImportService
{
    Task<DiscoveryResult> DiscoverProjectsAsync(string path, CancellationToken ct = default);
    Task<ImportAnalysis> AnalyzeAsync(ImportRequest request, CancellationToken ct = default);   // ยังไม่ init → NeedsInit = true, Plan เทียบกับค่าว่าง
    Task InitAsync(ProjectRef project, CancellationToken ct = default);                          // ไม่มี SDK → ImportException(SdkUnavailable)
    Task<ImportResult> ApplyAsync(ImportAnalysis analysis, CancellationToken ct = default);      // ไฟล์เปลี่ยนไปหลัง Analyze → ConcurrentModification
    Task<ExportResult> ExportAsync(ExportRequest request, CancellationToken ct = default);        // ADR-0015, ไม่เขียนอะไร
    Task WriteExportAsync(string path, string json, bool overwrite, CancellationToken ct = default); // ADR-0015, atomic, Unix 600
}
```
การ map ไปเป็น exit code ของ CLI: `InvalidInput`/`NoProjectFound`/`MultipleProjects` → `1` และ `SdkUnavailable`/`ConcurrentModification`/`IoFailure` → `3`
ทิศทางการอ้างอิง: `Cli` → `Core` ← `Desktop` และ Core ห้ามอ้างอิง UI framework ใดๆ
library และเวอร์ชันทั้งหมดอยู่ใน [[ADR-0007-tech-stack]]

## Core components
| Component | หน้าที่ |
|---|---|
| `ProjectLocator` | รับ path ที่เป็น `.csproj/.fsproj/.vbproj`, โฟลเดอร์ หรือ `.sln/.slnx` (อ่านด้วย SolutionPersistence) → คืนรายการ `ProjectRef` |
| `UserSecretsIdResolver` | เรียก `dotnet msbuild -getProperty:UserSecretsId` ถ้าไม่สำเร็จถอยไปอ่าน XML → คืน `SecretsIdResult { Id?, Source: MsBuild \| XmlFallback }` |
| `SecretsInitializer` | เรียก `dotnet user-secrets init --project` |
| `IDotnetCli` | รัน `dotnet` และเก็บ stdout/stderr/exit code (ใช้ mock ใน test) และตรวจว่ามี SDK หรือไม่ |
| `JsonFlattener` | อ่าน JSONC → `IReadOnlyDictionary<string,string>` แบบไม่สนตัวพิมพ์เล็ก/ใหญ่ ตรวจ key ซ้ำ เลือก section และเก็บคำเตือนเรื่อง null |
| `SecretsStore` | หา path ด้วย `PathHelper` อ่านไฟล์ (flatten ถ้าจำเป็น) และเขียนแบบ atomic พร้อม `.bak` และจัดการ permission บน Unix ตาม [[ADR-0009-unix-file-permissions]] |
| `ImportPlanner` | (secret เดิม, secret ใหม่, โหมด) → `ImportPlan` (diff ทีละ key + ผลลัพธ์สุดท้าย) |
| `ImportService` | ควบคุมขั้นตอนทั้งหมดแบบ 2 ช่วง ดูด้านล่าง |

## Flow แบบ 2 ช่วง (Analyze → Apply)
ทั้ง GUI และ CLI ใช้ flow เดียวกัน ความต่างอยู่ที่วิธีถาม confirm เท่านั้น

```
Analyze(projectPath, jsonPath, mode, section)
  ├─ ProjectLocator          → ต้องได้โปรเจคเดียว
  ├─ UserSecretsIdResolver   → Id หรือ null
  ├─ JsonFlattener           → incoming (มี error → หยุด)
  ├─ SecretsStore.Read(id)   → existing (ถ้ามี id)
  ├─ ImportPlanner           → diff
  └─ return ImportAnalysis { NeedsInit, CanInit, SharedWith[], SecretsPath, Plan, Warnings }

[UI/CLI] ถ้า NeedsInit → ถามเรื่อง init → InitAsync() → Analyze ใหม่
[UI/CLI] แสดงหน้า confirm (id, path, จำนวน key, diff, คำเตือน) → ผู้ใช้ตอบตกลง

Apply(analysis)
  ├─ ตรวจว่า secrets.json ไม่เปลี่ยนไปตั้งแต่ตอน Analyze (เทียบ hash) ถ้าเปลี่ยน → error ให้ Analyze ใหม่
  ├─ backup → secrets.json.bak
  └─ เขียนแบบ atomic (ไฟล์ชั่วคราว → rename)
```

## GUI (Avalonia 12.1)
- MVVM ด้วย `CommunityToolkit.Mvvm` + DI ด้วย `Microsoft.Extensions.DependencyInjection` และใช้ compiled bindings
- หน้าจอเดียวแบบ wizard: เลือกโปรเจค → เลือกไฟล์ JSON (+ section) → ดู diff / confirm → ผลลัพธ์
- ตาราง diff ซ่อนค่า secret ไว้ มีปุ่มกดดูทีละแถว
- recent projects เก็บใน settings ของแอปตามโฟลเดอร์มาตรฐานของแต่ละ OS (`Environment.SpecialFolder.ApplicationData`)

## CLI
```
secret-import --project <path> --file <json|->
              [--mode overwrite|skip|replace] [--section <path>]
              [--dry-run] [--yes] [--init] [--show-values]
```
- parse argument ด้วย `System.CommandLine` 2.0 ส่วนตาราง, prompt และการตรวจ non-interactive ใช้ `Spectre.Console`
- `--file -` อ่านจาก stdin และต้องใส่ `--yes` ถ้าไม่ใช่ `--dry-run`
- exit codes: `0` สำเร็จ / `1` input ผิด / `2` ยกเลิกหรือต้อง confirm / `3` I/O หรือ SDK error ดูรายละเอียดที่ [[ADR-0008-cli-contract]]
- แจกจ่ายเป็น `.nupkg` บน GitHub Releases ([[ADR-0010-distribution]])

ดูการตัดสินใจทั้งหมดได้ที่ [[02-Decisions/README|Decisions]]
