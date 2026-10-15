---
status: accepted
date: 2026-10-14
tags: [adr, testing]
---
# ADR-0011: Unit test และ code coverage ไม่ต่ำกว่า 90%

## Context
ผู้ใช้กำหนดให้ทุกโปรเจคมี unit test และ code coverage ไม่ต่ำกว่า 90% และใช้ coverage เป็นเงื่อนไขหนึ่งของการ merge เข้า `develop` (ดู [[ADR-0012-branching-and-qa]])

## Decision
**Test projects** (ทุกโปรเจคใน `src/` มีโปรเจค test คู่กัน)
- `tests/SecretImport.Core.Tests`
- `tests/SecretImport.Cli.Tests` (ใช้ `TestConsole` ของ Spectre และ fake `IImportService`)
- `tests/SecretImport.Desktop.Tests` (test ViewModel + `Avalonia.Headless`)

**เครื่องมือ** (as of 2026-10-14)
- xUnit v3 (`xunit.v3` 4.0.x) และ `Microsoft.NET.Test.Sdk`
- `coverlet.msbuild` 10.1.x ใช้เก็บ coverage และบังคับ threshold
- `dotnet-reportgenerator-globaltool` 5.5.x ติดตั้งเป็น local tool (`.config/dotnet-tools.json`) ใช้สร้างรายงาน HTML
- **Headless UI test**: `Avalonia.Headless.XUnit` 12.1.4 ใช้กับ xUnit v3 4.0 ไม่ได้ (build มาสำหรับ xunit.v3 3.2.2 และ test discovery เจอ `MissingMethodException`) จึงใช้ `Avalonia.Headless` ตรงๆ แทน ผ่าน assembly fixture `HeadlessSession` ที่ห่อ `HeadlessUnitTestSession.StartNew(...)` และ test เรียก `RunAsync(...)` fixture ต้อง dispose ผ่าน `DisposeAsync` เท่านั้น เพราะ `Dispose()` แบบ synchronous จะค้างตอน xUnit teardown (ตรวจสอบเมื่อ 2026-10-14)
- **Test runner**: รันผ่าน VSTest (`IsTestingPlatformApplication=false`) ไม่ใช้ Microsoft.Testing.Platform เพราะ `coverlet.msbuild` ทำงานกับ VSTest target เท่านั้น ถ้าวันหลังย้ายไป MTP ต้องเปลี่ยนไปใช้ coverlet แบบ MTP
- **ขอบเขตการวัด**: แต่ละโปรเจค test วัดเฉพาะโปรเจคที่ตัวเอง test (`Include=[SecretImport.X]*`)

**เกณฑ์**
- **line ≥ 90% และ branch ≥ 90%** คิดแยกต่อโปรเจค test (ThresholdStat = total)
- ถ้าต่ำกว่าเกณฑ์ build จะ fail ทั้งในเครื่องและใน CI
- ตั้งค่า threshold ไว้ใน `tests/Directory.Build.props` เพื่อให้ทุกโปรเจค test ใช้เกณฑ์เดียวกัน
- คำสั่งที่ใช้: `dotnet test -p:CollectCoverage=true` (ค่า threshold มาจาก props)

**สิ่งที่ไม่นับใน coverage** (ต้องระบุชื่อไฟล์หรือ attribute ชัดเจน และต้องผ่านการรีวิวของ QA)
- โค้ดที่ compiler หรือ source generator สร้าง (`[GeneratedCode]`, `[CompilerGenerated]`, `*.g.cs`)
- Desktop: `Program.cs`, `App.axaml.cs` และ code-behind ของ View ที่มีแค่ `InitializeComponent()`
- Cli: `Program.cs` ได้เฉพาะกรณีที่มีแค่บรรทัดเดียวเรียกเข้า entry point ที่ test ได้
- ห้ามใส่ `[ExcludeFromCodeCoverage]` ให้ logic เพื่อดัน % ให้ผ่าน

**Integration test** ที่รัน `dotnet` SDK จริงให้ใส่ trait `Category=Integration` ทั้งในเครื่องและใน CI ให้รันรวมกับ test อื่นด้วย ใน CI ถ้าไม่มี SDK จะทำให้ test fail

## Consequences
- ต้องออกแบบ Cli และ Desktop ให้ test ได้ตั้งแต่ต้น (inject `IAnsiConsole`, ใช้ interface ของ `IImportService`, ไม่เรียก `Console` หรือ static ตรงๆ)
- QA จะรัน coverage เองก่อน merge ทุกครั้ง ไม่เชื่อตัวเลขที่ agent รายงานมา
