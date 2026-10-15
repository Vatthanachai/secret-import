---
status: accepted
date: 2026-10-14
tags: [adr, security]
---
# ADR-0009: permission ของไฟล์ secrets บน Unix

## Context
- `secrets.json` เก็บเป็น plain text ถ้าสร้างตาม umask มักได้ `644` แปลว่าผู้ใช้อื่นในเครื่องอ่านได้
- การเขียนแบบ atomic (ไฟล์ชั่วคราวแล้ว rename) ทำให้ permission ของไฟล์ใหม่มาจากไฟล์ชั่วคราว ไม่ได้ติดมาจากไฟล์เดิม
- .NET มี `FileStreamOptions.UnixCreateMode` และ `File.SetUnixFileMode` ให้ใช้

## Decision (macOS / Linux)
- **ไฟล์ชั่วคราว** สร้างด้วย `600` เสมอ ผ่าน `UnixCreateMode` จึงไม่มีช่วงเวลาที่ไฟล์เปิดให้คนอื่นอ่าน
- **ถ้ามี `secrets.json` อยู่แล้ว** ก่อน rename ให้ตั้ง permission ของไฟล์ชั่วคราวเป็นค่าเดียวกับไฟล์เดิม
- **ถ้าเป็นไฟล์ใหม่** ใช้ `600` และถ้าเราต้องสร้างโฟลเดอร์ `<id>/` เองให้ใช้ `700`
- **`.bak`** ใช้ permission เดียวกับไฟล์ต้นฉบับ
- ถ้าไฟล์เดิมให้ group หรือ other อ่านได้ → ขึ้นคำเตือนในหน้า confirm แต่ **ไม่เปลี่ยน permission ให้เอง**

## Decision (Windows)
- ไม่ต้องทำอะไรเพิ่ม ใช้ ACL ที่ได้จาก `%APPDATA%` ซึ่งจำกัดสิทธิ์เฉพาะผู้ใช้อยู่แล้ว

## Rejected
- บังคับ `600` ทุกครั้ง: จะไปเปลี่ยน permission ที่ผู้ใช้ตั้งไว้เอง
- ไม่ยุ่งเรื่อง permission: การเขียนแบบ atomic อาจทำให้ไฟล์ถูกเปิดสิทธิ์เพิ่มขึ้นโดยไม่ตั้งใจ
