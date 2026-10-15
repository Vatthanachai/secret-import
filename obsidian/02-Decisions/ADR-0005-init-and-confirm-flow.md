---
status: accepted
date: 2026-10-14
tags: [adr]
---
# ADR-0005: การ init และการ confirm ก่อน import

## Decision
**โปรเจคที่ init แล้ว (มี `UserSecretsId`)** → ต้อง confirm ก่อน import ทุกครั้ง หน้า confirm แสดง:
- `UserSecretsId` และ path ของ `secrets.json`
- จำนวน key ที่มีอยู่เดิม และจำนวน key ที่จะ import
- diff ตามโหมดที่เลือก (ดู [[ADR-0003-import-merge-modes]])
- คำเตือนถ้ามีโปรเจคอื่นใช้ id เดียวกัน (ดู [[ADR-0006-project-selection]])

**โปรเจคที่ยังไม่ init** → ถามว่า "จะ init ให้ไหม" ถ้าตอบใช่ ระบบจะรัน `dotnet user-secrets init` (ซึ่งแก้ `.csproj`) แล้วไปที่หน้า confirm ต่อ
- ถ้าไม่มี SDK → init ไม่ได้ ให้แจ้ง error พร้อมบอกคำสั่งที่ผู้ใช้ต้องไปรันเอง

**CLI**
- `--yes` ข้ามการ confirm
- `--init` อนุญาตให้ init โดยไม่ถาม
- ถ้าไม่มี `--yes` และรันแบบ non-interactive (stdin ถูก redirect) → แจ้ง error ไม่ต้องรอคำตอบ

## Rejected
- init ให้อัตโนมัติโดยไม่ถาม: ไม่เลือก เพราะการแก้ `.csproj` ควรได้รับความยินยอมจากผู้ใช้
- หยุดแล้วให้ไป init เอง: ไม่เลือก เพราะเพิ่มขั้นตอนโดยไม่จำเป็น
