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
