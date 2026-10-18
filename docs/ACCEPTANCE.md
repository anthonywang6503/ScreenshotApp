# 驗收紀錄

記錄自動化測試、Agent 端實機煙霧驗證與使用者實機驗收（MT）的結果。未經實際操作確認的項目一律標示「尚未驗證」，不以「編譯成功」取代操作驗收。

## 1. 自動化測試

最近一次執行：2026-10-18，`dotnet test ScreenshotApp.sln -c Release`，**114 passed / 0 failed**。

| 規格驗收項目 | 範圍 | 測試類別 | 狀態 |
|--------------|------|----------|------|
| AT-08 設定 | 正常載入、快捷鍵變更、JSON 損壞復原（含備份原檔）、無效欄位只重設該欄位、讀取失敗、權限不足（唯讀檔）、無效路徑、原子寫入 | `SettingsStoreTests`（31） | 通過 |
| AT-01～AT-07、AT-09、AT-10 | 座標、選取、裁切、標註、Undo／Redo、文字、合成、錯誤恢復、圖片匯入 | 尚未實作（Phase 2～4） | 尚未驗證 |

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

## 3. 使用者實機驗收（MT）

| 編號 | 內容 | 提出時機 | 狀態 |
|------|------|----------|------|
| MT-01 | 首次啟動只進系統匣；再次啟動不重複常駐；退出後重新啟動快捷鍵可用 | Phase 1 | 尚未驗證 |
| MT-02 | 快捷鍵被占用時有明確提示 | Phase 1 | 尚未驗證 |
| MT-02 | 預設及自訂快捷鍵在桌面、瀏覽器及 PowerPoint 中可以啟動截圖 | Phase 2 | 尚未驗證 |
| MT-03 | 拖曳選取後自動開啟編輯器；取消不留下遮罩，且不影響先前文件 | Phase 3 | 尚未驗證 |
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

> 註：Phase 1 只完成常駐平台，按下快捷鍵**尚不會截圖**（截圖於 Phase 2 接上）。
