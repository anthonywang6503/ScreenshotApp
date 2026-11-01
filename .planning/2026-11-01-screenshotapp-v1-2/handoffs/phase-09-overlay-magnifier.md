# Handoff — Phase 9：選取放大鏡與螢幕取色

**日期**：2026-11-01
**狀態**：程式、測試、文件與煙霧腳本完成；實機項目依計畫允許標記尚未驗證，詳見下方。
**分支**：`feature/phase-09-overlay-magnifier`；Phase 8 已獨立提交 `b48b6fe`。
**Git**：使用者已授權每個 Phase 完成後提交；本交接隨 Phase 9 在目前分支提交，不合併、不推送、不建立 PR。

## 完成內容

- [ADR-0019](../../../docs/adr/0019-overlay-magnifier-and-color-pick.md)：15×15 格、每格 12 實體像素、快照取樣、四角翻轉／灰格、Ctrl 啟動抑制與取色結果分流。
- [幾何](../../../ScreenshotApp/Capture/MagnifierGeometry.cs)、[輸入狀態](../../../ScreenshotApp/Capture/MagnifierInput.cs)、[覆蓋層](../../../ScreenshotApp/Capture/SelectionOverlay.xaml.cs)：最近鄰繪製、黑白中心框、座標／色碼、未拖曳與拖曳中更新；捕捉或焦點遺失時隱藏。
- [覆蓋層結果](../../../ScreenshotApp/Capture/OverlayResult.cs) 與 [CaptureFlow](../../../ScreenshotApp/Capture/CaptureFlow.cs)：區域選取與取色分流；取色不裁切、不替換既有文件，先關閉覆蓋層及恢復視窗，再由 AppController 複製 Unicode 文字並通知。
- [文字剪貼簿](../../../ScreenshotApp/Export/ClipboardService.cs) 以委派測試成功與失敗，沿用 WPF 內建有限重試；所有新介面文字在 Strings.resx。
- 使用指南、架構、限制、驗收紀錄與 CHANGELOG 已同步；編輯器取色仍留在 Phase 11。

## 驗證

- `pwsh -NoProfile -File tools/Invoke-Quality.ps1`：Release 0 警告／0 錯誤、991／991 測試通過，無跳過。
- 新增 34 個測試案例；未顯示覆蓋層在中央及四角各以 RenderTargetBitmap 比對 32,400 個像素，沒有混色、取樣偏移或中心框差異。這不替代桌面實機驗收。
- [自動化摘要](../../../docs/evidence/phase-09/automated-validation.json) 包含組件 SHA-256；原始 TRX 位於摘要所記載的本機 artifacts 目錄，該目錄不提交。
- 煙霧腳本與共用工具的 PowerShell AST 語法檢查通過。啟動前偵測到既有 ScreenshotApp，結果 [NOT_RUN](../../../docs/evidence/phase-09/phase9-results.txt)，未送出任何模擬輸入。
- 文件相對連結、UTF-8／LF、`git diff --check` 於提交前檢查。

## 尚未驗證與後續入口

使用者的 ScreenshotApp 仍在執行，因此未結束其處理程序或取代文件。實際 Ctrl／C／拖曳／取消、剪貼簿通知、PrintWindow 像素比對與多螢幕混合 DPI 尚未驗證。結束既有程式後，在閒置時可執行：

```powershell
pwsh -NoProfile -STA -File tools/smoke/Invoke-Phase9Smoke.ps1 -ExePath ScreenshotApp/bin/Release/net10.0-windows/ScreenshotApp.exe -EvidenceDirectory docs/evidence/phase-09
```

腳本會先等至少 90 秒閒置；已有程式、快捷鍵被占用、登入啟動登錄值存在或閒置逾時時，不操作桌面並記錄尚未驗證。設定與日誌以記憶體備份，剪貼簿在沒有外部更新時還原；只終止腳本自己建立的處理程序。Phase 4 新增 `LibraryOnly` 入口供共用工具載入，未變更原本的預設驗證流程。

Phase 10 維持 pending。本次停在 Phase 9 控制點；續作先載入計畫、進度與發現，再實作效果標註與 ADR-0020。
