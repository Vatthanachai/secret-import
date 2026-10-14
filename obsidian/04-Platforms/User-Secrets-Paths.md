---
tags: [platform]
updated: 2026-10-14
---
# User-secrets paths ของแต่ละ OS

ให้ใช้ `PathHelper.GetSecretsPathFromSecretsId(id)` จาก `Microsoft.Extensions.Configuration.UserSecrets` เสมอ ไม่ต้องคำนวณ path เอง

ลำดับการหา root ของ `PathHelper` (as of .NET 8–10, inferred — ให้ตรวจกับ source ของ `PathHelper` อีกครั้งตอน Phase 3):
1. `APPDATA` → `%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json` (Windows)
2. `HOME` → `~/.microsoft/usersecrets/<id>/secrets.json` (macOS / Linux)
3. `Environment.SpecialFolder.ApplicationData` / `UserProfile`
4. `DOTNET_USER_SECRETS_FALLBACK_DIR` (ใช้เมื่อหา root แบบอื่นไม่ได้เลย เช่นใน container)

ถ้าได้ root จาก `APPDATA` จะใช้โฟลเดอร์ย่อย `Microsoft/UserSecrets` ถ้ามาจากแหล่งอื่นจะใช้ `.microsoft/usersecrets` (ตัวพิมพ์เล็ก ซึ่งสำคัญบน Linux)

## ข้อควรระวัง
- Linux: ชื่อไฟล์และโฟลเดอร์แยกตัวพิมพ์เล็ก/ใหญ่ ใช้ path จาก `PathHelper` ตรงๆ เท่านั้น
- ไฟล์ secrets **ไม่ได้เข้ารหัส** ดูการจัดการ permission บน Unix ที่ [[ADR-0009-unix-file-permissions]]
- `dotnet msbuild -getProperty` ต้องใช้ SDK 8 ขึ้นไป
