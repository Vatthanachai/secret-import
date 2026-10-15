---
status: accepted
date: 2026-10-15
tags: [adr, cli]
---
# ADR-0014: CLI `--json` และ `--exit-code`

## Context
สอง option นี้อยู่ใน backlog ของ [[ADR-0008-cli-contract]] คือ `--json` เพื่อให้ script อ่านผลลัพธ์ได้ และ `--exit-code` เพื่อให้ CI รู้ว่า `--dry-run` เจอ diff หรือไม่ ตอนนี้ `--dry-run` คืน `0` เสมอ และ output เป็นตารางของ Spectre ซึ่ง parse ไม่ได้

## Decision

### `--exit-code`
- ใช้ได้เฉพาะคู่กับ `--dry-run` ถ้าใส่โดยไม่มี `--dry-run` → exit `1` (input ผิด) และไม่เขียนอะไรเลย
- เมื่อ `--dry-run --exit-code` แล้วเจอ diff (`ImportPlan.HasChanges`) → exit **`4`** (`ExitCodes.ChangesPending`) ถ้าไม่มี diff → `0`
- ใช้ `4` แทน `1` แบบ `git diff --exit-code` เพราะ `1` ใช้กับ input ผิดอยู่แล้ว CI จึงแยก "มี diff" ออกจาก "error" ได้
- ไม่ใส่ `--exit-code` แล้ว `--dry-run` ยังคืน `0` เสมอเหมือนเดิม

### `--json`
- พิมพ์ JSON object **เดียว** ลง stdout ทุกกรณี ทั้งสำเร็จ, dry-run, ไม่มี diff, ยกเลิก และ error ส่วน stderr ว่าง
- ไม่แสดงตาราง ข้อความ หรือ prompt ของ Spectre เลย จึงถือเป็น non-interactive เสมอ ต้องใส่ `--yes` (และ `--init` ถ้าโปรเจคยังไม่ init) เอง ถ้าไม่ใส่ → `status: "cancelled"`, exit `2`
- exit code ของ process ตรงกับ field `exitCode` และใช้ตาราง exit code เดิมของ [[ADR-0008-cli-contract]] บวก `4`
- ค่า secret ไม่อยู่ใน JSON เว้นแต่ใส่ `--show-values` จึงจะมี `oldValue` / `newValue` (เป็น `null` เมื่อไม่มีค่า)
- property เป็น camelCase, enum เป็น string camelCase, เขียนด้วย System.Text.Json แบบ indented และเขียนลง stdout ตรงๆ ไม่ผ่าน Spectre (เพื่อไม่ให้ตัดบรรทัดหรือใส่สี)
- parse error ของ System.CommandLine (option ที่ไม่รู้จัก, ขาด `--project`/`--file`) เกิดก่อนที่ `--json` จะถูกอ่าน จึงยังเป็นข้อความบน stderr และ exit `1` ตามเดิม

Schema:
```jsonc
{
  "exitCode": 0,
  "status": "applied",            // dryRun | noChanges | applied | cancelled | error
  "project": { "name": "Api", "path": "..." },   // null ถ้า error ก่อน Analyze
  "userSecretsId": "abc-123",     // null ถ้ายังไม่ init
  "secretsPath": ".../secrets.json",
  "mode": "overwrite",
  "section": null,
  "scope": null,
  "initialized": false,           // true ถ้ารอบนี้รัน dotnet user-secrets init
  "summary": { "added": 2, "changed": 1, "removed": 0, "unchanged": 3, "skipped": 0 },
  "changes": [ { "key": "Db:Conn", "kind": "added" } ],
  "warnings": [ { "kind": "sharedSecretsId", "message": "..." } ],
  "backupPath": ".../secrets.json.bak",          // เฉพาะ status applied
  "error": null                   // { "kind": "invalidInput", "message": "...", "candidates": [ { "name", "path" } ] }
}
```
`error.kind` ใช้ค่าของ `ImportErrorKind` (camelCase) บวกค่าฝั่ง CLI: `confirmationRequired` (exit 2), `cancelled` (exit 2), `invalidOptions` (exit 1) และ `ioFailure` สำหรับ `IOException` / `UnauthorizedAccessException` (exit 3)

## Consequences
- Core ไม่เปลี่ยน ทั้งหมดอยู่ใน `SecretImport.Cli`
- ตาราง exit code ใน [[ADR-0008-cli-contract]], readme และ `AGENTS.md` ต้องเพิ่ม `4`
- schema ข้างบนเป็น contract ต่อ script ภายนอก การลบหรือเปลี่ยนชื่อ field ต้องทำ ADR ใหม่ การเพิ่ม field ทำได้
