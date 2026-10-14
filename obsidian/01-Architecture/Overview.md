---
tags: [architecture]
updated: 2026-10-14
---
# Architecture Overview

## Solution layout (แผน)
```
SecretImport.slnx
src/
  SecretImport.Core/        net10.0 class library — logic ทั้งหมด
  SecretImport.Cli/         net10.0 dotnet tool `secret-import`
  SecretImport.Desktop/     Avalonia app (Windows / macOS / Linux)
tests/
  SecretImport.Core.Tests/  xUnit
```
ทิศทางการอ้างอิง: `Cli` → `Core` ← `Desktop` และ Core ห้ามอ้างอิง UI framework ใดๆ

## Core components
| Component | หน้าที่ |
|---|---|
| `ProjectLocator` | รับ path ที่เป็น `.csproj`, โฟลเดอร์ หรือ `.sln/.slnx` → คืนรายการ `ProjectRef` |
| `UserSecretsIdResolver` | เรียก `dotnet msbuild -getProperty:UserSecretsId` ถ้าไม่สำเร็จถอยไปอ่าน XML → คืน `SecretsIdResult { Id?, Source: MsBuild \| XmlFallback }` |
| `SecretsInitializer` | เรียก `dotnet user-secrets init --project` |
| `IDotnetCli` | รัน `dotnet` และเก็บ stdout/stderr/exit code (ใช้ mock ใน test) และตรวจว่ามี SDK หรือไม่ |
| `JsonFlattener` | อ่าน JSONC → `IReadOnlyDictionary<string,string>` แบบไม่สนตัวพิมพ์เล็ก/ใหญ่ ตรวจ key ซ้ำ เลือก section และเก็บคำเตือนเรื่อง null |
| `SecretsStore` | หา path ด้วย `PathHelper` อ่านไฟล์ (flatten ถ้าจำเป็น) และเขียนแบบ atomic พร้อม `.bak` |
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

## GUI (Avalonia)
- MVVM ด้วย `CommunityToolkit.Mvvm` (TBC)
- หน้าจอเดียวแบบ wizard: เลือกโปรเจค → เลือกไฟล์ JSON (+ section) → ดู diff / confirm → ผลลัพธ์
- ตาราง diff ซ่อนค่า secret ไว้ มีปุ่มกดดูทีละแถว
- recent projects เก็บใน settings ของแอปตามโฟลเดอร์มาตรฐานของแต่ละ OS (`Environment.SpecialFolder.ApplicationData`)

## CLI
```
secret-import --project <path> --file <json>
              [--mode overwrite|skip|replace] [--section <path>]
              [--dry-run] [--yes] [--init] [--show-values]
```
- library: `System.CommandLine` (TBC)
- exit codes: `0` สำเร็จ / `1` input ผิด / `2` ผู้ใช้ยกเลิก / `3` I/O หรือ SDK error (TBC)

ดูการตัดสินใจทั้งหมดได้ที่ [[02-Decisions/README|Decisions]]
