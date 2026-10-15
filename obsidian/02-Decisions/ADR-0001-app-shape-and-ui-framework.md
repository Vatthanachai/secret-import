---
status: accepted
date: 2026-10-14
tags: [adr]
---
# ADR-0001: รูปแบบแอป — Core + Avalonia GUI + CLI

## Context
เครื่องมือ import ไฟล์ JSON เข้า `dotnet user-secrets` ต้องใช้ได้บน Windows, macOS และ Linux และต้องมีทั้งหน้าจอ GUI และ CLI
.NET MAUI ไม่รองรับ Linux อย่างเป็นทางการ และข้อดีหลักของ MAUI คือรองรับมือถือ ซึ่งเครื่องมือนี้ไม่ได้ใช้ เพราะ user-secrets มีเฉพาะบนเครื่องนักพัฒนา

## Decision
- แยก logic ทั้งหมดไว้ใน `SecretImport.Core` (class library ธรรมดา ไม่ผูกกับ UI)
- GUI ใช้ **Avalonia UI** + MVVM ตัวเดียวรันได้ทั้ง 3 OS
- CLI เป็น `dotnet tool` ชื่อ `secret-import`
- GUI และ CLI ใช้ Core ตัวเดียวกัน ไม่มี logic ซ้ำกันในสองที่

## Consequences
- ไม่ใช้ MAUI แล้ว เอกสารที่เคยเขียนว่า MAUI ต้องแก้ตาม
- Core test ได้โดยไม่ต้องมี UI
- วันหลังจะเพิ่ม frontend อื่นก็เพิ่มได้ง่าย
