---
status: accepted
date: 2026-10-14
tags: [adr]
---
# ADR-0008: CLI contract — options, stdin, exit codes

## Options
```
secret-import --project <path> --file <json|-> [--mode overwrite|skip|replace]
              [--section <path>] [--scope <path>] [--dry-run] [--yes] [--init] [--show-values]
```
`--scope` ใช้ตรวจ id ร่วม ([[ADR-0013-scope-and-discovery-warnings]]) ถ้าไม่ระบุจะเลือก scope ให้อัตโนมัติ

## stdin (`--file -`)
- อ่าน JSON จาก stdin เพื่อให้ใช้กับ pipe ได้โดยไม่ต้องเขียน secret ลง disk (`op read … | secret-import --file - …`)
- ถ้า stdin ถูกใช้รับ JSON แล้วจะใช้ stdin ถาม confirm ไม่ได้ จึงต้องใส่ `--yes` (และ `--init` ถ้าโปรเจคยังไม่ init) ถ้าไม่ใส่ → exit `2` พร้อมข้อความบอกวิธีใช้
- `--dry-run` ใช้กับ stdin ได้โดยไม่ต้องมี `--yes`
- ไม่อ่านคำตอบจาก TTY โดยตรง (`/dev/tty`, `CONIN$`)

## Exit codes
| Code | ความหมาย | ตัวอย่าง |
|---|---|---|
| `0` | สำเร็จ หรือไม่มีอะไรเปลี่ยน | import เสร็จ / `--dry-run` (คืน 0 เสมอ ไม่ว่าจะมี diff หรือไม่) / ไม่มี diff |
| `1` | input ผิด | parse JSON ไม่ได้, key ซ้ำ, ไม่เจอ section, เจอหลายโปรเจค, option ผิด |
| `2` | ผู้ใช้ยกเลิก หรือต้อง confirm แต่ไม่มี `--yes` | ตอบ "n", รันแบบ non-interactive หรืออ่านจาก stdin โดยไม่มี `--yes` หรือ `--init` |
| `3` | สภาพแวดล้อมหรือ I/O ผิดพลาด | ไม่มี SDK ตอนที่ต้อง init, เขียนไฟล์ไม่ได้, `secrets.json` เปลี่ยนไประหว่าง Analyze กับ Apply |

## Backlog
- `--json` สำหรับให้ script อ่านผลลัพธ์แบบ machine-readable
- `--exit-code` สำหรับให้ `--dry-run` คืน code แยกเมื่อมี diff (ใช้ใน CI)
