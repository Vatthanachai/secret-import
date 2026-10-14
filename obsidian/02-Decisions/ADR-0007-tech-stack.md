---
status: accepted
date: 2026-10-14
tags: [adr]
---
# ADR-0007: Tech stack และเวอร์ชัน

## Context
- เครื่องพัฒนามี SDK `10.0.401` (as of 2026-10-14)
- .NET 10 เป็น LTS (support ถึง 2028-11) ส่วน .NET 8 จะหมด support ในเดือน 2026-11
- เวอร์ชันบน NuGet ณ 2026-10-14: Avalonia 12.1.4, CommunityToolkit.Mvvm 8.4.2, System.CommandLine 2.0.13 (3.0 ยังเป็น RC), Spectre.Console 0.57.2, Microsoft.VisualStudio.SolutionPersistence 1.0.52

## Decision
| เรื่อง | เลือก |
|---|---|
| Target framework | `net10.0` อย่างเดียว |
| SDK | ปักด้วย `global.json` เป็น `10.0.400` กับ `rollForward: latestFeature` |
| CLI runtime | `RollForward=Major` เพื่อให้รันบน .NET 11 ขึ้นไปได้ |
| GUI | Avalonia 12.1.x + compiled bindings |
| MVVM | CommunityToolkit.Mvvm 8.4.x (source generators) |
| DI | Microsoft.Extensions.DependencyInjection |
| CLI parsing | System.CommandLine 2.0.x (stable) |
| CLI output/prompt | Spectre.Console 0.57.x ใช้ตรวจ non-interactive ด้วย `AnsiConsole.Profile.Capabilities.Interactive` |
| Solution parsing | Microsoft.VisualStudio.SolutionPersistence 1.0.x |
| Secrets path | Microsoft.Extensions.Configuration.UserSecrets (`PathHelper`) |
| Tests | xUnit |
| Package versions | central package management (`Directory.Packages.props`) |

## Rules
- Spectre และ System.CommandLine อยู่เฉพาะใน `SecretImport.Cli` ส่วน Avalonia และ MVVM toolkit อยู่เฉพาะใน `SecretImport.Desktop`
- Core อ้างอิงได้เฉพาะ BCL, `Microsoft.Extensions.*` และ SolutionPersistence

## Rejected
- target `net8.0` คู่กับ `net10.0`: .NET 8 ใกล้หมด support ไม่คุ้มที่จะดูแลสอง target
- ReactiveUI: หนักเกินไปสำหรับหน้าจอ wizard 4 ขั้น
- System.CommandLine 3.0: ยังเป็น RC
- Spectre.Console.Cli: เลือกใช้ System.CommandLine ซึ่งเป็นมาตรฐานของ .NET แทน
