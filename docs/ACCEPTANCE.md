# 驗收紀錄

記錄自動化測試、Agent 端實機煙霧驗證與使用者實機驗收（MT）的結果。未經實際操作確認的項目一律標示「尚未驗證」，不以「編譯成功」取代操作驗收。

## 1. 自動化測試

最近一次執行：2026-10-18，`dotnet test ScreenshotApp.sln -c Release`，**314 passed / 0 failed**。

| 規格驗收項目 | 範圍 | 測試類別 | 狀態 |
|--------------|------|----------|------|
| AT-01 座標映射（顯示器部分） | 桌面實體像素 ↔ 顯示器本地像素（正負起點、非主螢幕、主螢幕左側與上方）；顯示器本地像素 ↔ 覆蓋層 DIP，在 100%～300% 共 8 種縮放下 0～4000 每個整數像素往返誤差為 0 | `CoordinateMapperTests`（67） | 通過 |
| AT-01 座標映射（編輯器縮放與平移） | 圖片像素 ↔ 編輯器 DIP（縮放、平移） | 隨編輯器實作（Phase 3） | 尚未驗證 |
| AT-02 選取矩形 | 半開區間、各方向拖曳、包含起點與終點像素、邊界夾取、跨顯示器拖曳夾取在起始顯示器、負座標顯示器、可選到整個顯示器、零尺寸 | `PixelRectTests`（78）、`SelectionLogicTests`（29） | 通過 |
| AT-08 設定 | 正常載入、快捷鍵變更、JSON 損壞復原（含備份原檔）、無效欄位只重設該欄位、讀取失敗、權限不足（唯讀檔）、無效路徑、原子寫入 | `SettingsStoreTests`（31） | 通過 |
| AT-03～AT-07、AT-09、AT-10 | 裁切、標註、Undo／Redo、文字、合成、錯誤恢復、圖片匯入 | 尚未實作（Phase 3～4） | 尚未驗證 |

Phase 2 其他自動化測試：

| 測試類別 | 涵蓋內容 |
|----------|----------|
| `CaptureFlowTests`（7） | 以測試替身驗證截圖流程：隱藏自家視窗先於快照；完成時裁切出正確顯示器與位置的像素；取消、擷取失敗、選取期間顯示設定變更與未預期例外都會恢復視窗並回到常駐；選取中再要求截圖會被忽略 |
| `AppStateMachineTests`（7） | 常駐 → 擷取 → 選取 → 常駐；截圖或選取中拒絕重入；不合法轉換拋出例外 |
| `CaptureServiceTests`（3） | 真實 GDI 擷取：凍結的 `Bgr32`、96 DPI、尺寸等於要求的實體像素；每個顯示器；重複擷取 20 次 GDI 物件數不增加 |
| `SafeHandleTests`（4） | 螢幕 DC、記憶體 DC、GDI bitmap 各以正確的函式釋放，GDI 物件數回到原值；重複釋放無害 |
| `MonitorServiceTests`（3） | 顯示器數、主螢幕實體尺寸、虛擬桌面範圍與 Windows 系統計量一致；顯示器不重疊 |
| `ClipboardServiceTests`（2） | 剪貼簿圖片的 DIB 格式：24 位元、由下而上、每列補齊 4 位元組、96 DPI（不寫入真實剪貼簿） |

Phase 1 其他自動化測試：

| 測試類別 | 涵蓋內容 |
|----------|----------|
| `HotkeyGestureTests`（34） | 快捷鍵文字解析與格式化、所有支援按鍵往返、修飾鍵規則、拒絕 PrintScreen 與 Win+Shift+S、Win32 鍵碼與旗標對應 |
| `HotkeyServiceTests`（7） | 以真實 `RegisterHotKey`（Ctrl+Alt+Shift+F21／F22）驗證全域占用、變更時先註冊新組合再解除舊組合、新組合衝突時保留原註冊、釋放、`WM_HOTKEY` 分派 |
| `AppSettingsTests`（14） | 預設值（Ctrl+Shift+2、紅色、3px、20px、開機啟動關閉）、常用色、`#RRGGBB` 轉換 |
| `StartupServiceTests`（6） | 以測試專用登錄子機碼驗證啟用（帶引號路徑）、停用、執行檔移動後更新路徑 |
| `ErrorLogTests`（7） | 只記錄情境與例外堆疊、256 KB 輪替保留兩份、單筆截斷、30 天保留期間、目錄無效時不拋出 |
| `GlobalExceptionHandlerTests`（2） | UI 執行緒例外記錄並標記已處理、後續工作繼續執行、通知期間的例外不重複通知 |
| `StringsResourceTests`（7） | 程式碼與 XAML 引用的資源鍵都存在、沒有閒置資源、XAML 沒有寫死的介面文字 |
| `SingleInstanceGuardTests`（4） | 第二個執行個體喚醒第一個、喚醒訊號在開始監聽前送達、無法開啟既有具名物件時仍視為已有執行個體 |
| `DpiAwarenessTests`（1） | Per-Monitor V2 判斷 |
| `AssemblyConfigurationTests`（1） | 中性資源語言為 zh-TW |

