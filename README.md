# ScreenshotApp

Windows 原生的輕量截圖與圖片標註工具：常駐系統匣，以快捷鍵選取畫面，在編輯器加入方框、箭頭、文字、步驟編號或裁切後，複製到剪貼簿或另存 PNG。完全本機離線運作，不需要系統管理員權限、帳號或網路。

> 開發中。各階段完成的功能與驗證結果記錄於 [CHANGELOG.md](CHANGELOG.md)。

## 系統需求

- 執行：Windows 11 x64（Windows 10 x64 盡力相容）。
- 開發：.NET 10 SDK（`net10.0-windows`，含 WPF 與 Windows Forms 桌面工作負載）。

## 建置與測試

```powershell
dotnet build ScreenshotApp.sln -c Release
dotnet test ScreenshotApp.sln -c Release
```

套件來源由專案根目錄的 [nuget.config](nuget.config) 指定為 nuget.org，不影響使用者全域設定。應用程式本身不引用任何 NuGet 套件。

## 執行

```powershell
.\ScreenshotApp\bin\Release\net10.0-windows\ScreenshotApp.exe
```

程式啟動後不顯示主視窗，只在系統匣常駐：

- 按下截圖快捷鍵或選擇系統匣的「開始截圖」：開始截圖（見下節）。
- 雙擊系統匣圖示或選擇「設定」：開啟設定視窗（截圖快捷鍵、登入 Windows 時啟動、預設標註樣式）。
- 選擇「結束」：解除快捷鍵、移除系統匣圖示並結束程式。
- 再次執行程式不會產生第二個執行個體，而是顯示既有執行個體的設定視窗。

預設截圖快捷鍵為 `Ctrl+Shift+2`。快捷鍵被其他程式占用時，系統匣通知、圖示工具提示與設定視窗都會顯示原因。

### 截圖

1. 按下快捷鍵後，畫面凍結並變暗，每個顯示器都可以操作。
2. 按住滑鼠左鍵拖曳選取範圍，選取框旁顯示實際像素寬高；選取限於起始的顯示器內。
3. 放開滑鼠即完成。編輯器完成前，截圖會直接複製到剪貼簿，並以系統通知顯示尺寸。
4. 按 `Esc` 或滑鼠右鍵取消；在原地點一下（沒有拖曳）不會產生截圖，可以重新拖曳。

已知限制（不擷取游標、受保護內容、安全桌面等）見 [docs/KNOWN_LIMITATIONS.md](docs/KNOWN_LIMITATIONS.md)。

## 本機資料位置

| 項目 | 位置 |
|------|------|
| 設定 | `%LocalAppData%\ScreenshotApp\settings.json` |
| 故障日誌（只含錯誤訊息與堆疊，不含圖像或輸入文字） | `%LocalAppData%\ScreenshotApp\logs\error.log` |
| 登入時啟動 | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 的 `ScreenshotApp` 值（只在啟用時存在） |

## 專案結構

```
ScreenshotApp/            WPF 應用程式（net10.0-windows）
  Platform/               Win32 互操作、快捷鍵、系統匣、顯示器列舉、GDI 擷取、單一執行個體、開機啟動、故障日誌
  Capture/                截圖流程、座標換算、區域選取覆蓋層
  Editor/Model/           圖片像素座標模型
  Export/                 剪貼簿輸出
  Settings/               設定模型、JSON 持久化、設定視窗
  Resources/              介面文字（Strings.resx，台灣繁體中文）與應用程式圖示
ScreenshotApp.Tests/      xunit 自動化測試
docs/adr/                 架構決策紀錄
tools/                    開發輔助腳本：圖示產生、實機煙霧驗證（tools/smoke/）
```

## 文件

- 架構決策紀錄：[docs/adr/](docs/adr/)
- 驗收紀錄（自動化測試、煙霧驗證、實機驗收）：[docs/ACCEPTANCE.md](docs/ACCEPTANCE.md)
- 已知限制：[docs/KNOWN_LIMITATIONS.md](docs/KNOWN_LIMITATIONS.md)
