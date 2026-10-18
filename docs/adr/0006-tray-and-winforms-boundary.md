# ADR-0006：系統匣與 Windows Forms 使用邊界

- **狀態**：Accepted
- **日期**：2026-10-18

## 背景

WPF 沒有系統匣 API，也沒有色彩選擇對話框。規格允許以 Windows Forms `NotifyIcon` 實作系統匣，但主要介面一律使用 WPF。

## 決策

1. **允許使用的 Windows Forms 型別**僅限：`NotifyIcon`、`ContextMenuStrip`（含其選單項目）與 `ColorDialog`。其他介面一律為 WPF。
2. Windows Forms 程式碼集中於 `Platform/TrayService` 與色彩選擇輔助方法；專案移除 `System.Windows.Forms` 的隱含 `using`，引用時使用完整命名空間或別名，避免與 WPF 型別混用。
3. **訊息迴圈**：不呼叫 `System.Windows.Forms.Application.Run`；WPF `Dispatcher` 的訊息迴圈同時處理 `NotifyIcon` 隱藏視窗的訊息。
4. **外觀**：`app.manifest` 宣告 Common Controls v6 相依性，讓 Windows Forms 對話框與 Win32 訊息方塊使用現代外觀。
5. **DPI**：處理程序為 Per-Monitor V2；系統匣圖示依小圖示系統尺寸從內嵌的多尺寸 `app.ico` 載入；選單縮放需實機確認。
6. **選單內容**：只放已實作的功能，不放占位項目。Phase 1 為「設定」「結束」；「開始截圖」與「開啟圖片」隨功能完成加入。雙擊圖示開啟設定。
7. **通知**：`ShowBalloonTip` 在 Windows 11 會成為系統通知，可能被「專注」模式抑制；因此重要狀態（例如快捷鍵註冊失敗）同時反映在系統匣工具提示與設定視窗中，不只依賴通知。
8. **釋放**：結束時明確 `Dispose` `NotifyIcon`，讓圖示立即從通知區域移除。

## 考慮過的替代方案

- **自行以 `Shell_NotifyIcon` P/Invoke 實作**：需自行處理視窗訊息、Explorer 重新啟動（`TaskbarCreated`）與選單定位，`NotifyIcon` 已處理這些細節。
- **第三方 WPF 系統匣套件**：增加外部相依，違反不引用套件的原則。

## 影響

- 自包含發佈包含 Windows Forms 執行階段。
- Windows Forms 與 WPF 的邊界清楚，審查時可快速確認沒有以 Windows Forms 實作主要介面。