## 2. Agent 端實機煙霧驗證

### Phase 1：常駐平台（2026-10-18）

- 腳本：[tools/smoke/Invoke-Phase1Smoke.ps1](../tools/smoke/Invoke-Phase1Smoke.ps1)（實際啟動 Release 建置；以 Win32 與 UI Automation 操作；測試前後還原設定檔與 Run 登錄值）
- 環境：Windows 11 Pro 10.0.26200，單一顯示器 3456×2160、縮放 175%
- 結果：**31 項全數通過**；加入自訂顏色檢查後連續 4 次執行皆無失敗（先前 28 項版本另有連續 7 次無失敗）。完整輸出：[evidence/phase-01/phase1-results.txt](evidence/phase-01/phase1-results.txt)

| 檢查 | 結果 |
|------|------|
| 啟動後只進系統匣：處理程序常駐、圖示存在（`Shell_NotifyIconGetRect`）、沒有可見視窗 | 通過 |
| 首次啟動建立 `settings.json`（預設 Ctrl+Shift+2） | 通過 |
| 處理程序 DPI 感知為 Per-Monitor V2（`GetDpiAwarenessContextForProcess`） | 通過 |
| 執行中占用 Ctrl+Shift+2（外部 `RegisterHotKey` 回傳 1409） | 通過 |
| 再次啟動：第二個執行個體以結束代碼 0 結束、只有一個處理程序、既有執行個體顯示設定視窗 | 通過 |
| 關閉設定視窗後仍常駐；雙擊系統匣圖示開啟設定 | 通過 |
| 系統匣「結束」：處理程序結束（代碼 0）、快捷鍵釋放；重新啟動後快捷鍵再次註冊 | 通過 |
| 快捷鍵已被其他程式占用時仍常駐，設定視窗顯示原因 | 通過 |
| 設定視窗：拒絕缺少修飾鍵的組合、變更快捷鍵立即生效並寫入設定檔、新組合衝突時保留原設定並提示、改回預設並以「確定」關閉 | 通過 |
| 設定視窗切換「登入 Windows 時啟動」：寫入／移除 `HKCU\...\Run` 的帶引號路徑 | 通過 |
| 自訂顏色：開啟 Windows 色彩對話框、輸入 RGB 後確定，清單出現並選取「自訂 #12ABEF」且寫入設定檔；再選回「紅色」 | 通過 |
| 過程中沒有新的錯誤記錄 | 通過 |

截圖：

- 設定視窗：[evidence/phase-01/phase1-settings.png](evidence/phase-01/phase1-settings.png)
- 快捷鍵被占用時的狀態：[evidence/phase-01/phase1-settings-hotkey-in-use.png](evidence/phase-01/phase1-settings-hotkey-in-use.png)
- 變更快捷鍵遇到衝突時的提示：[evidence/phase-01/phase1-settings-rebind-conflict.png](evidence/phase-01/phase1-settings-rebind-conflict.png)
- 自訂顏色加入清單並選取：[evidence/phase-01/phase1-settings-custom-color.png](evidence/phase-01/phase1-settings-custom-color.png)
- 系統匣選單：[evidence/phase-01/phase1-tray-menu.png](evidence/phase-01/phase1-tray-menu.png)

腳本無法確認、需要使用者目視的部分：系統匣圖示的外觀與位置、快捷鍵被占用時的系統通知（可能被「專注」模式抑制）、從檔案總管雙擊執行檔時設定視窗是否來到最前景。

### Phase 2：截圖與區域選取（2026-10-18）

- 腳本：[tools/smoke/Invoke-Phase2Smoke.ps1](../tools/smoke/Invoke-Phase2Smoke.ps1)，搭配已知圖樣視窗 [tools/smoke/Show-TestPattern.ps1](../tools/smoke/Show-TestPattern.ps1)（每個像素的顏色由座標決定，截圖結果可逐像素比對出任何偏移）。實際啟動 Release 建置，以模擬的鍵盤滑鼠操作；開始前等待使用者閒置，Esc 只在覆蓋層位於前景時送出，快捷鍵只在確認由本程式持有時送出；證據只裁切圖樣範圍。
- 環境：Windows 11 Pro 10.0.26200，單一顯示器 3456×2160、縮放 175%；圖樣視窗位於主螢幕中央 (1128,680) 1200×800。
- 結果：**38 項全數通過**。完整輸出：[evidence/phase-02/phase2-results.txt](evidence/phase-02/phase2-results.txt)；循環量測：[evidence/phase-02/phase2-resources.csv](evidence/phase-02/phase2-resources.csv)。

