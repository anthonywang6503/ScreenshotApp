# Changelog

本專案所有重要變更記錄於此。格式依循 Keep a Changelog，版本號依循語意化版本。

## [Unreleased]

### Added
- Phase 0（規劃）：建立 `.planning/2026-10-18-screenshotapp-v1/` 規劃檔案、Phase 0～5 完整 Checklist、FR 追蹤矩陣、ADR 清單與 handoff 規範。
- 建立 git 儲存庫、`.gitignore`、專案層級 `nuget.config`（nuget.org 來源）。
- Phase 1（Windows 平台基礎）：
  - 啟動後只常駐系統匣（選單「設定」「結束」，雙擊開啟設定，工具提示顯示快捷鍵狀態）；同一登入工作階段只有一個執行個體，再次啟動會顯示既有執行個體的設定視窗。
  - 全域截圖快捷鍵：預設 `Ctrl+Shift+2`、`MOD_NOREPEAT`；拒絕 PrintScreen 與 Win+Shift+S 等 Windows 保留組合；被占用時程式仍可使用並顯示原因；變更時先註冊新組合，成功後才解除舊組合。按下快捷鍵的截圖動作於 Phase 2 接上。
  - 設定視窗與 `%LocalAppData%\ScreenshotApp\settings.json`：原子寫入；損毀時備份原檔並回復預設值，個別無效欄位只重設該欄位，皆會提示。
  - 「登入 Windows 時啟動」（`HKCU\...\Run`，預設關閉）、系統匣「結束」時解除快捷鍵並移除圖示。
  - 故障日誌：只記錄錯誤訊息與堆疊，256 KB 輪替保留兩份，超過 30 天刪除；UI 執行緒的未處理例外會記錄並提示，程式繼續執行。
  - Per-Monitor V2 DPI、台灣繁體中文介面文字集中於 `Strings.resx`、多尺寸應用程式圖示。
  - ADR-0001～0006、114 個自動化測試、實機煙霧驗證腳本 `tools/smoke/Invoke-Phase1Smoke.ps1` 與 `docs/ACCEPTANCE.md`。
- Phase 2（截圖與區域選取）：
  - 快捷鍵與系統匣新增的「開始截圖」：先隱藏自家視窗（關閉淡出動畫並等待桌面合成器），再以 GDI `BitBlt` 逐顯示器擷取靜態快照。
  - 每個顯示器一個選取覆蓋層，直接以該顯示器的實體像素邊界建立；暗化遮罩、選取框、實體像素尺寸標籤、十字游標；選取限於起始顯示器，可選到最後一行與最後一列。
  - `Esc` 或滑鼠右鍵取消；原地點一下不產生截圖並維持選取；截圖或選取中再按快捷鍵不重入；選取期間顯示設定變更時安全取消並提示。
  - 選取完成後暫時直接複製到剪貼簿（24 位元 DIB）並以通知顯示尺寸，編輯器於 Phase 3 接上。
  - 座標模型 `PixelRect`／`PixelPoint` 與集中換算的 `CoordinateMapper`；三種 GDI `SafeHandle`；應用程式狀態機與可測試的截圖流程。
  - ADR-0007～0009、`docs/KNOWN_LIMITATIONS.md` 初稿、實機煙霧驗證腳本 `tools/smoke/Invoke-Phase2Smoke.ps1`（以已知圖樣逐像素比對）。

### Fixed
- Phase 2 煙霧驗證發現並修正：
  - `Clipboard.SetImage` 每次複製都會留下一個 GDI 物件，改以 DIB 位元組提供剪貼簿圖片。
  - 隱藏設定視窗時的淡出殘影會被截入，改為隱藏前關閉該視窗的轉場動畫。
  - 覆蓋層剛顯示就開始拖曳時，選取起點可能落在游標稍後的位置；改取滑鼠事件本身的位置。
  - 175% 縮放下選取框會蓋住選取範圍最後一列像素；改為以實體像素計算的外框。
