---
status: accepted
date: 2026-10-14
tags: [adr, process]
---
# ADR-0012: Git flow และ QA gate ก่อน merge เข้า develop

## Context
repo นี้ init git flow ไว้แล้วด้วย **git-flow-next 2.0** (as of 2026-10-14)
- `main` เป็น production และ `develop` เป็น branch รวมงาน
- `feature/*` และ `bugfix/*` แตกออกจาก `develop` และ merge กลับเข้า `develop`
- `release/*` แตกออกจาก `develop` แล้ว merge เข้า `main` (สร้าง tag ให้)
- `hotfix/*` แตกออกจาก `main`

## Decision
**แยก branch และ merge ด้วยคำสั่ง git flow เท่านั้น** ห้ามสร้างหรือ merge branch เองด้วย `git checkout -b` หรือ `git merge`
| งาน | คำสั่ง |
|---|---|
| เริ่ม feature | `git flow feature start <name>` |
| ปิด feature (หลังผ่าน QA) | `git flow feature finish <name> --no-ff` |
| แก้ bug บน develop | `git flow bugfix start/finish <name>` |
| เตรียม release | `git flow release start <ver>` → `git flow release finish <ver> -T v<ver>` (tag `v<ver>` จะเป็นตัวสั่งให้ `release.yml` ทำงาน) |
| แก้ด่วนบน main | `git flow hotfix start/finish <ver>` |

**Worktree สำหรับ agent ที่ทำงานพร้อมกัน**
1. QA (เซสชันหลัก) รัน `git flow feature start <name>` แล้ว `git checkout develop` เพื่อให้ worktree หลักยังอยู่บน `develop`
2. `git worktree add ../net-secret-import.wt/<name> feature/<name>` แล้วส่ง path นี้ให้ agent
3. agent commit ใน worktree ของตัวเองเท่านั้น ห้ามรัน `git flow ... finish`, ห้าม merge, ห้ามสลับ branch
4. เมื่อผ่าน QA: `git worktree remove ../net-secret-import.wt/<name>` แล้ว `git flow feature finish <name> --no-ff`
5. feature ที่ยังทำอยู่ให้ rebase ตาม `develop` ด้วย `git flow feature update` หรือ `git rebase develop` ใน worktree ของ feature นั้น

**ชื่อ feature**
- `p0-scaffold`
- `p1-5-core`
- `p6-cli`
- `p7-desktop`
- `p8-release-ci`
- งานเอกสารใช้ `docs-<topic>`

**การแบ่งงาน**
1. `p0-scaffold` (scaffold + public contracts ของ Core) ต้อง finish เข้า `develop` ให้เสร็จก่อน
2. หลังจากนั้นทำพร้อมกันได้ เพราะแต่ละ agent แก้คนละไฟล์:
   - `core-tdd` → `p1-5-core`
   - `cli-builder` → `p6-cli`
   - `avalonia-ui` → `p7-desktop`
   - `release-ci` → `p8-release-ci`
   - Cli และ Desktop เขียนโค้ดเทียบกับ interface จาก Phase 0 และใช้ fake ไปก่อน
3. ถ้าต้องเปลี่ยน public contract ของ Core ให้ทำใน feature แยก (`core-tdd`) ผ่าน QA แล้ว finish ก่อน จากนั้น feature อื่นค่อย rebase ตาม

**QA gate** (Claude ในเซสชันหลัก) ตรวจทุก feature ก่อน finish:
1. `dotnet build -c Release` ต้องไม่มี warning
2. `dotnet test -c Release -p:CollectCoverage=true` ทุก test ต้องผ่าน และ line/branch ≥ 90% ([[ADR-0011-testing-and-coverage]])
3. code review: ตรวจกับ ADR, library boundary, ห้ามมีค่า secret ใน log หรือ output, ดูว่า test มี assertion จริงไม่ใช่แค่ดัน coverage, และเหตุผลของไฟล์ที่ไม่นับใน coverage
4. ผ่าน → `git flow feature finish <name> --no-ff` แล้วติ๊ก checkbox ในแผนงาน
5. ไม่ผ่าน → ส่งรายการสิ่งที่ต้องแก้กลับไปให้ agent เดิม และยังไม่ finish

## Notes
- repo ยังไม่มี remote ทุกอย่างทำในเครื่องและใช้ `--no-push`
- ห้ามทำ `release` หรือ `hotfix` และห้ามแตะ `main` จนกว่าผู้ใช้จะสั่ง
