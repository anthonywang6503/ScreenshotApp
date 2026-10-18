# ADR-0007：截圖策略——逐顯示器 BitBlt + GetDIBits

- **狀態**：Accepted
- **日期**：2026-10-18

## 背景

規格固定以 GDI `BitBlt` 取得靜態桌面快照，不使用 DXGI 或 Windows.Graphics.Capture。截圖前必須先隱藏自己可能遮住桌面的視窗，快照不得包含自己的選取遮罩或編輯器；HDC、HBITMAP 等非受控資源必須有明確的所有權與例外安全的清理；擷取失敗時要能安全取消並提示。v1.0 的選取範圍限於單一顯示器，而各顯示器的解析度與縮放比例可能不同。

## 決策

1. **逐顯示器擷取**：對列舉到的每個顯示器，以 `GetDC(NULL)` 取得整個虛擬桌面的 DC，`CreateCompatibleDC` + `CreateCompatibleBitmap`（該顯示器的實體寬高），再以 `BitBlt(SRCCOPY | CAPTUREBLT)` 從該顯示器在桌面上的實體像素左上角複製。`CAPTUREBLT` 讓分層視窗（例如半透明的選單、工具提示）一併被擷取。
2. **取出像素**：先把記憶體 DC 原本的 bitmap 換回，再以 `GetDIBits`（32 bpp、由上而下、`BI_RGB`）直接寫入 `WriteableBitmap` 的後端緩衝，凍結後可跨執行緒讀取，不多保留一份暫存陣列。
3. **像素格式為 `Bgr32`**：螢幕畫面沒有透明度，而 `BitBlt` 結果的 alpha 位元組未定義（通常為 0）；以 `Bgra32` 解讀會得到整張透明的圖片，之後的合成與剪貼簿輸出都會出錯。
4. **資源所有權**：三種 `SafeHandle` 對應三種釋放函式——`SafeScreenDcHandle`（`ReleaseDC(NULL, hdc)`）、`SafeMemoryDcHandle`（`DeleteDC`）、`SafeGdiObjectHandle`（`DeleteObject`）。`SelectObject` 換入的 bitmap 在 `finally` 中換回，之後才刪除 DC 與 bitmap。擷取方法返回前釋放全部 GDI 物件；快照只以凍結的 bitmap 存在。
5. **失敗**：任何 GDI 呼叫失敗或沒有任何顯示器時，拋出 `CaptureException`（含 Win32 錯誤碼與繁體中文說明）。截圖流程負責關閉覆蓋層、恢復視窗、回到常駐狀態並提示使用者。
6. **擷取時機**：截圖流程先隱藏自家視窗，再以 `DwmFlush` 等待桌面合成器兩次，確保畫面上已沒有自己的視窗後才擷取；選取遮罩在快照完成後才建立，不可能被截入。
7. **裁切**：選取完成後，從該顯示器的快照複製出選取範圍成為獨立的 bitmap，整張快照即可被回收，不會因為保留一小塊裁切而留住整個 4K 畫面。
8. **游標與持久化**：`BitBlt` 本身不包含滑鼠游標，v1.0 不另外繪製游標。快照只存在記憶體，不寫入檔案或日誌。

## 考慮過的替代方案

- **整個虛擬桌面一次 BitBlt**：一張橫跨所有顯示器的大圖在不同解析度並存時浪費記憶體，且選取本來就限於單一顯示器，逐顯示器快照可直接以顯示器本地座標裁切。
- **`Imaging.CreateBitmapSourceFromHBitmap`**：較簡短，但輸出像素格式與 alpha 處理由 WPF 決定，且需要在轉換期間持有 HBITMAP；`GetDIBits` 讓格式與複製次數都明確。
- **Windows Forms `Graphics.CopyFromScreen`**：超出系統匣與色彩對話框的 Windows Forms 使用邊界，且仍需再轉換一次成 WPF bitmap。
- **DXGI Desktop Duplication、Windows.Graphics.Capture**：規格明確排除。

## 影響

- GDI 物件數量在每次擷取後回到原值，可用 `GetGuiResources` 在自動化測試與重複循環中量測。
- 每次擷取暫時配置「各顯示器實體寬 × 高 × 4 位元組」的記憶體（4K 約 33 MB），流程結束後由垃圾回收釋放。
- DRM 保護內容、UAC 安全桌面與部分特殊 GPU 畫面可能擷取為黑色或失敗，列入已知限制文件。
