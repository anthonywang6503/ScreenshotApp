# ADR-0017：發佈與封裝——自包含 win-x64 資料夾版為主、單檔版為輔、不簽章

- **狀態**：Accepted
- **日期**：2026-10-18（2026-10-19 修訂第 4 點：版本遞增方式）

## 背景

v1.0 的交付物是 Windows 11 x64 自包含式發佈目錄及 ZIP，以 `dotnet publish -r win-x64 --self-contained true` 產生；可另外提供 `PublishSingleFile=true` 的單一執行檔，但必須再次確認 WPF 資源、圖示與截圖功能正常。不要求 MSIX、安裝程式、程式碼簽章、Microsoft Store 或自動更新。發佈版本必須能在沒有安裝 .NET Runtime 的乾淨 Windows 11 x64 上執行。

本機試發佈（.NET SDK 10.0.402、Runtime 10.0.13）的結果：

| 方式 | 輸出 |
|------|------|
| 自包含資料夾 | 473 個檔案、173 MB；其中 13 種語言的框架附屬組件資料夾各 17 個檔案，除繁體中文外的 12 種共 204 個檔案、17 MB |
| 單檔（預設） | `ScreenshotApp.exe` 165 MB，旁邊仍有 5 個 WPF 原生程式庫（`wpfgfx_cor3.dll`、`PresentationNative_cor3.dll`、`D3DCompiler_47_cor3.dll`、`PenImc_cor3.dll`、`vcruntime140_cor3.dll`） |
| 單檔＋`IncludeNativeLibrariesForSelfExtract` | 只有 `ScreenshotApp.exe`（173 MB）；原生程式庫在第一次執行時解壓到暫存資料夾 |

## 決策

1. **主要交付物是自包含資料夾版**：`dotnet publish -c Release -r win-x64 --self-contained true`，輸出到 `publish/ScreenshotApp-<版本>-win-x64/` 並壓縮成同名 ZIP（ZIP 內以該資料夾為根目錄）。資料夾版不需要解壓到暫存資料夾，啟動行為與開發時的建置最接近。
2. **單檔版為輔**：`PublishSingleFile=true` 搭配 `IncludeNativeLibrariesForSelfExtract=true`，輸出只有一個 `ScreenshotApp.exe`。只有在以發佈出來的單檔執行檔通過系統匣、快捷鍵、截圖與複製的煙霧驗證後才提供，否則在驗收紀錄中標示「未提供」。
3. **只保留繁體中文的框架附屬組件**（`SatelliteResourceLanguages=zh-Hant`）：介面只有繁體中文；WPF 文字輸入框的右鍵選單（剪下、複製、貼上）等框架文字需要 zh-Hant 附屬組件，其餘 12 種語言用不到。
4. **版本**：專案檔明確設定 `Version` 為 `1.0.0`，執行檔的檔案版本與產品版本由它產生。
   - 修訂（2026-10-19）：版本隨發佈依語意化版本遞增，專案檔的 `Version` 與 `app.manifest` 的 `assemblyIdentity` 版本一起更新；加入相容的新功能時遞增次版號（v1.1.0 為 `1.1.0`／`1.1.0.0`）。commit 後加上本機標籤 `v<版本>`，再以該 commit 發佈，產品版本內嵌的 commit 才會與標籤一致。單檔版仍須以該版本的發佈檔通過煙霧驗證後才提供（第 2 點）。
5. **授權檔案**：自包含版本內含 .NET Runtime 與 Windows Desktop Runtime（MIT 授權），發佈時把兩個 Runtime 套件的授權檔與第三方聲明複製到發佈目錄的 `licenses/`；儲存庫根目錄的 `THIRD-PARTY-NOTICES.md` 說明來源。
6. **不簽章、不做安裝程式、不修剪、不使用 ReadyToRun 或 Native AOT**：WPF 不支援修剪與 Native AOT；框架組件在 Runtime 套件中已預先編譯，應用程式本身很小，ReadyToRun 的效益有限。
7. **偵錯符號**：保留應用程式的 `ScreenshotApp.pdb`（約 80 KB），故障日誌的堆疊會包含行號，方便回報問題；框架不附帶符號。
8. **可重現的發佈步驟**：`tools/Publish-Release.ps1` 依序清除舊輸出、發佈兩種版本、複製授權檔與使用者文件、建立 ZIP，並列出檔案數與大小。

## 考慮過的替代方案

- **框架相依（不自包含）**：檔案小，但需要使用者先安裝 .NET 10 Desktop Runtime，不符合乾淨機器可執行的要求。
- **只提供單檔版**：單檔版第一次執行要把原生程式庫解壓到暫存資料夾；若暫存資料夾受限（例如企業原則），程式無法啟動。資料夾版沒有這個前提，因此作為主要交付物。
- **單檔版不內含原生程式庫**：會在執行檔旁留下 5 個 DLL，失去「單一檔案」的意義。
- **`EnableCompressionInSingleFile`**：可縮小單檔版，但每次啟動都要解壓縮；發佈時已另外壓縮成 ZIP，沒有必要。
- **保留全部語言的附屬組件**：多 204 個檔案與 17 MB，介面只有繁體中文，沒有使用者。

## 影響

- 實際發佈結果（只保留繁體中文附屬組件、含授權檔與文件）：資料夾版 275 個檔案、約 156 MB，ZIP 約 67 MB；單檔版執行檔約 149 MB，ZIP 約 64 MB。
- 未簽章的執行檔從網路下載後，Windows SmartScreen 可能顯示警告，需要使用者選擇「仍要執行」；列入已知限制。
- 單檔版第一次啟動會把原生程式庫寫入暫存資料夾（不含任何使用者資料）；若暫存資料夾不可寫入，請改用資料夾版。
- 乾淨機器上的執行（沒有 .NET Runtime）需要使用者以另一台電腦確認，未確認前標示「尚未驗證」。
