---
status: accepted
date: 2026-10-15
tags: [adr, contract, cli]
---
# ADR-0015: Export secrets → JSON

## Context
อยู่ใน backlog ของ [[Import-JSON-to-User-Secrets#Backlog (หลังเวอร์ชันแรก)]] ใช้ย้าย secrets ไปเครื่องอื่น สำรองไว้ หรือก๊อปกลับไปวางใน `appsettings.json` ค่าใน `secrets.json` เก็บแบบ flat (`Db:Conn`) และเป็น string ทั้งหมด

## Decision

### ขอบเขต
- รอบนี้ทำ Core และ CLI ก่อน ส่วนปุ่ม Export ใน Desktop เป็น feature ถัดไป ใช้ contract เดียวกันนี้

### Core contract (เพิ่มเข้าไป ไม่ breaking)
```csharp
public sealed record ExportRequest(string ProjectPath, string? Section = null, bool Flat = false);
public sealed record ExportResult(ProjectInfo Project, string SecretsPath, int KeyCount, string Json, IReadOnlyList<ImportWarning> Warnings);

public interface IImportService
{
    // ...ของเดิม
    Task<ExportResult> ExportAsync(ExportRequest request, CancellationToken ct = default);
    Task WriteExportAsync(string path, string json, bool overwrite, CancellationToken ct = default);
}
```
- `ExportAsync` หาโปรเจคด้วยกติกาเดียวกับ import ([[ADR-0006-project-selection]]) และอ่าน `secrets.json` ผ่าน `SecretsStore` ไม่เขียนอะไรเลย
- โปรเจคที่ยังไม่มี `UserSecretsId` → `ImportException(InvalidInput)` พร้อมข้อความว่ายังไม่ได้ init
- มี id แต่ยังไม่มี `secrets.json` → `Json` = `{}`, `KeyCount` = 0
- คำเตือนที่ได้ตอนอ่าน (เช่น `InsecureFilePermissions`) ส่งต่อใน `Warnings`

### รูปแบบ JSON
- **ค่าเริ่มต้นเป็น nested** แตก key ด้วย `:` กลับเป็น object ซ้อน
  - กลุ่มลูกที่ key เป็น `0..n-1` ครบและต่อเนื่อง → array นอกนั้นเป็น object
  - key ที่ต่างกันแค่ตัวพิมพ์เล็ก/ใหญ่ถือเป็นตัวเดียวกัน (configuration ของ .NET ไม่สนตัวพิมพ์) ใช้ตัวสะกดของ key แรกที่เจอ
  - **key ชนกัน** เช่นมีทั้ง `A` (ค่า) และ `A:B` → `ImportException(InvalidInput)` ข้อความบอกชื่อ key ที่ชน (ไม่บอกค่า) และแนะนำ `--flat`
- **`Flat = true`** เขียน key แบบ `A:B` ตามที่เก็บจริง ไม่มีกรณีชน
- ค่าทุกตัวเป็น JSON string ลำดับ key ตามลำดับใน `secrets.json`
- System.Text.Json แบบ indented 2 ช่อง, `UnsafeRelaxedJsonEscaping`, UTF-8 ไม่มี BOM, ขึ้นบรรทัดใหม่ท้ายไฟล์
- ผลที่ได้ต้อง import กลับด้วย `secret-import` แล้วได้ค่าเท่าเดิม (round-trip test)

### `Section`
- กติกาเดียวกับ import ([[ADR-0004-json-input-format]]) คือ path แบบ `A:B` ไม่สนตัวพิมพ์ key ที่ได้ยังมีชื่อ section นำหน้า และถ้าไม่เจอ section → `InvalidInput`

### การเขียนไฟล์ (`WriteExportAsync`)
- ไฟล์มีอยู่แล้วและ `overwrite = false` → `ImportException(InvalidInput)` ไม่แตะไฟล์เดิม
- เขียนแบบ atomic (temp file + rename) ไม่ทำ `.bak`
- Unix: ไฟล์ใหม่สร้างเป็น `600` ถ้าเขียนทับให้ใช้ mode ของไฟล์เดิม ตาม [[ADR-0009-unix-file-permissions]]
- เขียนไม่ได้ → `IoFailure`

### CLI
```
secret-import export --project <path> --output <file|-> [--section <path>] [--flat] [--force]
```
- เป็น subcommand ใหม่ ส่วน import ยังเรียกแบบเดิม (`secret-import --project … --file …`) ไม่เปลี่ยน
- `--output` **ต้องใส่เสมอ** ใส่ `-` ถึงจะพิมพ์ JSON ลง stdout เพื่อไม่ให้ค่า secret ขึ้นจอหรือเข้า log โดยไม่ตั้งใจ
- `--force` อนุญาตให้เขียนทับไฟล์เดิม
- เขียนลงไฟล์: stdout แสดงสรุป (โปรเจค, จำนวน key, path) ไม่มีค่า secret
- `--output -`: stdout มีแค่ JSON ส่วนคำเตือนและ error ไป stderr
- exit code ใช้ตารางของ [[ADR-0008-cli-contract]]: `0` สำเร็จ, `1` input ผิด (key ชน, ไฟล์มีอยู่แล้ว, ไม่เจอ section, ยังไม่ init, หลายโปรเจค), `3` I/O
- ไม่ถาม confirm และไม่มี `--json` (output ก็เป็น JSON อยู่แล้ว)

## Consequences
- `IImportService` มี 2 method ใหม่ Cli และ Desktop ที่ fake service ต้องเพิ่ม stub
- Overview, spec, readme และ `AGENTS.md` ต้องเพิ่ม export
- ไฟล์ที่ export ออกมามีค่า secret เป็น plain text เป็นหน้าที่ของผู้ใช้ที่จะเก็บให้ปลอดภัย readme ต้องเตือนเรื่องนี้
