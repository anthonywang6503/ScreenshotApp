# ADR-0023：垂直長截圖與自動捲動

- **狀態**：Accepted（實作決策；跨應用程式的桌面相容性尚未驗證）
- **日期**：2026-11-02

## 決策

1. 系統匣新增長截圖；第二個 `HotkeyService` 管理選用快捷鍵，預設未設定。不得與一般截圖相同；新組合註冊失敗時保留舊組合，清除時解除註冊。JSON 缺少或 null 代表未設定，無效或重複欄位只重設長截圖快捷鍵。
2. 沿用選取覆蓋層與 Ctrl 放大鏡。長截圖模式停用 C 取色與 R 量測，選取完成後進入捲動工作階段。`CaptureFlow` 在整段期間保持 Selecting，延後釋放隱藏視窗的 scope；所有完成、取消與例外路徑恢復原狀態。只有成功結果經既有 `RecordCapture`／`OpenEditor` 加入歷史與編輯器。
3. `CaptureService.Capture(PixelRect)` 已提供區域 BitBlt／GetDIBits 與 SafeHandle，不新增另一套擷取。四條不啟用、滑鼠穿透的 HWND 完全位於取樣範圍之外。面板優先置於範圍右、左、下、上；沒有空間或被移入範圍時，在每次取樣前隱藏並等待兩次 DwmFlush，再恢復。面板不擷取進結果，但此情境可能閃爍。
4. 每次輪詢間隔 150ms（不含擷取與處理時間），連續三個逐像素相同影格才交給拼接。Bgr32 第四個未定義位元組歸零。背景執行緒從凍結點陣圖複製像素及計算列雜湊；UI 執行緒更新工作階段與拼接狀態，不並行修改模型。27 次取樣仍不穩定時停止自動捲動並提示；這些數值是目前實作常數，尚非跨應用程式實測的最佳值。
5. `ScrollStitcher` 以首次成功位移前後相同的前綴／後綴列辨認固定標頭／頁尾，之後要求固定帶保持相同。列雜湊上的 KMP 尋找全部後綴／前綴重疊，再逐像素驗證候選。最小重疊為 `max(2, min(32, 內容區高度 / 4))` 列；只接受唯一向下候選且沒有向上候選。重複區塊、變動畫面或無重疊均不猜測；保留最後成功影格，讓使用者捲回重疊位置重試。固定帶過大、細小範圍、捲動條與動畫可能令對齊失敗。
6. 初始標頭保存一次，後續僅保存新列，頁尾最後加一次；每次附加不重建完整長圖。預覽直接抽樣各段，不配置原尺寸預覽。高度上限 32,768，總像素上限 100,000,000；最大高度是兩者取小並向下取整。初始影格超限拒絕開始，附加時只保留可容納的完整新列，達限停止取樣並提示按完成。完成時才配置完整凍結點陣圖，記憶體不足回報失敗並恢復舊文件。
7. 自動捲動採 `PostMessage(WM_MOUSEWHEEL, -120)`，送至開始時範圍中心的視窗，不搬移游標、不送全域 SendInput、不裝低階 Hook。每次送出前確認 HWND／程序仍存在、範圍中心仍屬於原根視窗、前景是原程式或控制面板、沒有使用者輸入。超出訊息有號 16 位元座標範圍時停止自動、可改手動。無法送出、使用者移動滑鼠／按鍵或按停止時回到手動。
8. `GetLastInputInfo` 的時間戳記使用不相等而非遞增比較，另比較游標位置與按住的按鍵。定向視窗訊息不產生自身全域輸入；每次輪詢及送出前檢查。三次捲動後的穩定畫面均無變化，且這輪自動捲動至少成功附加過一次，才自動完成；完全沒有進展時提示「可能到底或不支援」，保留手動與完成入口。
9. 擷取失敗、顯示變更或範圍中心的原視窗被遮住／關閉時取消。所有自家框線及面板在 finally 釋放，取消不寫歷史、不取代文件。手動模式不需要全域鍵盤攔截；Esc 只在面板有焦點時取消，其他時候使用面板取消。

## 實測界線與替代方案

開始本階段時，使用者的 ScreenshotApp（PID 35760）正在使用標準 Release 建置。依桌面保護規則，未關閉該程式、未發送測試輸入。P13-00 對 Edge、Chrome、Word、VS Code、檔案總管與已知圖樣的兩種注入方式都記為 NOT_RUN；[測試矩陣](../evidence/phase-13/wheel-probe-matrix.csv) 與[重跑工具](../../tools/smoke/Invoke-ScrollWheelProbe.ps1) 保留後續實測路徑。

因此 PostMessage 是依既有 WPF 滾輪觀察與官方訊息契約選定的保守初始方案，沒有宣稱相容性已驗證。SendInput 需要改變游標與區分自身輸入，目前不作產品的自動回退；不接受訊息、較高完整性層級或無法可靠對齊的程式改用手動。若後續實測需要改變方案，修訂本決策並保留回歸。

Microsoft 文件：[WM_MOUSEWHEEL](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-mousewheel) 定義滾輪單位、桌面座標及向父視窗傳遞；[GetLastInputInfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getlastinputinfo) 說明時間戳記不保證遞增。文件契約不是實機相容性證據。

## 驗證

- [ScrollStitcherTests](../../ScreenshotApp.Tests/ScrollStitcherTests.cs)：合成連續內容、固定帶、前後位移、重複圖樣、損壞與不變影格、完整列上限及預覽。
- [ScrollCaptureSessionTests](../../ScreenshotApp.Tests/ScrollCaptureSessionTests.cs)：三張穩定影格、自動到底、無回應、停止與持續動畫。
- [CaptureFlowTests](../../ScreenshotApp.Tests/CaptureFlowTests.cs)、[整合測試](../../ScreenshotApp.Tests/ScrollCaptureIntegrationTests.cs)：座標、重入、全部結束路徑、輸入中止、設定、快捷鍵衝突及未顯示視窗的元素。
- [Phase 13 煙霧](../../tools/smoke/Invoke-Phase13Smoke.ps1)：可捲動已知圖樣、手動與自動結果逐像素比對、取消框線與資源量測。實際狀態以[驗收紀錄](../ACCEPTANCE.md)為準。

## 既有決策修訂

本決策補完 [擷取策略](0007-capture-strategy.md) 的區域輪詢、[覆蓋層](0008-overlay-placement-and-input.md) 的長截圖選取模式、[狀態機](0009-app-state-machine.md) 的整段 Selecting 保護，以及[設定](0004-settings-persistence.md) 的選用快捷鍵；一般截圖、取色與量測流程不變。
