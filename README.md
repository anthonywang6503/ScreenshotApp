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
3. 放開滑鼠即完成，截圖在編輯器中開啟。
4. 按 `Esc` 或滑鼠右鍵取消；在原地點一下（沒有拖曳）不會產生截圖，可以重新拖曳。編輯中再截圖時取消，會回到原本的圖片。

### 編輯

截圖完成後開啟編輯器，以「符合視窗」顯示（不超過 100%）。100% 代表一個圖片像素對應一個螢幕實體像素；縮放、平移或調整視窗都不會改變輸出。同一時間只有一個編輯視窗，新截圖會取代目前的圖片。

| 工具（Alt+字母） | 操作 |
|------------------|------|
| 選取（V） | 點選標註後拖曳移動、拖曳控制點調整大小或端點；雙擊文字重新編輯，雙擊步驟編號修改數值 |
| 裁切（C） | 拖曳框選新的裁切範圍，或拖曳控制點調整；`Enter` 或在範圍內雙擊套用，`Esc` 取消；不會刪除原圖，之後可以重新調整 |
| 矩形（R）、橢圓（E）、直線（L）、箭頭（A） | 拖曳繪製；箭頭指向放開滑鼠的位置 |
| 文字（T） | 點一下開始輸入（可用微軟注音，`Enter` 換行），`Ctrl+Enter` 或點擊其他位置完成，`Esc` 取消；點擊既有文字可重新編輯 |
| 編號（N） | 點一下放置自動遞增的步驟編號；刪除編號不會重排其他編號 |

屬性列可變更顏色（8 種常用色與自訂）、線寬、字型、字級與步驟編號值，尺寸都以圖片像素為單位。有選取標註時變更該標註；沒有選取時變更之後新增標註的預設值，顏色、線寬與字級會記住到下次啟動。標註上按滑鼠右鍵可調整前後層順序或刪除。

| 快捷鍵 | 功能 |
|--------|------|
| `Ctrl+Z`／`Ctrl+Y`（或 `Ctrl+Shift+Z`） | 復原／重做（最多 100 筆，一次拖曳算一筆） |
| `Ctrl+C` | 將完成的圖片複製到剪貼簿 |
| `Ctrl+S` | 另存 PNG（預設檔名 `Screenshot_yyyyMMdd_HHmmss_fff.png`，預設位置為「圖片」資料夾） |
| `Delete` | 刪除選取的標註 |
| `Esc` | 取消目前的拖曳、裁切或選取 |
| `Ctrl+]`／`Ctrl+[`、`Ctrl+Shift+]`／`Ctrl+Shift+[` | 上移／下移一層、移到最上層／最下層 |
| `Ctrl+滾輪`、`Ctrl+=`／`Ctrl+-`、`Ctrl+0` | 以游標為中心縮放（25%～400%）、放大／縮小、符合視窗 |
| 按住空白鍵或滑鼠中鍵拖曳 | 平移畫布 |

輸入文字時，`Ctrl+C`、`Ctrl+Z` 等按鍵作用在文字上，不會複製整張圖片或復原其他操作。複製與另存的圖片尺寸等於裁切範圍。關閉編輯器回到系統匣常駐。

已知限制（不擷取游標、受保護內容、安全桌面、尚無未匯出變更提醒等）見 [docs/KNOWN_LIMITATIONS.md](docs/KNOWN_LIMITATIONS.md)。

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
  Editor/                 編輯視窗、編輯工作階段（工具邏輯）、標註層與控制點層、縮放階段
  Editor/Model/           圖片文件、標註與像素座標模型
  Editor/Commands/        編輯命令與復原／重做歷史
  Editor/Rendering/       標註繪製（畫面與輸出共用）
  Export/                 圖片合成、剪貼簿、PNG 檔案
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
