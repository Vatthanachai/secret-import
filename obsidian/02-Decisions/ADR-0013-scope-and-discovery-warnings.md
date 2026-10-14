---
status: accepted
date: 2026-10-14
tags: [adr, contract]
---
# ADR-0013: Scope สำหรับตรวจ id ร่วม และคำเตือนจาก Discover

## Context
พบระหว่าง QA ของ Phase 1–5 ([[Import-JSON-to-User-Secrets#Open questions]]):
- `AnalyzeAsync` เห็นแค่โปรเจคเดียว `ProjectInfo.SharedWith` จึงว่างเสมอ และคำเตือน `SharedSecretsId` ไม่ขึ้นในหน้า confirm ซึ่งขัดกับ [[ADR-0005-init-and-confirm-flow]]
- `DiscoverProjectsAsync` คืนได้แค่รายการโปรเจค คำเตือน `MissingProjectFile` จาก solution จึงหายไป

## Decision
**Contract ใหม่ (breaking change ของ public contract ใน Core)**
```csharp
public sealed record ImportRequest(string ProjectPath, JsonInput Json, ImportMode Mode, string? Section, string? ScopePath = null);
public sealed record DiscoveryResult(IReadOnlyList<ProjectInfo> Projects, IReadOnlyList<ImportWarning> Warnings);

Task<DiscoveryResult> DiscoverProjectsAsync(string path, CancellationToken ct = default);
```

**พฤติกรรมใน Core**
- `ScopePath` (โฟลเดอร์ หรือ `.sln`/`.slnx`): ตอน Analyze ให้ค้นหาโปรเจคทั้งหมดใน scope เติม `SharedWith` ของโปรเจคเป้าหมาย และถ้าไม่ว่างให้เพิ่มคำเตือน `SharedSecretsId` (เทียบ id แบบไม่สนตัวพิมพ์เล็ก/ใหญ่)
  - โปรเจคเป้าหมายไม่อยู่ใน scope → ไม่ error ใช้แค่โปรเจคที่เจอใน scope มาเทียบ
  - `ScopePath` ที่ไม่มีอยู่จริง → `ImportException(InvalidInput)`
  - คำเตือนที่เกิดจากการค้นหาใน scope (เช่น `MissingProjectFile`) ให้รวมไว้ใน `ImportAnalysis.Warnings` ด้วย
  - ถ้า `ScopePath` เป็น `null` → ทำงานเหมือนเดิม ไม่ตรวจ id ร่วม
- `DiscoverProjectsAsync` คืน `DiscoveryResult` ที่มี `Warnings` จากการอ่าน solution หรือโฟลเดอร์

**Frontends**
- **Desktop**: ส่ง path ที่ผู้ใช้ใช้ตอน Discover (โฟลเดอร์ หรือ solution) เป็น `ScopePath` และแสดง `DiscoveryResult.Warnings` ในขั้น Project
- **CLI**: เพิ่ม option `--scope <path>`
  - ถ้าไม่ระบุและ `--project` เป็นโฟลเดอร์หรือ solution → ใช้ค่านั้นเป็น scope
  - ถ้า `--project` เป็นไฟล์โปรเจค → หา `.sln`/`.slnx` ที่ใกล้ที่สุดในโฟลเดอร์แม่ขึ้นไป ถ้าเจอมากกว่าหนึ่งไฟล์ในโฟลเดอร์เดียวกัน หรือไม่เจอเลย → ไม่ใช้ scope
  - การหา solution อัตโนมัติอยู่ใน Cli เพราะเป็นนโยบายของ CLI ไม่ใช่ของ Core

## Consequences
- ต้องแก้ทั้ง Core, Cli, Desktop และ fake ใน test ทั้งหมดพร้อมกันใน bugfix เดียว
- `ImportRequest` ที่สร้างแบบเดิม (4 argument) ยัง compile ได้ เพราะ `ScopePath` มีค่าเริ่มต้น
