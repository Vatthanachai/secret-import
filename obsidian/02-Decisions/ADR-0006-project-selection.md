---
status: accepted
date: 2026-10-14
tags: [adr]
---
# ADR-0006: การเลือกโปรเจคเป้าหมาย

## Decision
- สิ่งที่ผู้ใช้ชี้มาได้: `.csproj`, โฟลเดอร์ หรือ `.sln` / `.slnx`
  - โฟลเดอร์: ค้นหา `*.csproj` แบบ recursive โดยข้าม `bin/`, `obj/`, `.git/`, `node_modules/`
  - solution: อ่านรายการโปรเจคจากไฟล์ solution ใช้ `Microsoft.VisualStudio.SolutionPersistence` ซึ่งรองรับทั้ง `.sln` และ `.slnx` (TBC)
- แสดงสถานะของแต่ละโปรเจค: ✅ init แล้ว / ⚠️ ยังไม่ init / 🔗 ใช้ id ร่วมกับโปรเจคอื่นในชุดเดียวกัน
- import ได้ **ทีละโปรเจค** ในเวอร์ชันแรก
- GUI จำรายการโปรเจคที่เพิ่งใช้ (recent) ไว้ในไฟล์ settings ของแอป
- CLI: `--project` ถ้าชี้แล้วเจอโปรเจคมากกว่าหนึ่งตัว → แจ้ง error พร้อมรายการโปรเจค ไม่ถามแบบ interactive

## Consequences
- id ร่วมกันแปลว่าใช้ `secrets.json` ไฟล์เดียวกัน ต้องเตือนในหน้า confirm
- import หลายโปรเจคพร้อมกันยกไปไว้ backlog
