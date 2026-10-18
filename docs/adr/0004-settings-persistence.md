# ADR-0004：設定持久化——settings.json、原子寫入、損毀回復

- **狀態**：Accepted
- **日期**：2026-10-18

## 背景

規格要求設定至少包含截圖快捷鍵、開機啟動、上次儲存位置、預設顏色及線寬，儲存在 `%LocalAppData%\ScreenshotApp\settings.json`。JSON 損毀、欄位無效或讀取失敗時須安全回復預設值並提示，不得導致常駐程式無法啟動；程式不得提高權限。

## 決策

1. **路徑**：`Environment.SpecialFolder.LocalApplicationData` 下的 `ScreenshotApp\settings.json`。`SettingsStore` 以建構式取得檔案路徑，測試注入暫存目錄，絕不觸碰真實設定。
2. **格式**：`System.Text.Json`，UTF-8、縮排輸出。欄位：`Hotkey`（例如 `"Ctrl+Shift+2"`）、`StartWithWindows`、`LastSaveDirectory`、`DefaultColor`（`"#RRGGBB"`）、`DefaultStrokeWidth`、`DefaultFontSize`。未知欄位忽略。
3. **讀取**：
   - 檔案不存在 → 使用預設值，並寫出一份預設檔。
   - 讀取失敗（權限、I/O、無效路徑）→ 使用預設值並回報警告，不拋出例外。
   - JSON 無法解析 → 使用預設值並回報警告；原檔先複製為 `settings.corrupt.json` 保存，避免下次寫入時無聲覆蓋使用者資料。
   - 個別欄位型別錯誤或超出範圍 → 只有該欄位回到預設值，其餘欄位保留，並回報無效欄位名稱。
4. **寫入**：序列化後先寫入同目錄的暫存檔，再以 `File.Move(..., overwrite: true)` 取代正式檔（同一磁碟區上為原子操作），避免寫到一半當機留下半截檔案。失敗時回傳錯誤結果，不拋出例外，記憶體中的設定保持不變。
5. **提示**：載入警告於啟動時以系統匣通知顯示（繁中文字由 UI 層依警告種類產生）。
6. **開機啟動**：偏好值存於 `settings.json`；實際生效的登錄值由 `StartupService` 寫入 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`，啟動時依偏好值同步（可攜版移動位置後會更新路徑）。

## 考慮過的替代方案

- **登錄機碼存放全部設定**：不易檢視與備份，與規格指定路徑不符。
- **整檔失敗即全部重設**：會因一個欄位錯誤而遺失其他有效設定。

## 影響

- 設定檔可手動檢視與修改；異常情況皆可回復且有提示。
- 寫入需要目錄可寫；唯讀或權限不足時設定僅在本次執行有效，並明確告知使用者。
