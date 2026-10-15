---
tags: [platform]
updated: 2026-10-14
---
# User-secrets paths ของแต่ละ OS

ให้ใช้ `PathHelper.GetSecretsPathFromSecretsId(id)` จาก `Microsoft.Extensions.Configuration.UserSecrets` เสมอ ไม่ต้องคำนวณ path เอง

ลำดับการหา root ของ `PathHelper` (as of 2026-10-14, Microsoft.Extensions.Configuration.UserSecrets 10.0.13 ตรวจจาก source ของ release/10.0 และลองรันจริงแล้ว):
1. `APPDATA` → `%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json` (Windows)
2. `HOME` → `~/.microsoft/usersecrets/<id>/secrets.json` (macOS / Linux)
3. `Environment.GetFolderPath(ApplicationData)` (บน Unix คือ `$XDG_CONFIG_HOME` หรือ `~/.config`)
4. `Environment.GetFolderPath(UserProfile)`
5. `DOTNET_USER_SECRETS_FALLBACK_DIR`

**ข้อสังเกตจากการทดสอบ:**
- ลำดับนี้ต่อกันด้วย `??` แต่ `GetFolderPath` คืนค่า `""` ไม่ใช่ `null` ในทางปฏิบัติจึงไม่เคยไปถึงข้อ 4–5 ถ้าตั้งแค่ `DOTNET_USER_SECRETS_FALLBACK_DIR` มันจะถูกเพิกเฉย และถ้า ApplicationData ว่างจะ throw แทน
- โฟลเดอร์ย่อยขึ้นกับว่า**ตัวแปร `APPDATA` มีค่าหรือไม่**: ถ้ามีค่าใช้ `Microsoft/UserSecrets` ถ้าไม่มีใช้ `.microsoft/usersecrets` (ตัวพิมพ์เล็ก ซึ่งสำคัญบน Linux)
- ถ้า `APPDATA=""` (ตั้งไว้แต่ว่าง) หรือ id มีอักขระที่ใช้เป็นชื่อไฟล์ไม่ได้ → `InvalidOperationException` และ id ว่าง → `ArgumentException`
- `SecretsStore` ตรวจ id ก่อน (ผิด → `InvalidInput`) และถ้าหา root ไม่เจอจะรายงานเป็น `IoFailure`

## ข้อควรระวัง
- Linux: ชื่อไฟล์และโฟลเดอร์แยกตัวพิมพ์เล็ก/ใหญ่ ใช้ path จาก `PathHelper` ตรงๆ เท่านั้น
- ไฟล์ secrets **ไม่ได้เข้ารหัส** ดูการจัดการ permission บน Unix ที่ [[ADR-0009-unix-file-permissions]]
- `dotnet msbuild -getProperty` ต้องใช้ SDK 8 ขึ้นไป
