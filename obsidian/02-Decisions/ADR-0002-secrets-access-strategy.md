---
status: accepted
date: 2026-10-14
tags: [adr]
---
# ADR-0002: วิธีตรวจ UserSecretsId และอ่าน/เขียน secrets

## Context
- `UserSecretsId` อาจอยู่ใน `.csproj`, `Directory.Build.props` หรือใต้ `Condition` ก็ได้ ถ้าอ่าน XML อย่างเดียวจะพลาดบางกรณี
- `dotnet msbuild <proj> -getProperty:UserSecretsId` (SDK 8+) คืนค่าที่ MSBuild ประมวลผลครบแล้ว
- `secrets.json` เป็นไฟล์ JSON ธรรมดาที่อยู่ใน path ที่รู้ล่วงหน้า
- ถ้าใช้ `dotnet user-secrets set` ทีละ key จะช้า เขียนแบบ atomic ไม่ได้ และทำโหมด replace หรือ backup ได้ยาก

## Decision
แบบผสม:
1. **ตรวจ id**: ใช้ `dotnet msbuild -getProperty:UserSecretsId` ก่อน ถ้าไม่มี SDK หรือรันไม่สำเร็จ ให้ถอยไปอ่าน XML ของ `.csproj` และ `Directory.Build.props` ที่อยู่ในโฟลเดอร์แม่
2. **Init**: ใช้ `dotnet user-secrets init --project <proj>` ให้เครื่องมือทางการเป็นคนแก้ `.csproj`
3. **อ่าน/เขียน `secrets.json` เอง**:
   - หา path ด้วย `PathHelper.GetSecretsPathFromSecretsId(id)` จาก package `Microsoft.Extensions.Configuration.UserSecrets` เพื่อให้ได้ path ตรงกับ runtime (ดู [[04-Platforms/User-Secrets-Paths]])
   - เขียนไฟล์ชั่วคราวก่อนแล้วค่อย rename แทนไฟล์เดิม (atomic) และ copy ไฟล์เดิมเป็น `secrets.json.bak` ก่อนทุกครั้ง
   - ตอนอ่าน ถ้าไฟล์เดิมเป็นแบบซ้อนกัน (มีคนแก้มือ) ให้ flatten ก่อน ตอนเขียนกลับใช้ key แบนเสมอ ซึ่งเป็นรูปแบบที่ CLI ทางการใช้
4. **ถ้าไม่มี SDK**: ยัง import ได้ถ้าอ่าน XML แล้วเจอ id แต่ปิดการ init และขึ้นคำเตือนว่าอาจตรวจพลาดกรณีที่มี `Condition`

## Consequences
- ต้องมี abstraction `IDotnetCli` สำหรับรัน process เพื่อให้ test ได้
- พฤติกรรมเรื่อง path และการ init ตรงกับเครื่องมือทางการ