| 檢查 | 結果 |
|------|------|
| 圖樣視窗在螢幕上與圖樣逐像素一致（比對基準本身正確） | 通過 |
| 其他程式位於前景時按快捷鍵：開啟覆蓋層（約 0.3～0.5 秒，含輪詢間隔）、覆蓋層取得前景 | 通過 |
| 覆蓋層視窗矩形等於顯示器實體邊界、DPI 與顯示器相同、置頂且不出現在工作列 | 通過 |
| 選取中，覆蓋層在選取範圍內顯示的快照與實際畫面逐像素一致（沒有 DPI 偏移或縮放） | 通過 |
| 左上→右下、右下→左上拖曳：剪貼簿圖片尺寸包含起點與終點像素，內容與圖樣逐像素一致 | 通過 |
| 自家設定視窗擋在圖樣上：截圖期間隱藏、沒有被截入、之後恢復顯示 | 通過 |
| 系統匣「開始截圖」：選單出現在圖樣上，截圖不包含選單 | 通過 |
| Esc、滑鼠右鍵取消：覆蓋層全部關閉，剪貼簿不變 | 通過 |
| 原地點一下：不產生截圖、維持選取，之後可用 Esc 取消 | 通過 |
| 選取中再按快捷鍵：不重入，覆蓋層不變 | 通過 |
| 選取期間顯示設定變更（送出 `WM_DISPLAYCHANGE` 模擬）：關閉覆蓋層、程式繼續常駐、可再次截圖、故障日誌只多一筆「截圖失敗」 | 通過 |
| 30 次循環（每次一次完成並逐像素比對＋一次 Esc 取消）全部成功 | 通過 |
| 30 次循環資源：GDI 物件恆為 38；USER 物件 52～54；Handle 595～600；私有記憶體 286～381 MB 隨垃圾回收起伏，後 10 次平均比前 10 次少 28 MB | 通過（無成長趨勢） |
| 系統匣「結束」：處理程序結束（代碼 0）、快捷鍵釋放 | 通過 |

截圖：

- 選取前的覆蓋層（整個畫面暗化）：[evidence/phase-02/phase2-overlay-idle.png](evidence/phase-02/phase2-overlay-idle.png)
- 選取中（選取範圍不暗化、實體像素外框、尺寸標籤）：[evidence/phase-02/phase2-overlay-selecting.png](evidence/phase-02/phase2-overlay-selecting.png)
- 設定視窗擋在圖樣上（截圖前）：[evidence/phase-02/phase2-settings-over-pattern.png](evidence/phase-02/phase2-settings-over-pattern.png)
- 系統匣選單出現在圖樣上（截圖前）：[evidence/phase-02/phase2-tray-menu.png](evidence/phase-02/phase2-tray-menu.png)

驗證過程中發現並修正的產品問題：`Clipboard.SetImage` 每次留下一個 GDI 物件（改以 DIB 位元組提供剪貼簿圖片）；設定視窗淡出殘影被截入（隱藏前關閉轉場動畫）；UI 執行緒延後處理時選取起點落在游標稍後的位置（改取事件本身的位置）；175% 下選取框蓋住最後一列像素（改以實體像素計算外框）。

本次執行後，腳本的剪貼簿備份由「只還原文字」改為「盡力備份並還原所有格式」（不影響上述檢查項目）；修改後已以腳本語法剖析與實際的圖片備份還原確認。

腳本無法確認、需要使用者實機驗證的部分：多螢幕、混合 DPI、負座標與上下排列（本機只有一個顯示器）；100%、125%、150%、200% 縮放；在瀏覽器與 PowerPoint 位於前景時觸發；實際變更解析度或插拔顯示器。

## 3. 使用者實機驗收（MT）

| 編號 | 內容 | 提出時機 | 狀態 |
|------|------|----------|------|
| MT-01 | 首次啟動只進系統匣；再次啟動不重複常駐；退出後重新啟動快捷鍵可用 | Phase 1 | 尚未驗證 |
| MT-02 | 快捷鍵被占用時有明確提示 | Phase 1 | 尚未驗證 |
| MT-02 | 預設及自訂快捷鍵在桌面、瀏覽器及 PowerPoint 中可以啟動截圖 | Phase 2 | 尚未驗證 |
| MT-03 | 取消不留下遮罩（Phase 2 可先驗證）；拖曳選取後自動開啟編輯器、不影響先前文件 | Phase 2／3 | 尚未驗證 |
| MT-04 | 單螢幕 100%、125%、150%、200% 縮放下，選取與 PNG 尺寸正確 | Phase 2 | 尚未驗證 |
| MT-05 | 雙螢幕不同 DPI、負 X 座標、上下排列、非主螢幕截圖正確 | Phase 2 | 尚未驗證 |
| MT-06 | 微軟注音輸入繁體中文並重新編輯 | Phase 3 | 尚未驗證 |
| MT-07 | 25%、100%、400% Zoom 操作後匯出結果一致 | Phase 4 | 尚未驗證 |
| MT-08 | 貼入 PowerPoint 與 PNG 一致；Word／Teams 可接受 | Phase 4 | 尚未驗證 |
| MT-09 | 磁碟不可寫、剪貼簿被占用、顯示器中途拔除後可回復 | Phase 4 | 尚未驗證 |
| MT-10 | 30 次循環 GDI Handle 與記憶體無持續成長 | Phase 5 | 尚未驗證 |
| MT-11 | 無 .NET Runtime 的乾淨 Windows 11 x64 可執行 | Phase 5 | 尚未驗證 |

