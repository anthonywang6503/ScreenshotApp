# Handoff — Phase 0：規劃與環境確認

**日期**：2026-10-18
**狀態**：complete
**分支／commit**：`main`（基準 commit，見 `git log`）

## 1. 完成內容
- 讀取規格《ScreenshotApp — Windows 原生截圖與圖片標註工具》並擷取需求至 `../findings.md`。
- 建立 planning-with-files Gated + Slug Mode 計畫目錄 `.planning/2026-10-18-screenshotapp-v1/`，`.mode` = `autonomous gate inject-smart`，已 attest。
- 完整 `../task_plan.md`：Phase 0～5；每個 Item 具 ID、依賴、Effort、可否委派、驗收方式；Phase 門檻 Acceptance Criteria；FR→模組→測試→證據追蹤矩陣；MT 需使用者驗證時機表；ADR 0001～0017 清單；決策與錯誤表。
- 建立 git 儲存庫（`main`）、`.gitignore`、專案層級 `nuget.config`、`CHANGELOG.md`。
- 尚未建立任何產品程式碼（依人工控制點規範，Phase 1 須經授權）。

## 2. 設計原因（關鍵決策）
- **Phase 劃分沿用規格第 9 節**，方便 FR 對應驗收；Phase 0 另立以記錄規劃。
- **`ImageRenderer` 基礎版提前到 Phase 3**：規格禁止占位按鈕，編輯器的複製／儲存必須為真功能；Phase 4 專注完整化（像素精確、重試、原子寫入、未匯出變更保護）。
- **Phase 2 先以「選取→複製到剪貼簿」收尾**：讓截圖流程在編輯器完成前即可被 Agent 實機驗證（PowerShell 讀剪貼簿尺寸比對）。
- **座標策略**：選取用 `GetCursorPos` 實體像素、覆蓋層用 `SetWindowPos` 定位，避開 WPF DIP 往返誤差與跨 DPI 初始定位問題；集中於 `CoordinateMapper`。
- **顯示與匯出共用 `AnnotationPainter`**：確保畫面、PNG、剪貼簿一致。
- **100% 縮放 = 1 圖片 px = 1 實體 px**。
- **Git**：每 Phase 一個 feature branch，完成時 ff 合併回 `main`（假設，使用者可覆寫）。
- **註解規範**：程式碼註解不得引用 FR／AT／ADR 編號或文件路徑；追蹤關係放在 `docs/` 與測試名稱。

## 3. 驗證結果
| 項目 | 結果 |
|------|------|
| .NET SDK | 10.0.402；WindowsDesktop Runtime 10.0.13 |
| WPF + WinForms 混用建置（scratchpad 煙霧專案） | 成功，需 `<Using Remove="System.Windows.Forms" />` |
| xunit（net10.0-windows + UseWPF） | 1 passed |
| NuGet | 本機僅 VS 離線來源；nuget.org 可連線；專案層級 `nuget.config` 可 restore |

## 4. 風險
- **混合 DPI 偏移**（規格風險 1）：Phase 2 必須先驗證覆蓋層定位；使用者實機配置待查（Key Question 1）。
- **GDI 資源洩漏**：Phase 1 先建立 SafeHandle 基礎，Phase 2／5 量測 30 次循環。
- **IME 與文字匯出差異**：Phase 3 以 WPF TextBox 覆蓋並由使用者做 MT-06。
- **Gated Mode 操作風險**：編輯 `task_plan.md` 後未 attest 會導致 hooks 拒絕注入；收尾儀式已納入。
- **需真人驗證的 MT 項目**（PowerPoint／Teams、拔除顯示器、乾淨機器）若使用者無法執行，將標示「尚未驗證」。

## 5. 後續工作（給下一個 Session）
1. 取得使用者授權後，建立 `feature/phase-01-platform-foundation`。
2. 以 `phase-status.sh 1 in_progress`（或手動編輯）將 Phase 1 設為 `in_progress`，並重新 attest。
3. 依 P1-00 → P1-17 順序執行；可委派 P1-05／P1-09／P1-10／P1-12／P1-14 給 Subagent（同時最多 2 個）。
4. Phase 1 結束時執行收尾儀式並提出 MT-01、MT-02 請使用者驗證。

## 6. 未驗證項目
- 所有 FR／MT 皆尚未實作與驗證（Phase 0 僅規劃）。
- `dotnet publish --self-contained` 尚未在本機實測（排程於 Phase 5；若 Phase 1 時間允許可先煙霧驗證）。
