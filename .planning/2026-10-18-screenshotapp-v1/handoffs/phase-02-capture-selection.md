# Handoff — Phase 2：截圖與區域選取

**日期**：2026-10-18
**狀態**：complete
**分支**：`feature/phase-02-capture-selection` → fast-forward 合併回 `main`

## 1. 完成內容

| Item | 產出 |
|------|------|
| P2-00 | `docs/adr/0007`（截圖策略）、`0008`（覆蓋層定位與輸入座標）、`0009`（狀態機與重入保護），皆 Accepted |
| P2-01 | `Editor/Model/PixelPoint.cs`、`PixelRect.cs`（半開區間、寬高不得為負、`FromCorners`、`Intersect`、`Clamp`、`Contains`、`Offset`）——由 subagent 實作，主 agent 審查 |
| P2-02 | `Platform/MonitorService.cs`（`EnumDisplayMonitors` → `MonitorInfo{Index, Bounds}`） |
| P2-03 | `Capture/CoordinateMapper.cs`（桌面 ↔ 顯示器本地 ↔ 覆蓋層 DIP）——由 subagent 實作，主 agent 審查 |
| P2-04 | `Platform/CaptureService.cs`（`BitBlt(SRCCOPY|CAPTUREBLT)` + `GetDIBits` → 凍結 `Bgr32`）、`Platform/GdiHandles.cs`（`SafeScreenDcHandle`／`SafeMemoryDcHandle`／`SafeGdiObjectHandle`）、`CaptureException` |
| P2-05 | `Capture/DesktopSnapshot.cs`（逐顯示器快照、裁切為獨立 bitmap） |
| P2-06 | `Capture/SelectionOverlay.xaml(.cs)`（`HwndSource` 直接建立於顯示器實體邊界；暗化遮罩、實體像素外框、尺寸標籤、十字游標；`WM_DISPLAYCHANGE`／`WM_DPICHANGED` 取消） |
| P2-07 | `Capture/DragSelection.cs`（選取幾何）、`Capture/SelectionController.cs`（協調覆蓋層、滑鼠捕捉、Esc／右鍵取消、零尺寸維持選取） |
| P2-08 | `AppStateMachine.cs`、`Capture/CaptureFlow.cs`（含 `ICaptureEnvironment`、`CaptureOutcome`）、`Capture/DesktopCaptureEnvironment.cs`、`Export/ClipboardService.cs`（24 位元 DIB）、系統匣「開始截圖」、`AppController` 接線與通知 |
| P2-09 | 擷取失敗與顯示設定變更：關閉覆蓋層、恢復視窗、回到常駐、記錄故障日誌並以通知提示 |
| P2-10 | 新增 9 個測試類別、200 個測試案例；全專案 314 個 |
| P2-11 | `tools/smoke/Invoke-Phase2Smoke.ps1`、`tools/smoke/Show-TestPattern.ps1`；證據 `docs/evidence/phase-02/` |
| P2-12 | `docs/KNOWN_LIMITATIONS.md` 初稿 |
| P2-13 | `docs/ACCEPTANCE.md` 新增 Phase 2 使用者驗證步驟（MT-02 觸發、MT-03 取消部分、MT-04、MT-05） |
| 文件 | `README.md`（截圖操作、專案結構）、`CHANGELOG.md` |

## 2. 設計原因

- **覆蓋層以 `HwndSource` 直接建立於顯示器實體邊界**：查 WPF 原始碼確認 PMv2 下初始 DPI 取自 HWND；出生即在目標顯示器就沒有跨 DPI 移動。開發機只有一個顯示器，任何「跨 DPI 移動」的路徑都不會被執行到，不能依賴它。
- **選取座標取自滑鼠事件本身**：最初在事件處理當下讀 `GetCursorPos`，煙霧驗證量到 UI 執行緒延後處理按下事件時，起點落在游標稍後的位置（選取只剩拖曳路徑的 25%／75%）。改用 WPF 事件位置經 `CoordinateMapper` 以覆蓋層 DPI 換回實體像素，往返誤差已證明為 0。
- **選取包含起點與終點像素、原地點一下不產生截圖並維持選取**：可以選到顯示器最後一行與最後一列；誤點不會丟掉剛凍結的畫面（與 Windows 剪取工具一致）。
- **快照 `Bgr32`**：`BitBlt` 結果的 alpha 未定義（多為 0），以 `Bgra32` 解讀會變成整張透明。
- **剪貼簿放 24 位元 DIB，不用 `Clipboard.SetImage`**：後者每次留下 1 個 GDI 物件；DIB 由 Windows 自動轉換為 CF_BITMAP；不用 32 位元以免接收端把未定義的 alpha 當成透明度。
- **隱藏自家視窗前關閉其 DWM 轉場動畫**：只等 `DwmFlush` 時會截到設定視窗淡出中的半透明殘影。
- **選取框為實體像素環形幾何、不做邊緣平滑**：175% 下 DIP 筆劃外框會蓋住選取範圍最後一列像素。
- **`AppStateMachine` 的 Editing、`CoordinateMapper` 的縮放平移、`MonitorInfo` 的其他欄位延後**：與 Phase 1 後移 SafeHandle、狀態機相同的原則，有使用者時才加入。
- **`CaptureFlow` + `ICaptureEnvironment`**：讓完成、取消、失敗、重入時都恢復視窗並回到常駐，可用測試替身驗證。

