# Handoff — Phase 13：手動／自動長截圖

**日期**：2026-11-02

**交付狀態**：程式、測試、文件及重跑腳本完成。桌面操作與 P13-00 滾輪探測尚未驗證，不能視為 MT-13 通過。Phase 14 維持 pending。

**分支／基準**：`feature/phase-13-scrolling-capture`，基準 `0d29a591a69b66b8787ac800fe93e9e4b51f51ec`。依使用者授權在目前分支提交，不合併、不推送、不建立 PR；本階段提交由 Git 記錄。

## 已交付

- [ADR-0023](../../../docs/adr/0023-scrolling-capture.md) 固定長截圖的入口、取樣、對齊、固定帶、定向滾輪、輸入停止、上限與恢復路徑；設定與狀態機 ADR 同步修訂。
- [ScrollFrame](../../../ScreenshotApp/Capture/ScrollFrame.cs) 正規化 Bgr32 與列雜湊；[ScrollStitcher](../../../ScreenshotApp/Capture/ScrollStitcher.cs) 以 KMP 列候選與逐像素確認，只接受唯一向下重疊，固定標頭／頁尾只存一次。向上、重複圖樣、變動畫面或無可靠重疊不附加；已接受內容不受破壞。
- [ScrollCaptureSession](../../../ScreenshotApp/Capture/ScrollCaptureSession.cs) 等三張相同影格才處理；自動每次送一格，曾成功前進後三次無變化才完成，完全無回應提示手動；動畫逾時、無法對齊及使用者介入回到手動。
- [面板](../../../ScreenshotApp/Capture/ScrollCaptureWindow.xaml.cs) 包含高度、抽樣預覽、自動／停止、完成及取消；[四邊框](../../../ScreenshotApp/Capture/ScrollCaptureFrame.cs) 均在取樣之外。面板重疊時先隱藏並等 DWM。沿用區域 BitBlt 與 SafeHandle；高 32,768、總像素 100,000,000，僅附加可容納完整列。
- [ScrollInput](../../../ScreenshotApp/Capture/ScrollInput.cs) 在發送前確認原 HWND／程序、範圍中心、前景及輸入；GetLastInputInfo 時間戳記以不相等判定，另查游標與按住的按鍵。不移游標、不送全域輸入、不使用低階 Hook。PostMessage 相容性尚未驗證。
- [CaptureFlow](../../../ScreenshotApp/Capture/CaptureFlow.cs) 將整段捲動包在 Selecting 與隱藏視窗 scope 中；所有出口回復原狀態。完成經既有 AppController 路徑建立 ImageDocument、RecordCapture 與 OpenEditor；取消／失敗不取代文件或新增歷史。
- 系統匣獨立入口；設定 `ScrollHotkey` 可為 null，預設未設定，不能與一般截圖相同；獨立註冊、衝突保留舊值、清除解除。長截圖選取模式保留 Ctrl，停用 C／R；一般截圖不變。

## 自動化與版面驗證

- 最終品質腳本：Release 0 警告／0 錯誤、1176／1176 通過、無跳過，比 Phase 12 新增 49 個案例。[摘要、TRX 與受測組件 SHA-256](../../../docs/evidence/phase-13/automated-validation.json)。
- 因使用者程式鎖住標準 Release 輸出，以環境屬性隔離建置，未修改品質腳本或關閉使用者程式：

```powershell
$env:BaseOutputPath = 'bin/phase13/'
pwsh -NoProfile -File tools/Invoke-Quality.ps1
```

- 合成像素驗證固定帶與不同位移、往回／歧義／失敗不改變前段、完整列上限、縮圖；純工作階段驗證穩定、自動到底、無回應、動畫與停止。流程替身確認負座標、完成、取消、擷取失敗、顯示變更、重入與原視窗恢復。
- 跨模組驗證 600×1600、1×32768、2×32768 的歷史保存／還原與共用 ImageRenderer 合成逐像素相同。磁碟只使用暫存目錄；測試快捷鍵只使用測試組合，不寫登入啟動登錄值或使用者剪貼簿。
- 未顯示 WPF 內容的 RenderTargetBitmap 確認面板與設定欄位可見，預覽位於忽略的 `artifacts/phase13-ui/`；不是桌面操作證據。
- 三支煙霧／圖樣腳本 AST 與內嵌 C# 編譯通過。方法群組相容性在首次整合修正；清除長截圖快捷鍵後保留舊錯誤的問題先以失敗回歸重現，再修正。

## 尚未驗證與重跑

本次 [煙霧結果](../../../docs/evidence/phase-13/phase13-results.txt) 及 [12 格滾輪矩陣](../../../docs/evidence/phase-13/wheel-probe-matrix.csv) 均為 NOT_RUN。前置守衛發現使用者 ScreenshotApp PID 35760，沒有送出桌面輸入或修改使用者設定／剪貼簿／歷史。P13-00 的實測未完成，PostMessage 是待相容性驗證的初始方案，150ms／三影格／三次無變化是實作常數，不能宣稱為各應用程式實測最佳值。

在隔離測試桌面、既有程式結束且閒置至少 90 秒後，以前述隔離建置執行：

```powershell
pwsh -NoProfile -STA -File tools/smoke/Invoke-Phase13Smoke.ps1 -ExePath ScreenshotApp/bin/phase13/Release/net10.0-windows/ScreenshotApp.exe -EvidenceDirectory docs/evidence/phase-13
```

腳本關閉歷史，備份並還原設定與剪貼簿，登入啟動值存在時不開始，只結束自己建立的程序。可捲動圖樣固定標頭 24px、內容 600×1600、每格 80px；手動三格預期 600×640，自動到底預期 600×1600，逐像素比對原始連續圖樣；另測取消殘留視窗與資源數。執行期間不要執行其他指令搶走前景；使用者輸入時立即停止。

對每種應用程式使用無敏感內容、可向下捲動的測試文件，目標視窗先在前景，再以十進位 HWND 執行：

```powershell
pwsh -NoProfile -STA -File tools/smoke/Invoke-ScrollWheelProbe.ps1 -WindowHandle <十進位HWND> -ApplicationName 'Edge' -EvidenceDirectory docs/evidence/phase-13
```

`ApplicationName` 可為 Edge、Chrome、Word、VS Code、Explorer、Pattern。工具比較 SendInput（游標中心）與 PostMessage，記錄版本、畫面雜湊是否變化及三張穩定影格所需時間，不保存螢幕內容；無變化也可能已到底，須搭配測試文件與實際觀察判斷。既有 ScreenshotApp 或未指定目標時只產生 NOT_RUN。

尚待確認：瀏覽器／Word／程式碼編輯器與已知圖樣的兩種捲動、真實滑鼠穿透、輸入中止、到底、取消、顯示變更清理、混合 DPI、框外面板及資源趨勢。這些是 [MT-13](../../../docs/ScreenshotApp.Spec.md) 與下一階段整合驗證的續作項目。

## Phase 14 接點

本次沒有開始發佈、版本更新、標籤或封裝。續作先補齊上述桌面證據，必要時依實測修訂 ADR-0023 與注入方式，再執行原計畫的整合、文件總檢及 v1.2.0 發佈。未經重跑的項目繼續標示尚未驗證，不以自動化通過取代 MT。
