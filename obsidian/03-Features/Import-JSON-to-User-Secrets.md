---
status: planned
created: 2026-10-14
platforms: [windows, macos, linux]
tags: [feature]
---
# Import JSON → dotnet user-secrets

## Goal
นำเข้าค่าจากไฟล์ JSON เข้า user-secrets ของโปรเจค .NET ที่เลือก
ก่อน import ต้องตรวจว่าโปรเจค init user-secrets แล้วหรือยัง ถ้าแล้วให้ **confirm ก่อนเริ่ม import**

## User flow
1. เลือกโปรเจค (`.csproj`, โฟลเดอร์ หรือ solution) ถ้าเจอหลายโปรเจคให้เลือกหนึ่งตัวจากรายการที่แสดงสถานะ ✅ / ⚠️ / 🔗
2. เลือกไฟล์ JSON และ section (ไม่บังคับ) ถ้า parse หรือ flatten ไม่ผ่าน ให้แสดง error ตรงนี้
3. ถ้ายังไม่ init → ถามว่า "จะรัน `dotnet user-secrets init` ไหม" (ถ้าไม่มี SDK จะ init ไม่ได้)
4. หน้า confirm: id, path, จำนวน key, เลือกโหมด (overwrite / skip / replace), ตาราง diff, คำเตือน (id ร่วม / null / ใช้ XML แทน msbuild)
5. กด Import → สร้าง backup → เขียนไฟล์ → แสดงผลสรุป (+N, ~N, -N) และ path ของ backup

## Acceptance criteria
- [ ] ตรวจเจอ `UserSecretsId` ที่กำหนดใน `.csproj` และใน `Directory.Build.props`
- [ ] โปรเจคที่ init แล้ว ต้องผ่านหน้า confirm ก่อนเขียนทุกครั้ง (CLI ข้ามได้ด้วย `--yes` เท่านั้น)
- [ ] โปรเจคที่ยังไม่ init ต้องถามก่อน init (CLI ข้ามได้ด้วย `--init`)
- [ ] ผลการ flatten ตรงกับ `ConfigurationBuilder().AddJsonFile()` ทุกกรณีใน test
- [ ] key ซ้ำกันหลัง flatten → error และไม่เขียนอะไรเลย
- [ ] โหมดทั้ง 3 ให้ผลตามตารางใน [[ADR-0003-import-merge-modes]]
- [ ] มี `.bak` ทุกครั้งก่อนเขียน และเขียนแบบ atomic
- [ ] `--dry-run` ไม่แก้ไฟล์ใดๆ เลย
- [ ] ค่า secret ไม่แสดงใน log หรือ output จนกว่าผู้ใช้จะขอดู
- [ ] ทำงานได้บน Windows, macOS และ Linux (path ตาม [[04-Platforms/User-Secrets-Paths]])

## Open questions (TBC)
- เวอร์ชัน .NET: เสนอ `net10.0` (LTS)
- เวอร์ชัน Avalonia, MVVM toolkit และ CLI library
- วิธีแจกจ่าย GUI: self-contained publish ต่อ OS, ทำ installer หรือไม่
- รองรับ import แบบ pipe เข้า stdin (`--file -`) หรือไม่
- ตั้ง permission `600` ให้ `secrets.json` บน Unix หรือไม่

## Backlog (หลังเวอร์ชันแรก)
- import เข้าหลายโปรเจคพร้อมกัน
- เลือก merge ทีละ key
- export secrets → JSON
- เก็บ backup หลายเวอร์ชัน

Plan: [[Implementation-Plan]]
