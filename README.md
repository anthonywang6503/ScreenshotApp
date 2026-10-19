# ScreenshotApp

Windows 原生的輕量截圖與圖片標註工具：常駐系統匣，以快捷鍵選取畫面，在編輯器加入方框、箭頭、文字、步驟編號或裁切後，複製到剪貼簿或另存 PNG。完全本機離線運作，不需要系統管理員權限、帳號或網路。

目前版本：**1.1.0**（加入依情境的右鍵選單、再製與重新編號等編輯操作，裁切後以裁切範圍填滿畫布）。各版本的功能與驗證結果記錄於 [CHANGELOG.md](CHANGELOG.md)。

## 下載與執行

發佈版本是 Windows 11 x64 的自包含程式，不需要另外安裝 .NET：

- `ScreenshotApp-1.1.0-win-x64.zip`：資料夾版本，解壓縮後執行 `ScreenshotApp.exe`。
- `ScreenshotApp-1.1.0-win-x64-singlefile.zip`：只有一個 `ScreenshotApp.exe` 的版本。

程式啟動後只在系統匣常駐，預設截圖快捷鍵為 `Ctrl+Shift+2`。安裝、截圖、編輯、複製與另存、設定與移除方式見 [使用說明](docs/USER_GUIDE.md)。

## 系統需求

- 執行：Windows 11 x64（Windows 10 x64 盡力相容）。
- 開發：.NET 10 SDK（`net10.0-windows`，含 WPF 與 Windows Forms）。

## 建置、測試與發佈

```powershell
dotnet build ScreenshotApp.sln -c Release
dotnet test ScreenshotApp.sln -c Release
pwsh -NoProfile -File tools/Publish-Release.ps1
```

- 套件來源由專案根目錄的 [nuget.config](nuget.config) 指定為 nuget.org，不影響使用者全域設定。應用程式本身不引用任何 NuGet 套件。
- 開發時的執行檔：`ScreenshotApp\bin\Release\net10.0-windows\ScreenshotApp.exe`。
- `tools/Publish-Release.ps1` 以 `dotnet publish -c Release -r win-x64 --self-contained true` 產生資料夾版與單檔版，連同授權檔與使用文件輸出到 `publish/`（不納入版本控制），並建立兩個 ZIP。封裝方式的決策見 [ADR-0017](docs/adr/0017-release-packaging.md)。
- 實機煙霧驗證腳本在 [tools/smoke/](tools/smoke/)，會實際啟動程式並模擬滑鼠鍵盤操作；開始前會等待電腦閒置，執行期間請勿操作電腦。

## 專案結構

```
ScreenshotApp/            WPF 應用程式（net10.0-windows）
  Platform/               Win32 互操作、快捷鍵、系統匣、顯示器列舉、GDI 擷取、單一執行個體、開機啟動、故障日誌
  Capture/                截圖流程、座標換算、區域選取覆蓋層
  Editor/                 編輯視窗、編輯工作階段（工具邏輯）、標註層與控制點層、縮放階段
  Editor/Model/           圖片文件、標註與像素座標模型
  Editor/Commands/        編輯命令與復原／重做歷史
  Editor/Rendering/       標註繪製（畫面與輸出共用）
  Export/                 圖片合成、剪貼簿、PNG 檔案、開啟圖片
  Settings/               設定模型、JSON 持久化、設定視窗
  Resources/              介面文字（Strings.resx，台灣繁體中文）與應用程式圖示
ScreenshotApp.Tests/      xunit 自動化測試
docs/                     使用說明、架構、驗收紀錄、已知限制、架構決策紀錄（adr/）
tools/                    圖示產生、發佈、實機煙霧驗證（smoke/）
```

## 文件

- [使用說明](docs/USER_GUIDE.md)：快捷鍵、編輯器、複製與存檔、設定、本機資料與移除
- [架構](docs/ARCHITECTURE.md)：模組關係、圖片資料模型、座標系統、狀態機
- [驗收紀錄](docs/ACCEPTANCE.md)：自動化測試、Agent 端煙霧驗證與使用者實機驗收
- [已知限制](docs/KNOWN_LIMITATIONS.md)
- [架構決策紀錄](docs/adr/)
- [第三方聲明](THIRD-PARTY-NOTICES.md)
