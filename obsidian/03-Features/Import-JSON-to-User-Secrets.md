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

- [ ] CLI `--file -` อ่านจาก stdin ได้ ถ้าไม่มี `--yes` (และไม่ใช่ `--dry-run`) → exit `2`
- [ ] exit codes ตาม [[ADR-0008-cli-contract]]
- [ ] Unix: ไฟล์ใหม่เป็น `600` ไฟล์เดิมใช้ permission เดิม และเตือนถ้า group/other อ่านได้ ([[ADR-0009-unix-file-permissions]])
- [ ] รองรับ `.csproj`, `.fsproj`, `.vbproj` และ `.sln`, `.slnx`

## Open questions
- ✅ แก้แล้ว (2026-10-14, [[ADR-0013-scope-and-discovery-warnings]]): คำเตือน id ร่วมตอน Analyze และคำเตือนจาก `DiscoverProjectsAsync`
- ✅ แก้แล้ว (2026-10-14, ADR-0013 Amendment 1): Desktop หา solution อัตโนมัติเหมือน CLI และเลือก `.slnx` ก่อนเมื่อมีคู่ `.sln`/`.slnx`
- **`CanInit`** เป็น true เฉพาะเมื่อ `NeedsInit` เป็น true และมี SDK ซึ่งเป็นพฤติกรรมที่ใช้อยู่ตอนนี้

## Decisions
ตัดสินใจครบทุกข้อแล้ว (as of 2026-10-14) ดู [[02-Decisions/README|Decisions]] โดยเฉพาะ [[ADR-0007-tech-stack]] และ [[ADR-0010-distribution]]

## Backlog (หลังเวอร์ชันแรก)
- import เข้าหลายโปรเจคพร้อมกัน
- เลือก merge ทีละ key
- export secrets → JSON
- เก็บ backup หลายเวอร์ชัน
- CLI `--json` และ `--exit-code`
- sign/notarize, installer และ nuget.org

Plan: [[Implementation-Plan]]
