# ADR-0001：技術堆疊與單一專案配置

- **狀態**：Accepted
- **日期**：2026-10-18

## 背景

ScreenshotApp 必須在 Windows 11 x64 完全離線運作，並以 C#、.NET 10 LTS、WPF 與 Win32 API 實作；規格要求單一 WPF 可執行專案加上一個測試專案，不因抽象化拆分多個類別庫，也不得導入 DI Container、事件匯流排或外掛架構。

開發機器的 NuGet 只註冊了 Visual Studio 離線來源，未註冊 nuget.org；同時啟用 WPF 與 Windows Forms 時，隱含 `using` 會造成 `Application` 等型別名稱衝突（CS0104）。

## 決策

1. **Solution**：`ScreenshotApp.sln`（傳統 `.sln` 格式，以 `dotnet new sln --format sln` 建立，相容較舊的 Visual Studio）。
2. **應用程式專案** `ScreenshotApp/ScreenshotApp.csproj`：
   - `OutputType=WinExe`、`TargetFramework=net10.0-windows`、`UseWPF=true`。
   - `UseWindowsForms=true`，僅供系統匣與色彩對話框使用（見 [ADR-0006](0006-tray-and-winforms-boundary.md)）；以 `<Using Remove="System.Windows.Forms" />` 移除隱含 `using`，避免名稱衝突並讓 WinForms 型別只能以完整命名空間或別名引用。
   - `Nullable=enable`、`ImplicitUsings=enable`、`AllowUnsafeBlocks=true`（供 `LibraryImport` 原始碼產生器使用）。
   - `ApplicationManifest=app.manifest`（Per-Monitor V2 DPI、Common Controls v6）、`ApplicationIcon=Resources\app.ico`、`NeutralLanguage=zh-TW`。
   - 應用程式專案**不引用任何 NuGet 套件**；所需功能皆來自 .NET 10 與 Windows Desktop 執行階段。
3. **Win32 互操作**：集中於 `Platform/NativeMethods.cs`，使用 `LibraryImport`（編譯期產生封送程式碼）；只宣告實際使用的 API。非受控資源以 `SafeHandle` 或 `try/finally` 管理所有權。
4. **測試專案** `ScreenshotApp.Tests/ScreenshotApp.Tests.csproj`：xunit 2.9.x、`net10.0-windows`、`UseWPF=true`，以 `ProjectReference` 引用應用程式專案；應用程式以 `InternalsVisibleTo` 對測試開放 `internal` 型別。需要 WPF 物件或訊息迴圈的測試在專用 STA 執行緒執行。
5. **套件來源**：專案根目錄的 `nuget.config` 以 `<clear/>` 後僅加入 nuget.org，只影響本儲存庫，不更動使用者全域設定。測試專案不引用 `coverlet.collector`。
6. **目錄依功能劃分**：`Platform/`、`Capture/`、`Editor/`、`Export/`、`Settings/`、`Resources/`。
7. **組合方式**：不使用 DI Container 或 MVVM 框架；由 `AppController` 建立並連接各服務，服務以建構式參數取得相依物件，以便測試注入。

## 考慮過的替代方案

- **多個類別庫（Core／Platform／UI）**：規格明確禁止無意義拆分；單一組件加 `InternalsVisibleTo` 已足以測試。
- **`DllImport`**：可行，但 `LibraryImport` 在編譯期產生封送程式碼、錯誤更早發現，且為 .NET 7 之後的建議做法。
- **`.slnx` 格式**：.NET 10 預設格式，但較舊的 IDE 不支援；傳統 `.sln` 相容性較高。

## 影響

- 單一可執行組件，發佈與除錯簡單；自包含發佈會因 Windows Forms 執行階段而略增大小。
- 所有 Windows API 呼叫集中在 `Platform/`，便於審查 GDI 資源與錯誤處理。
- 新增 NuGet 套件需經 ADR 評估。
