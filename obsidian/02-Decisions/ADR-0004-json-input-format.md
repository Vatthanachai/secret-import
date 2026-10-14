---
status: accepted
date: 2026-10-14
tags: [adr]
---
# ADR-0004: รูปแบบไฟล์ JSON ที่รับเข้า

## Decision
รับ JSON ทุกรูปแบบที่ root เป็น object แล้ว flatten ตามกติกาของ .NET configuration:
- object ซ้อนกัน → `Parent:Child`
- array → `Items:0`, `Items:1`, …
- number / bool → แปลงเป็น string ที่ invariant culture (`5432`, `true`)
- `null` → ข้ามไปและขึ้นคำเตือน
- string ว่าง `""` → import ตามปกติ
- รองรับ comment และ trailing comma (`JsonCommentHandling.Skip`, `AllowTrailingCommas`)
- **key ซ้ำกันหลัง flatten** เช่น `{"A":{"B":1},"A:B":2}` → แจ้ง error แล้วไม่ import อะไรเลย ตรวจโดยไม่สนตัวพิมพ์เล็ก/ใหญ่ (configuration ของ .NET ไม่สนตัวพิมพ์เล็ก/ใหญ่)
- `--section <path>` (เช่น `ConnectionStrings`) เลือก import เฉพาะ section นั้น
  - key ที่ได้ยังคงมีชื่อ section นำหน้า (`ConnectionStrings:Default`)
  - ถ้าไม่เจอ section → error
- root ไม่ใช่ object (เป็น array หรือ primitive) → error

## Consequences
- ก๊อป section จาก `appsettings.json` มาวางได้เลย
- `JsonFlattener` เป็นส่วนที่ต้องมี unit test มากที่สุด