## 3. 驗證結果與證據

| 驗證 | 結果 |
|------|------|
| `dotnet build ScreenshotApp.sln -c Release` | 0 error、0 warning |
| `dotnet test ScreenshotApp.sln -c Release` | 314 passed、0 failed |
| 煙霧驗證（Release exe，Windows 11 Pro 10.0.26200，單一顯示器 3456×2160 @175%） | 38 項全數通過：覆蓋層貼合顯示器且快照與畫面逐像素一致；雙向拖曳、設定視窗遮擋、系統匣選單路徑的截圖皆與已知圖樣逐像素一致；取消、零尺寸、重入、顯示設定變更 |
| 30 次循環資源量測（每次一次完成＋一次取消） | GDI 物件恆為 38；USER 52～54；Handle 595～600；私有記憶體 286～381 MB 隨垃圾回收起伏，無成長趨勢 |
| 程式碼註解掃描（不得引用編號或文件路徑） | 無違規 |

證據：`docs/evidence/phase-02/`（結果清單、循環量測 CSV、4 張只含圖樣範圍的截圖）；細節見 `docs/ACCEPTANCE.md`。

煙霧驗證過程中發現並修正的產品問題（皆有對應修正與重新驗證）：

1. `Clipboard.SetImage` 每次留下 1 個 GDI 物件（循環量測每次 +1）→ 改用 DIB 位元組，GDI 物件 30 次循環恆定。
2. 設定視窗淡出殘影被截入（遮住區域 98.8% 像素差 1 個色階）→ 隱藏前關閉轉場動畫。
3. 選取起點／終點落在游標稍後的位置（UI 執行緒延後處理時）→ 改取事件本身的位置。
4. 175% 下選取框蓋住最後一列像素 → 實體像素環形外框。

測試工具問題（非產品缺陷）：pwsh `Add-Type` 無法在 C# 中使用 WinForms 型別（改由 PowerShell 端操作、圖樣視窗獨立處理程序）；嚴格模式下空陣列回傳為 `$null`；日誌比對未排除行尾 `\r`；使用者正在操作電腦時圖樣被遮住（腳本改為先等使用者閒置）。

## 4. 風險

- **混合 DPI、多螢幕、負座標只能靠設計與推論**：本機只有一顆 175% 顯示器；覆蓋層「出生即在目標顯示器」避開了跨 DPI 移動，但實際行為需使用者 MT-04、MT-05 回報。
- **煙霧腳本會操作真實桌面**：執行期間移動滑鼠、送出按鍵，並暫時改寫剪貼簿（結束時盡力還原所有格式；本階段正式執行時腳本只會還原文字，使用者當時剪貼簿中的非文字內容已被覆蓋）；每次截圖成功會留下一則系統通知；開始前會等使用者閒置 20 秒。
- **截圖成功只以系統通知告知**：Windows「專注」模式可能抑制通知；Phase 3 改為開啟編輯器後不再依賴通知。
- **Windows 系統對話框可能被截入**：例如設定視窗的色彩對話框開啟時按快捷鍵，已列入已知限制。

## 5. 後續工作（Phase 3）

1. 從 `main` 建立 `feature/phase-03-editor-core`，Phase 3 → `in_progress`，先寫 ADR-0010～0013。
2. `AppStateMachine` 加入 Editing（取消選取時回到先前狀態並恢復編輯視窗）；`AppController.StartCapture` 的完成路徑由「複製到剪貼簿＋通知」改為開啟編輯器（`CaptureOutcome.Image` 即文件的原始 bitmap，`Bgr32`、96 DPI）。
3. `CoordinateMapper` 加入編輯器的 Image ↔ ViewDip（縮放、平移）與對應的 AT-01 測試。
4. `ClipboardService` 的 DIB 產生器可直接供編輯器的複製使用；Phase 4 再加入 PNG 格式。
5. 可重用：`tools/smoke/Show-TestPattern.ps1`（已知圖樣）、`Invoke-Phase2Smoke.ps1` 的模擬輸入保護（只在覆蓋層位於前景時送 Esc、只在快捷鍵由本程式持有時送快捷鍵、開始前等使用者閒置）。

## 6. 未驗證項目

- MT-01、MT-02 占用提示（Phase 1）：使用者尚未回報。
- MT-02 在各應用程式中觸發截圖、MT-03 取消部分、MT-04 各縮放、MT-05 雙螢幕：需使用者實機驗證（步驟見 `docs/ACCEPTANCE.md`）。
- 選取期間實際變更解析度或插拔顯示器：Agent 只以送出 `WM_DISPLAYCHANGE` 模擬；實際操作列入 MT-09。
