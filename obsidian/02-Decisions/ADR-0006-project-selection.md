---
status: accepted
date: 2026-10-14
tags: [adr]
---
# ADR-0006: การเลือกโปรเจคเป้าหมาย

## Decision
- ประเภทโปรเจคที่รองรับ: `.csproj`, `.fsproj`, `.vbproj` (user-secrets ใช้ได้กับโปรเจค SDK-style ทุกภาษา)
- สิ่งที่ผู้ใช้ชี้มาได้: ไฟล์โปรเจค, โฟลเดอร์ หรือ `.sln` / `.slnx`
  - โฟลเดอร์: ค้นหาไฟล์โปรเจคทั้ง 3 นามสกุลแบบ recursive โดยข้าม `bin/`, `obj/`, `.git/`, `node_modules/`
  - solution: อ่านด้วย `Microsoft.VisualStudio.SolutionPersistence` (1.0.x) ซึ่งเป็น library ทางการที่รองรับทั้ง `.sln` และ `.slnx` ไม่ต้องมี SDK
    - เลือกเฉพาะไฟล์โปรเจค 3 นามสกุลข้างบน ส่วน solution folder และโปรเจคประเภทอื่น (เช่น `.sqlproj`) ข้ามไป
    - โปรเจคที่ระบุไว้ใน solution แต่ไม่มีไฟล์อยู่จริง → ขึ้นคำเตือนแล้วข้ามไป
  - ไม่ใช้ `dotnet sln list` เพราะต้องมี SDK (ขัดกับ [[ADR-0002-secrets-access-strategy]])
- แสดงสถานะของแต่ละโปรเจค: ✅ init แล้ว / ⚠️ ยังไม่ init / 🔗 ใช้ id ร่วมกับโปรเจคอื่นในชุดเดียวกัน
- import ได้ **ทีละโปรเจค** ในเวอร์ชันแรก
- GUI จำรายการโปรเจคที่เพิ่งใช้ (recent) ไว้ในไฟล์ settings ของแอป
- CLI: `--project` ถ้าชี้แล้วเจอโปรเจคมากกว่าหนึ่งตัว → แจ้ง error พร้อมรายการโปรเจค ไม่ถามแบบ interactive

## Consequences
- id ร่วมกันแปลว่าใช้ `secrets.json` ไฟล์เดียวกัน ต้องเตือนในหน้า confirm
- import หลายโปรเจคพร้อมกันยกไปไว้ backlog