### Phase 1 驗證步驟（MT-01、MT-02 占用提示）

執行檔：`ScreenshotApp\bin\Release\net10.0-windows\ScreenshotApp.exe`（先執行 `dotnet build ScreenshotApp.sln -c Release`）。

1. 在檔案總管雙擊執行檔：不應出現任何視窗，通知區域（可能在「顯示隱藏的圖示」內）出現藍色截圖圖示；滑鼠停在圖示上顯示「ScreenshotApp（快捷鍵：Ctrl+Shift+2）」。
2. 再雙擊一次執行檔：不會出現第二個圖示，而是跳出「ScreenshotApp 設定」視窗並位於最前景。
3. 右鍵系統匣圖示選「結束」：圖示消失，工作管理員中沒有 ScreenshotApp。再啟動一次，圖示恢復，沒有快捷鍵失敗的通知。
4. 占用提示（擇一）：
   - 在設定視窗把快捷鍵改成另一個常駐程式正在使用的全域快捷鍵後按「套用」，應看到紅字「無法使用 …（此組合已被其他程式或 Windows 使用），仍保留原設定 …」，原快捷鍵仍有效。
   - 先結束 ScreenshotApp，再以 `pwsh -File tools\smoke\Invoke-Phase1Smoke.ps1` 執行煙霧腳本：腳本會先占用 Ctrl+Shift+2 再啟動程式，此時應出現「無法註冊快捷鍵 Ctrl+Shift+2」的系統通知（若開啟「專注」模式則可能不顯示）。

> 註：Phase 1 只完成常駐平台；截圖於 Phase 2 接上。

### Phase 2 驗證步驟（MT-02 觸發、MT-04、MT-05；MT-03 的取消部分）

執行檔同上。Phase 2 選取完成後會**直接複製到剪貼簿**並以系統通知顯示尺寸（編輯器於 Phase 3 接上）。

1. **MT-02（觸發截圖）**：分別讓桌面、瀏覽器、PowerPoint 位於前景，按 `Ctrl+Shift+2`：畫面應立即凍結並變暗；拖曳選取後放開，通知顯示「已將 W × H 像素的截圖複製到剪貼簿」，在 PowerPoint 或小畫家貼上應得到選取的畫面。之後在設定視窗改用其他快捷鍵，再重複一次。
2. **MT-03（取消部分）**：按 `Esc` 或滑鼠右鍵，所有變暗的畫面立即消失，剪貼簿內容不變；在原地點一下（沒有拖曳）不會產生截圖，畫面維持在選取狀態。
3. **MT-04（單螢幕各縮放）**：在「設定 → 系統 → 顯示器 → 縮放」分別設為 100%、125%、150%、200%，每種縮放下：
   - 變暗的畫面完整蓋住螢幕，邊緣沒有未變暗的縫隙，畫面內容與原本的位置完全重合（沒有放大、縮小或位移）。
   - 選取一個已知寬度的物件，選取框旁的尺寸、通知中的尺寸與貼上後的圖片尺寸一致，且內容正是選取框內的畫面。
   - 也可以執行 `pwsh -File tools\smoke\Invoke-Phase2Smoke.ps1 -Cycles 3`（約 1 分鐘，開始後請勿操作滑鼠鍵盤），所有項目應為 PASS。
4. **MT-05（雙螢幕）**：測試兩個顯示器縮放不同（例如 100% 與 150%）、第二個顯示器在主螢幕左側（負 X 座標）、上下排列，並在非主螢幕上截圖：
   - 每個顯示器都會變暗且完整貼合；在任一顯示器拖曳都能選取，拖到另一個顯示器時選取框停在起始顯示器的邊緣。
   - 貼上的圖片內容與選取框一致、沒有偏移或縮放，尺寸與通知一致。
   - 煙霧腳本會檢查每個顯示器覆蓋層的邊界與 DPI；圖樣比對只在主螢幕進行。
