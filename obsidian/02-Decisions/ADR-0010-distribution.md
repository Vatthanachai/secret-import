---
status: accepted
date: 2026-10-14
tags: [adr]
---
# ADR-0010: การแจกจ่าย (GitHub Releases เท่านั้น)

## Decision
- แจกทุกอย่างผ่าน **GitHub Releases** ด้วย workflow `release.yml` ที่ทำงานเมื่อ push tag `v*` **ไม่ขึ้น nuget.org**
- **GUI**: self-contained single-file ต่อ RID แพ็กเป็น zip (Windows) หรือ tar.gz (macOS/Linux)
  - RID: `win-x64`, `win-arm64`, `osx-arm64`, `osx-x64`, `linux-x64`, `linux-arm64`
  - macOS: แจกเป็น `.app` bundle ที่ไม่ sign และเขียนวิธีเปิดผ่าน Gatekeeper (คลิกขวา → Open) ไว้ใน readme
  - Windows: ไม่ sign ผู้ใช้จะเจอคำเตือนจาก SmartScreen
- **CLI**: แจกไฟล์ `SecretImport.Cli.<ver>.nupkg` บน Release ติดตั้งด้วย
  `dotnet tool install -g SecretImport.Cli --add-source <โฟลเดอร์ที่ดาวน์โหลด>`

## Backlog
- sign และ notarize (Apple Developer, Windows code signing)
- installer: MSIX, `.dmg`, AppImage, `.deb`
- publish ขึ้น nuget.org
