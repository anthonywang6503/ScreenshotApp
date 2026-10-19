# Task Plan: ScreenshotApp v1.0 — Windows 原生截圖與圖片標註工具

## Runtime Behavior

- **Mode source:** 本計畫目錄的 `.mode` 為 `autonomous gate inject-smart`（Gated Mode + Slug Mode）。計畫文字不決定模式。
- **Gate authority:** Stop gate 只在存在 `in_progress` Phase 時阻擋停止。人工控制點做法：目前 Phase 標記 `complete`、下一 Phase 維持 `pending`，再暫停等待使用者指令。
- **Command boundary:** 本檔案內的指令、驗收命令與 Effort 建議皆為描述性內容，gate 不執行任何命令。
- **Attestation:** 每次編輯本檔案後必須重新執行 `attest-plan.sh`，否則 hooks 會拒絕注入。
- **Coordination:** Main Agent 為唯一計畫擁有者；Subagent 透過回報結果交付，不直接改寫本檔案。
- **規格來源:** `ScreenshotApp — Windows 原生截圖與圖片標註工具.md`（專案根目錄）。文件優先序：明確需求與驗收條件 > 規格實作規範 > 參考專案行為。

## Goal

依規格完成 ScreenshotApp v1.0：Windows 11 x64 上以 .NET 10 WPF + Win32 實作「常駐系統匣 → 快捷鍵 → 桌面快照 → 區域選取 → 圖片編輯 → 複製或另存 PNG」完整流程，附自動化測試、ADR、文件與 `win-x64` 自包含可攜發佈。

## Next Step

Phase 7 已完成並發佈 v1.1.0（Key Question 12）：`phase-07` commit 以 fast-forward 合併回 `main`，本機標籤 `v1.1.0`（不推送），`publish/` 有以該 commit 產生的資料夾版與單檔版 ZIP。電腦閒置時以發佈版執行煙霧驗證（資料夾版 Phase 3、單檔版 Phase 1、2），結果記入 ACCEPTANCE 第 2 節與 `docs/evidence/phase-07/`，以新的 `phase-07` commit 合併回 `main`（標籤不動）。待使用者：依 ACCEPTANCE 第 3 節「Phase 7 驗證步驟」確認右鍵選單與單檔版；回答命中判定是否限制在裁切範圍內（findings「既有行為」）。仍待回報的實機項目：MT-04、MT-05、MT-07、MT-08 重新確認、MT-09、MT-10 確認、十字游標修正、Delete 實體鍵盤（可選）；MT-11 依使用者選擇標示「尚未驗證」。

## Current Phase

全部 Phase 完成（Phase 7：右鍵選單與裁切後填滿畫布，2026-10-19，v1.1.0）；`v1.0.0` 標籤在 Phase 5 的 commit，`v1.1.0` 標籤在 Phase 7 的發佈 commit。

## 執行規範（本任務專用）

### Effort 等級定義
| 等級 | 適用情境 |
|------|----------|
| `max` | 跨模組、錯誤代價極高、需反覆推演的核心（本任務無單一 Item 達此級，整體架構決策由 Main Agent 以最高注意力處理） |
| `xhigh` | DPI／座標、GDI 資源、畫布互動、文字輸入等高風險核心 |
| `high` | 需設計但邊界清楚的服務、整合流程、測試套件 |
| `medium` | 標準實作、UI 版面、文件 |
| `low` | 樣板、設定、收尾作業 |

### Subagent 政策
- 同時最多 2 個 Subagent；每個 Subagent 全新、一次性、邊界清楚、可獨立驗證（自行跑 `dotnet test`）。
- 可委派：純模型與數學（PixelRect、CoordinateMapper）、設定持久化、錯誤日誌、StartupService、UndoRedo 命令與測試、ImageRenderer 測試、圖片匯入、使用者文件。
- Main Agent 專責：架構與跨模組決策、Win32 互操作邊界、DPI 策略、覆蓋層與編輯器互動、AppController 狀態機、最終整合與驗收。
- Subagent 簡報必含：目標、檔案範圍白名單、既有慣例、驗收命令、禁止事項（不得改規劃檔、不得寫引用文件編號的註解）。

### 程式碼與文件規範
- 所有註解不得引用文件路徑、FR／AT／ADR 編號或任何外部識別資訊；設計意圖直接以文字說明。追蹤關係只存在於 `docs/` 與測試類別／方法名稱。
- 文字資源集中於 `ScreenshotApp/Resources/Strings.resx`（zh-TW）；程式碼與 XAML 不得硬編碼 UI 文字。
- 文件內引用專案內檔案一律使用相對路徑。
- 不得以占位按鈕、假成功訊息或關鍵 TODO 充數；無法在 GUI 驗證的項目標示「尚未驗證」。

### 收尾儀式（每個 Phase 的最後一個 Checklist 項目）
1. 該 Phase 所有 Item 完成，`dotnet build -c Release` 與 `dotnet test` 通過（0 error）。
2. 建立 `handoffs/phase-<NN>-<slug>.md`（完成內容、設計原因、驗證結果與證據、風險、後續工作、未驗證項目）。
3. 更新 `task_plan.md`（含 handoff 註記）、`progress.md`、`findings.md`、`CHANGELOG.md`。
4. 將 Phase 標記 `complete`（下一 Phase 維持 `pending`）並重新 attest，使 commit 內容、磁碟與 attestation 一致。
5. `git commit`，將 feature branch fast-forward 合併回 `main`，暫停等待使用者指令。

### Git 策略（假設，使用者可覆寫）
- `main` 永遠反映最後一個通過控制點的狀態。
- 每個 Phase 使用 `feature/phase-<NN>-<slug>` 分支，Phase 完成時 fast-forward 合併回 `main`；不建立 PR。
- commit 訊息格式：`phase-<NN>: <摘要>`；規劃檔與程式碼一起提交。

### 驗證原則
- 自動化測試（AT-01～AT-10）由 Agent 實際執行並記錄結果。
- Agent 端實機煙霧驗證：啟動 exe、以 Win32 模擬輸入、以螢幕截圖檢視畫面、以 PowerShell 讀取剪貼簿或檔案比對像素尺寸；量測結果需附條件。
- 需真人操作的 MT 項目（PowerPoint／Word／Teams、微軟注音、拔除顯示器、乾淨機器）在對應控制點請使用者執行並回報，結果登錄於 `docs/ACCEPTANCE.md`；未回報前標示「尚未驗證」。

## 目標專案結構

```
screenshoot.v1/
├── ScreenshotApp.sln
├── nuget.config / .gitignore / README.md / CHANGELOG.md
├── ScreenshotApp/                      # 唯一 WPF 應用程式專案（net10.0-windows）
│   ├── App.xaml(.cs)、AppController.cs、app.manifest
│   ├── Platform/    NativeMethods、SafeHandles、HotkeyService、TrayService、MonitorService、CaptureService、StartupService、SingleInstanceGuard、ErrorLog
│   ├── Capture/     DesktopSnapshot、CoordinateMapper、SelectionController、SelectionOverlayWindow(.xaml)
│   ├── Editor/      EditorWindow(.xaml)、EditorViewModel、Model/（ImageDocument、PixelRect、Annotation*）、Tools/、Rendering/AnnotationPainter、Commands/
│   ├── Export/      ImageRenderer、ClipboardService、PngFileService、ImageFileLoader
│   ├── Settings/    AppSettings、SettingsStore、SettingsWindow(.xaml)、HotkeyGesture
│   └── Resources/   Strings.resx、app.ico、Styles.xaml
├── ScreenshotApp.Tests/                # xunit（net10.0-windows + UseWPF）
├── docs/            ARCHITECTURE.md、USER_GUIDE.md、ACCEPTANCE.md、KNOWN_LIMITATIONS.md、adr/NNNN-<slug>.md
└── publish/         （.gitignore 排除）win-x64 自包含輸出與 ZIP
```

## Phases

### Phase 0: 規劃與環境確認

- [x] 讀取規格並擷取需求、驗收條件與固定決策至 `findings.md`
- [x] 驗證開發環境：.NET 10 SDK 10.0.402、WindowsDesktop 10.0.13、WPF + WinForms 混用可建置、xunit 可執行
- [x] 發現並解決 NuGet 來源問題（專案層級 `nuget.config`）
- [x] 以 Gated + Slug Mode 初始化計畫目錄、啟用 `inject-smart`
- [x] `git init -b main`、`.gitignore`、`CHANGELOG.md`
- [x] 完成 Phase 1～5 Checklist、依賴、Effort、Acceptance Criteria、追蹤矩陣、ADR 清單
- [x] Handoff：`handoffs/phase-00-planning.md`
- [x] 基準 commit 於 `main`
- **Status:** complete

### Phase 1: Windows 平台基礎（FR-01～FR-06、FR-33、FR-34、NFR-09）

**目標**：可執行的常駐程式骨架。系統匣、單一執行個體、全域快捷鍵、設定持久化與設定視窗、開機啟動、正常結束、錯誤日誌。
**依賴**：Phase 0。
**分支**：`feature/phase-01-platform-foundation`。

Checklist（格式：ID｜項目｜依賴｜Effort｜委派｜驗收）
- [x] P1-00 ADR-0001～0006 撰寫於 `docs/adr/`（技術堆疊與專案配置、座標系統、單一執行個體、設定持久化、文字資源、Tray/WinForms 邊界）｜無｜medium｜否｜六份 ADR 存在且狀態 Accepted
- [x] P1-01 Solution 與專案骨架：`ScreenshotApp.sln`（`dotnet new sln --format sln`；.NET 10 預設為 `.slnx`）、`ScreenshotApp.csproj`（`net10.0-windows`、UseWPF、UseWindowsForms、`<Using Remove="System.Windows.Forms"/>`、Nullable、`ApplicationIcon`、`app.manifest`）、`ScreenshotApp.Tests.csproj`（xunit、`UseWPF`）、`README.md` 初版｜P1-00｜medium｜否｜`dotnet build -c Release` 0 error；`dotnet test` 通過 1 個 smoke 測試
- [x] P1-02 Per-Monitor V2 DPI 宣告：`app.manifest` `dpiAwareness=PerMonitorV2`；啟動時以 `GetThreadDpiAwarenessContext` 驗證，僅在「不是」PerMonitorV2 時寫入錯誤日誌（日誌只記錄錯誤）｜P1-01｜medium｜否｜Agent 以外部 `GetDpiAwarenessContextForProcess` + `AreDpiAwarenessContextsEqual(ctx, -4)` 驗證執行中處理程序為 PerMonitorV2
- [x] P1-03 Win32 Interop：`Platform/NativeMethods.cs`（`LibraryImport`；僅宣告 Phase 1 實際使用的 user32 API：熱鍵、DPI 感知查詢、前景授權）。GDI SafeHandle 移至 P2-04 與其唯一使用者一起實作｜P1-01｜medium｜否｜編譯通過；由使用端測試（熱鍵、DPI）覆蓋
- [x] P1-04 應用程式生命週期：`App.xaml` `ShutdownMode=OnExplicitShutdown`、`AppController`（常駐服務的建立、接線與釋放）、`DispatcherUnhandledException`/`UnobservedTaskException`/`AppDomain.UnhandledException` 統一處理（繁中訊息 + 日誌，UI 執行緒例外不結束程式）。狀態機移至 P2-08（Phase 1 只有常駐狀態）｜P1-01｜high｜否｜`GlobalExceptionHandlerTests`：Dispatcher 例外被記錄並標記已處理，後續工作持續執行
- [x] P1-05 文字資源：`Resources/Strings.resx`（zh-TW 為預設文化）、`Strings` 靜態存取類別、XAML 標記延伸 `{res:Str Key}`｜P1-01｜medium｜可｜缺鍵時回傳 `!Key!` 且測試覆蓋；XAML/程式碼無硬編碼 UI 文字
- [x] P1-06 系統匣：`Platform/TrayService`（WinForms `NotifyIcon` + `ContextMenuStrip`：Phase 1 提供「設定」「結束」；「開始截圖」於 P2-08、「開啟圖片」於 P4-04 隨功能加入，不放占位項目；雙擊=設定；Balloon 提示；工具提示文字顯示快捷鍵狀態）｜P1-04、P1-05、P1-14｜medium｜否｜實機：啟動後無主視窗、匣圖示存在、選單項目可用、選單在目前 DPI 下比例正確
- [x] P1-07 全域快捷鍵：`Platform/HotkeyService`（message-only `HwndSource`、`RegisterHotKey` + `MOD_NOREPEAT`、`WM_HOTKEY` 事件、失敗原因（錯誤碼對應繁中訊息）、`Rebind(new)` 先註冊新再解除舊、失敗保留舊設定）、`Settings/HotkeyGesture`（解析／格式化／驗證；須含 Ctrl／Alt／Win；拒絕 PrintScreen 組合與 Win+Shift+S 等 Windows 保留截圖快捷鍵）。Phase 1 按下快捷鍵尚無動作，截圖於 P2-08 接上｜P1-03、P1-04｜high｜否｜`HotkeyGestureTests`、`HotkeyServiceTests`（以 F13～F24 組合實際註冊、衝突、Rebind）；實機：外部程序註冊同一組合於執行中失敗（錯誤 1409）、結束後成功；被占用時 Balloon + 工具提示 + 設定視窗顯示失敗原因且程式可用
- [x] P1-08 單一執行個體：`Platform/SingleInstanceGuard`（`Mutex` + `EventWaitHandle`；第二個執行個體先以 `AllowSetForegroundWindow` 授權前景、發訊後退出；第一個收到後顯示設定視窗或編輯視窗）｜P1-04｜medium｜否｜實機：啟動兩次僅一個處理程序、一個匣圖示；第二次啟動使設定視窗顯示於前景
- [x] P1-09 開機啟動：`Platform/StartupService`（`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`，值為帶引號 exe 路徑；可注入根鍵供測試）｜P1-01｜low｜可｜單元測試以測試用子鍵驗證寫入／移除／查詢；預設關閉
- [x] P1-10 設定模型與持久化：`Settings/AppSettings`（Hotkey、StartWithWindows、LastSaveDirectory、DefaultColor、DefaultStrokeWidth、DefaultFontSize）、`SettingsStore`（`%LocalAppData%\ScreenshotApp\settings.json`、System.Text.Json、temp→move 原子寫入、損毀／無效欄位→預設值並回傳警告、權限不足不拋出致命例外）｜P1-01｜medium｜可｜AT-08：正常載入、快捷鍵變更、JSON 損壞復原、無效欄位、唯讀目錄、無效路徑
- [x] P1-11 設定視窗：`Settings/SettingsWindow.xaml`（快捷鍵擷取欄位、開機啟動、預設顏色、線寬、字級、上次儲存位置唯讀、套用時立即 `Rebind` 並顯示結果、鍵盤導覽與 ToolTip）｜P1-05、P1-07、P1-09、P1-10｜high｜否｜實機：修改快捷鍵成功即生效；衝突時保留舊鍵並提示；關閉視窗不結束程式
- [x] P1-12 錯誤日誌：`Platform/ErrorLog`（`%LocalAppData%\ScreenshotApp\logs\error.log`，僅訊息與堆疊，單檔 256 KB 輪替保留 2 份，超過 30 天未更新的日誌檔於啟動時刪除，絕不寫入圖像或標註文字，寫入失敗不拋出）｜P1-01｜low｜可｜單元測試：輪替、保留期間、內容限制、目錄無效時不拋出
- [x] P1-13 正常結束：Tray「結束」→ 解除快捷鍵、Dispose NotifyIcon、關閉所有視窗、`Shutdown()`；關閉設定視窗只回常駐｜P1-06、P1-07｜low｜否｜實機：結束後處理程序消失、匣圖示移除、重新啟動後快捷鍵可再註冊
- [x] P1-14 應用程式圖示：以腳本產生 `Resources/app.ico`（16/32/48/256），exe 與匣共用｜無｜low｜可｜exe 顯示圖示；匣圖示非預設
- [x] P1-15 Agent 端煙霧驗證：啟動 Release 建置 → 處理程序存在、無可見主視窗、`settings.json` 產生、日誌無錯誤、DPI 感知外部查詢為 PerMonitorV2、外部 `RegisterHotKey` 衝突檢查（執行中 1409）、第二次啟動自動退出且設定視窗出現、預先占用 `Ctrl+Shift+2` 後啟動仍常駐並提示、以 UI Automation 觸發「結束」後處理程序消失且快捷鍵釋放（不可行時標示「尚未驗證」並交由 MT-01）｜P1-06～P1-13｜medium｜否｜以上各項有輸出證據記錄於 `progress.md`
- [x] P1-16 自動化測試整理：`HotkeyGestureTests`、`HotkeyServiceTests`、`SettingsStoreTests`（AT-08）、`StartupServiceTests`、`ErrorLogTests`、`GlobalExceptionHandlerTests`、`StringsResourceTests`（資源鍵存在、XAML 無硬編碼 UI 文字）；測試不得觸碰真實 `%LocalAppData%\ScreenshotApp` 與 `HKCU\...\Run`｜各實作｜medium｜否｜`dotnet test` 全數通過
- [x] P1-17 Phase 收尾儀式（handoff `handoffs/phase-01-platform-foundation.md`、planning files、CHANGELOG、commit、合併、暫停）｜全部｜low｜否｜handoff 存在並於本檔註記

**Acceptance Criteria（Phase 門檻）**
1. FR-01（選單「開始截圖」「開啟圖片」分別於 Phase 2、Phase 4 隨功能補齊）、FR-02～FR-06、FR-33、FR-34 在實機可操作並有證據；AT-08 通過。
2. 建置 0 error、測試全綠；無占位功能。
3. 使用者需驗證：MT-01（首次啟動只進匣、重複啟動、退出重啟）、MT-02 的「被占用提示」部分；MT-02「在桌面／瀏覽器／PowerPoint 中觸發截圖」於 Phase 2 控制點驗證。

**Handoff**：`handoffs/phase-01-platform-foundation.md`（2026-10-18；114 個自動化測試通過、煙霧驗證 31 項全數通過）。
- **Status:** complete

### Phase 2: 截圖與區域選取（FR-07～FR-13）

**目標**：快捷鍵後隱藏自家視窗、逐顯示器 BitBlt 快照、每顯示器覆蓋層、單一顯示器內矩形選取、取消與錯誤處理；選取完成先複製至剪貼簿（Phase 3 改為開啟編輯器）。
**依賴**：Phase 1（P1-03、P1-04、P1-07）。
**分支**：`feature/phase-02-capture-selection`。

Checklist
- [x] P2-00 ADR-0007～0009（截圖策略、覆蓋層定位與輸入座標策略、狀態機與重入保護）｜無｜medium｜否｜三份 ADR Accepted
- [x] P2-01 `Editor/Model/PixelRect` 與 `PixelPoint`（int、左上含右下不含、寬高不得為負、`FromCorners`（正規化）、`Intersect`、`Clamp(點)`、`Contains`（點／矩形）、`IsEmpty`、`Offset`）｜P1-01｜high｜可｜AT-02 單元測試
- [x] P2-02 `Platform/MonitorService`（`EnumDisplayMonitors` → `MonitorInfo{Index, Bounds}`；覆蓋層的 DPI 由 WPF 依 HWND 取得，`WorkArea`／`DpiScale`／`IsPrimary`／`DeviceName` 沒有使用者，不加入；負座標與非主螢幕）｜P1-03｜high｜否｜`MonitorServiceTests` 與系統計量（顯示器數、主螢幕、虛擬桌面）一致
- [x] P2-03 `Capture/CoordinateMapper`（Phase 2 範圍：DesktopPhysical ↔ MonitorLocal ↔ 覆蓋層 ViewDip；純函式、唯一換算點。編輯器的 Image ↔ ViewDip（縮放、平移）隨 P3-05 加入）｜P2-01｜xhigh｜可（含測試）｜AT-01（Phase 2 部分）：正負起點、混合 DPI、非主螢幕、往返誤差 ≤ 1px
- [x] P2-04 `Platform/CaptureService`（每顯示器 `GetDC(NULL)` + `CreateCompatibleDC/Bitmap` + `BitBlt(SRCCOPY|CAPTUREBLT)` + `GetDIBits` 直接寫入 `WriteableBitmap` → 凍結 `Bgr32`（BitBlt 的 alpha 未定義，不用 Bgra32）；失敗丟 `CaptureException` 含 Win32 碼；`SafeScreenDcHandle`（ReleaseDC）／`SafeMemoryDcHandle`（DeleteDC）／`SafeGdiObjectHandle`（DeleteObject），刪除 DC 前換回原 bitmap）｜P1-03、P2-02｜xhigh｜否｜`SafeHandleTests`（以 `GetGuiResources` 比對 GDI 物件數）；Agent 實機：30 次循環 GDI 物件數不成長
- [x] P2-05 `Capture/DesktopSnapshot`（每顯示器的 `MonitorInfo` + bitmap；裁切為獨立 bitmap）｜P2-02、P2-04｜low｜否｜編譯通過
- [x] P2-06 `Capture/SelectionOverlay(.xaml)`（每顯示器一個 `HwndSource`，直接以該顯示器實體像素邊界建立（`WS_POPUP`、`WS_EX_TOPMOST|WS_EX_TOOLWINDOW`），出生即為該顯示器 DPI，不經跨 DPI 移動；背景為該顯示器快照 `Stretch=Fill`；暗化遮罩、選取框、實體 px 尺寸標籤、十字游標；Esc／右鍵取消；`WM_DISPLAYCHANGE`／`WM_DPICHANGED`→取消）｜P2-03、P2-05｜xhigh｜否｜Agent 實機：覆蓋層精確貼合各顯示器（截圖檢視）；混合 DPI 依賴 MT-05
- [x] P2-07 `Capture/SelectionController` 與 `Capture/DragSelection`（協調全部覆蓋層；滑鼠座標取自事件本身並換回實體像素；拖曳期間 `Mouse.Capture` 於起始覆蓋層並夾取於起始顯示器；選取包含起點與終點像素；在起點像素放開＝零尺寸，不產生文件、維持選取狀態；完成→`SelectionResult{MonitorIndex, MonitorLocalRect}`）｜P2-06｜xhigh｜否｜AT-02 行為測試（`DragSelection`）；實機拖曳各方向正確
- [x] P2-08 `AppStateMachine`（自 P1-04 移入：Resident/Capturing/Selecting、轉換守衛；Editing 隨 P3-13 編輯器加入）、`Capture/CaptureFlow`（隱藏自家視窗（先關閉淡出動畫）→`DwmFlush`×2→快照→選取→裁切；任何結束路徑都恢復視窗並回到 Resident；以 `ICaptureEnvironment` 注入平台步驟供測試）與 `AppController` 接線（快捷鍵與系統匣新增的「開始截圖」共用入口）：完成→暫以 `Export/ClipboardService`（基礎版，24 位元 DIB）複製並通知 → Resident；Capturing/Selecting 期間忽略快捷鍵、系統匣與第二個執行個體的喚醒｜P2-04～P2-07｜high｜否｜實機：快捷鍵→選取→剪貼簿影像與已知圖樣逐像素一致；連按快捷鍵不重入
- [x] P2-09 擷取失敗處理：例外→關閉全部覆蓋層→繁中提示→Resident；顯示設定變更→安全取消並提示｜P2-08｜medium｜否｜以注入失敗的環境測試狀態回復；實機以送出 `WM_DISPLAYCHANGE` 模擬
- [x] P2-10 自動化測試：`PixelRectTests`（AT-02）、`CoordinateMapperTests`（AT-01）、`SelectionLogicTests`（AT-02 夾取／零尺寸／半開區間）、`CaptureFlowTests`（狀態回復）、`AppStateMachineTests`、`SafeHandleTests`、`MonitorServiceTests`、`CaptureServiceTests`、`ClipboardServiceTests`（DIB 格式）｜P2-01～P2-09｜high｜可（P2-01/03 同一 Subagent）｜314 個全數通過
- [x] P2-11 Agent 端實機驗證：查詢使用者顯示器配置；記事本置於前景後啟動 app → 送快捷鍵 → 截圖檢視覆蓋層與邊界 → 模擬拖曳 → 剪貼簿與已知圖樣視窗逐像素比對（含設定視窗遮在圖樣上時不被截入、系統匣「開始截圖」路徑）；Esc／右鍵取消不留遮罩；零尺寸；連按快捷鍵；30 次循環 GDI／USER 物件、handle、記憶體量測｜P2-08｜high｜否｜證據與量測條件記錄於 `progress.md` 與 `docs/evidence/phase-02/`
- [x] P2-12 已知限制文件初稿：游標不擷取、DRM／UAC 安全桌面、跨螢幕選取限制｜無｜low｜可｜`docs/KNOWN_LIMITATIONS.md` 初版
- [x] P2-13 使用者實機驗證請求：MT-02（觸發）、MT-03（取消部分）、MT-04（100/125/150/200%）、MT-05（雙螢幕混合 DPI、負 X、上下排列）｜P2-11｜low｜否｜步驟寫入 `docs/ACCEPTANCE.md`，於控制點提出
- [x] P2-14 Phase 收尾儀式（handoff `handoffs/phase-02-capture-selection.md`）｜全部｜low｜否｜handoff 存在並註記
- [x] P2-15 MT 回饋修正（Phase 2 控制點之後，2026-10-18）：擷取全白畫面時選取覆蓋層的十字游標看不清 → `Capture/CrosshairCursor`（`CreateCursor` 單色黑芯白邊、不使用反相；尺寸取 `GetSystemMetricsForDpi(SM_CXCURSOR)`；每種尺寸建立一次沿用到結束）、覆蓋層改在程式中依 DPI 設定游標；`Invoke-Phase3Smoke.ps1` 加入顯示中游標檢查｜P2-06｜high｜否｜`CrosshairCursorTests`（51）與完整 825 個測試通過；`DrawIconEx` 對照圖；使用者以新版重新確認

**Acceptance Criteria（Phase 門檻）**
1. FR-07～FR-13 可操作；AT-01、AT-02 通過；覆蓋層無 DPI 偏移（Agent 截圖證據 + 使用者 MT-04/05 回報）。
2. GDI 資源 30 次循環無成長趨勢（附量測數據）。

**Handoff**：`handoffs/phase-02-capture-selection.md`（2026-10-18；314 個自動化測試通過、煙霧驗證 38 項全數通過、30 次循環 GDI 物件恆為 38）。MT 回饋修正 P2-15 記錄於 `progress.md` 與 `docs/ACCEPTANCE.md`。
- **Status:** complete

### Phase 3: 圖片編輯核心（FR-14～FR-25）

**目標**：圖片文件模型、非破壞性標註八工具、裁切、縮放平移、文字輸入（IME）、步驟編號、樣式、Undo/Redo、編輯器快捷鍵；`ImageRenderer` 基礎版讓複製／儲存按鈕為真實功能。
**依賴**：Phase 2（P2-01、P2-03、P2-08）。
**分支**：`feature/phase-03-editor-core`。

Checklist
- [x] P3-00 ADR-0010～0013（文件模型與命令模式、共用 AnnotationPainter、畫布縮放模型、文字輸入策略）｜無｜medium｜否｜四份 ADR Accepted
- [x] P3-01 文件模型：`ImageDocument`（凍結 `Source`、`PixelWidth/Height`、`CropRect`、`Annotations`（索引即 Z-order）、`History`、`NextStepNumber`＝現有最大編號＋1）；不可變 `Annotation` record（Id、Color）：`BoxAnnotation`（矩形範圍＋線寬）→ `RectangleAnnotation`／`EllipseAnnotation`，`SegmentAnnotation`（起訖像素＋線寬）→ `LineAnnotation`／`ArrowAnnotation`，`TextAnnotation`（Origin、Text、FontFamily、FontSize）、`StepNumberAnnotation`（Center、Radius、Number）；幾何一律為整數圖片像素：`Bounds`、`HitTest(imagePt, tolerance)`（矩形與橢圓只命中外框）、`Translate`、控制點列舉與以拖曳位移套用控制點。`ExportedVersion`／`IsDirty` 隨唯一使用者移至 P4-05｜P2-01｜xhigh｜否｜AT-04 模型測試
- [x] P3-02 命令模式：`Editor/Commands/IEditCommand{Execute, Undo}`、`UndoRedoStack`（上限 100、新命令清除 Redo）；五個命令皆以 Id 找標註、Execute／Undo 皆為「設為某值」（重複執行無害）：`AddAnnotationCommand`、`RemoveAnnotationCommand`、`ReplaceAnnotationCommand`（涵蓋移動、調整大小、樣式、文字、編號）、`SetCropCommand`、`ReorderAnnotationCommand`｜P3-01｜high｜可（含測試）｜AT-05：全部可逆、一次拖曳一筆、新修改清除 Redo、上限 100
- [x] P3-03 `Editor/Rendering/AnnotationPainter`（`DrawingContext` 於圖片像素座標繪製所有標註：矩形／橢圓／直線／箭頭頭部幾何／文字 `FormattedText`（像素字級、換行）／步驟編號圓形+置中數字）；顯示與匯出共用｜P3-01｜xhigh｜否｜渲染快照測試：同一文件在兩種縮放下輸出一致（AT-07 前置）
- [x] P3-04 `Editor/EditorWindow.xaml` 版面：工具列（八工具 ToggleButton，圖示+文字、ToolTip、AccessKey）、屬性列（8 常用色 + 自訂、線寬、字級、字型、步驟編號值）、Undo／Redo／複製／儲存按鈕（「開啟」隨功能於 P4-04 加入）、畫布（`ScrollViewer` + 縮放容器（`Image` + 標註層 + 文字輸入層）+ 縮放容器外的控制點層）、狀態列（圖片尺寸、裁切尺寸、游標圖片像素、縮放 %）；`EditorSession`（編輯狀態與工具邏輯，不依賴 WPF 元素，可單元測試）｜P3-01、P3-03｜xhigh｜否｜實機：開啟後以符合視窗縮放顯示；所有控制項可鍵盤到達
- [x] P3-05 縮放與平移：符合視窗、100%（1 圖片 px = 1 實體 px，`scale = zoom / dpiScale`）、25%～400% 階段、Ctrl+滾輪以游標為中心、空白鍵／中鍵拖曳平移、視窗調整不改變模型；`CoordinateMapper` 加入 Image ↔ ViewDip（比例＋畫布原點）換算、螢幕長度 → 圖片長度（命中容差以螢幕像素定義）、浮點 → 整數像素取整、以游標為中心縮放的捲動位移；輸入、控制點層與縮放捲動都經由這些函式｜P3-04｜high｜否｜AT-01（縮放與平移部分）往返誤差 ≤ 1px；實機：各縮放下控制點尺寸固定、模型座標不變；100% 畫布與已知圖樣逐像素一致（P3-15）
- [x] P3-06 工具邏輯集中於 `EditorSession`（以圖片像素座標接收按下／移動／放開，內部以拖曳類別區分）：選取（由上而下命中、拖曳移動、控制點調整、Delete）、矩形／橢圓／直線／箭頭（拖曳建立，零尺寸不建立）、步驟編號（按下放置、放開前可拖曳、自動遞增、刪除不重排）、裁切、文字；任何工具下都可拖曳選取標註的控制點；拖曳中只更新預覽、放開時提交一筆命令；控制點位移以指標移動量四捨五入計算｜P3-02、P3-04｜xhigh｜否｜AT-04 + 實機操作截圖
- [x] P3-07 文字輸入：畫布上 WPF `TextBox` 覆蓋（`AcceptsReturn`、IME 可用、字級=像素字級隨縮放容器縮放）、Ctrl+Enter／點擊外部=提交、Esc=取消、空字串=取消或刪除、雙擊重編、建立與修改各為一筆命令；編輯中 Ctrl+C／Ctrl+Z 交給 TextBox｜P3-06｜xhigh｜否｜AT-06 模型測試 + 實機輸入中英文與換行
- [x] P3-08 樣式與屬性列：預設紅色、3px、20px；變更選取物件樣式→`ReplaceAnnotationCommand`（不適用的屬性停用）；未選取時變更預設值並回寫設定（字型只在本次執行沿用）；文字輸入中套用到輸入中的文字；自訂顏色使用 WinForms `ColorDialog`｜P3-04、P3-06｜medium｜否｜實機：樣式變更可 Undo；尺寸以圖片像素為準
- [x] P3-09 裁切：控制點以外的位置一律框選新範圍（進入裁切工具時範圍為整張圖片，不提供平移）、拖曳控制點調整（拖到圖片外停在邊緣）、Enter 或範圍內雙擊套用、Esc 取消、重複調整、最小 1×1、夾取於原圖、裁切外暗化顯示（裁切工具中顯示整張原圖）、Undo/Redo｜P3-02、P3-06｜high｜否｜AT-03 + 實機
- [x] P3-10 Z-order：上移／下移／最上／最下（右鍵選單 + Ctrl+]／Ctrl+[）｜P3-02、P3-06｜low｜否｜AT-04 Z-order 測試
- [x] P3-11 編輯器快捷鍵：Ctrl+Z／Y／C／S、Delete、Esc（取消選取或工具操作）；文字編輯中不攔截｜P3-06、P3-07｜medium｜否｜實機驗證各鍵
- [x] P3-12 `Export/ImageRenderer` 基礎版（`RenderTargetBitmap(crop.W, crop.H, 96, 96, Pbgra32)`、`Translate(-crop.X,-crop.Y)`、`DrawImage` + `AnnotationPainter`、Freeze）+ 複製按鈕接 `ClipboardService` + 儲存按鈕接 `PngFileService` 基礎版（SaveFileDialog、預設檔名）｜P3-03｜high｜否｜輸出尺寸 = CropRect；複製／儲存為真實功能
- [x] P3-13 `AppController` 整合：`AppStateMachine` 加入 Editing（可由常駐或編輯開始截圖並記住先前狀態，截圖結束回到先前狀態；`BeginEditing`／`EndEditing`）；選取完成→`ImageDocument`→開啟／重用唯一 `EditorWindow`；取消選取時恢復原編輯視窗；關閉編輯器回常駐不退出；編輯中第二個執行個體喚醒編輯視窗、系統匣「設定」仍可開啟｜P3-04、P2-08｜medium｜否｜`AppStateMachineTests` 涵蓋 Editing；實機流程驗證
- [x] P3-14 自動化測試（兩個 subagent 各自在 worktree 撰寫、主 agent 合併）：`AnnotationGeometryTests`、`EditorSessionTests`、`EditCommandTests`（含 ImageDocument）、`UndoRedoStackTests`、`TextAnnotationTests`、`ImageRendererTests`、`CropCommandTests`、`EditorZoomTests`、`PngFileServiceTests`，以及 `CoordinateMapperTests`、`AppStateMachineTests` 的新增案例；GDI 物件量測輔助改為等數量穩定｜P3-01～P3-12｜high｜可｜774 passed（連續 5 次）
- [x] P3-15 Agent 端實機驗證：啟動→截圖→編輯器開啟；100% 時畫布與已知圖樣逐像素一致；以模擬輸入建立矩形／箭頭／文字／編號→截圖檢視；Undo/Redo；縮放 25/100/400% 後複製輸出尺寸與像素一致｜P3-13｜high｜否｜證據記錄。**部分完成**：前 12 項通過（開啟、100% 逐像素、矩形純紅且在內側、箭頭、編號、Ctrl+Z／Y）；文字、刪除移動、縮放、介面裁切、文件取代、資源循環等因使用者正在使用電腦而未執行，於 `docs/ACCEPTANCE.md` 與 handoff 標示「尚未驗證」，使用者要求先收尾
- [x] P3-16 使用者實機驗證請求：MT-03（選取後開啟編輯器、取消不影響先前文件）、MT-06（微軟注音）、MT-07（多縮放一致性）｜P3-15｜low｜否｜步驟寫入 `docs/ACCEPTANCE.md`，於控制點提出
- [x] P3-17 Phase 收尾儀式（handoff `handoffs/phase-03-editor-core.md`）｜全部｜low｜否｜handoff 存在並註記

**Acceptance Criteria（Phase 門檻）**
1. FR-14～FR-25 可操作；AT-03～AT-06 通過；原始 Bitmap 在編輯中不被修改（測試以像素雜湊驗證）。
2. 縮放／平移／視窗調整不改變輸出（AT-07 前置測試）。

**Handoff**：`handoffs/phase-03-editor-core.md`（2026-10-18；774 個自動化測試通過、煙霧驗證前 12 項通過，後半段尚未驗證）。
- **Status:** complete

### Phase 4: 圖片輸入輸出（FR-24、FR-26～FR-32）

**目標**：唯一 `ImageRenderer` 完整化、剪貼簿（含重試與多格式）、PNG 另存（原子寫入、背景編碼）、開啟既有圖片、未匯出變更保護、文件切換行為。
**依賴**：Phase 3。
**分支**：`feature/phase-04-image-io`。

Checklist
- [x] P4-00 ADR-0014～0016（剪貼簿格式策略、檔案寫入與背景編碼、未匯出變更保護流程）｜無｜medium｜否｜三份 ADR Accepted
- [x] P4-01 `ImageRenderer` 完整化：像素精確（寬高嚴格 = CropRect）、裁切外標註不出現、顯示縮放不影響、執行緒親和（UI 執行緒渲染後 Freeze）｜P3-12｜high｜可（含 AT-07 測試）｜AT-07：不同畫布縮放輸出尺寸與像素雜湊一致；裁切外圖形不出現
- [x] P4-02 `ClipboardService` 強化：`DataObject` 同時放 24 位元 DIB 與 `PNG` 串流（PNG 與存檔共用編碼函式）；DIB／PNG 位元組在背景執行緒由凍結的合成結果產生；重試沿用 .NET 10 內建（`OleSetClipboard`／`OleFlushClipboard` 各 10 次 × 100 ms），不加外層重試；`CLIPBRD_E_CANT_OPEN` 顯示專屬訊息；寫入剪貼簿的步驟可注入；失敗回傳錯誤並保留文件；成功後狀態列輕量提示並以合成當時的版本更新 `ExportedVersion`｜P4-01｜high｜否｜AT-09（注入失敗）；以 COM `IDataObject.GetData` 驗證兩種格式轉成 HGLOBAL 且像素一致（不碰使用者剪貼簿）；實機：讀取剪貼簿兩種格式與另存 PNG 一致、另一程序占用時的耗時與錯誤碼
- [x] P4-03 `PngFileService`：預設檔名 `Screenshot_yyyyMMdd_HHmmss_fff.png`（開啟的圖片也相同，不預設來源檔名）、初始目錄 = 上次成功位置（仍存在時）或圖片資料夾、覆寫確認、背景執行緒 `PngBitmapEncoder`、temp→`File.Move(overwrite)`、失敗提示保留文件、成功更新 `LastSaveDirectory` 與 `ExportedVersion`（合成當時的版本）；複製與儲存進行中停用兩個命令｜P4-01、P1-10｜high｜否｜AT-09（唯讀目錄）；實機另存尺寸 = CropRect
- [x] P4-04 開啟既有圖片：`Export/ImageFileLoader`（PNG/JPEG/BMP；先讀取影格標頭的尺寸、超過 1 億像素在解碼前拒絕；`IgnoreColorProfile`；透明度以白色背景合成為凍結的 `Bgr32`，與截圖相同全不透明；不套用 EXIF 方向；格式錯誤／損壞友善提示）、Tray 選單新增「開啟圖片」與編輯器「開啟」按鈕（`Ctrl+O`）、另存預設不同名避免覆寫來源｜P3-13｜medium｜可（含 AT-10 測試）｜AT-10：有效 PNG/JPEG/BMP、格式錯誤、損壞、超大尺寸
- [x] P4-05 未匯出變更保護：`UndoRedoStack.Version`（每個狀態一個版本，復原／重做回到相鄰狀態的版本，捨棄最舊一筆時更新底層版本）、`ImageDocument.ExportedVersion`／`IsDirty`（新文件沒有未匯出的變更）；`UnsavedChangesDialog`（複製／儲存／捨棄／取消）；套用於新截圖（擷取前）、開啟圖片（開檔對話框前）、關閉編輯器、Tray 結束（確認後才 `Shutdown`）、登出或關機（拒絕結束工作階段後詢問）；詢問前先完成文字輸入；對話框期間忽略新的截圖、開啟與結束要求｜P4-02、P4-03｜high｜否｜版本與 IsDirty 單元測試；實機四種路徑皆提示；取消時文件不變；登出或關機需實際登出（未驗證時標示）
- [x] P4-06 文件切換與視窗行為：單一文件；複製／儲存後留在編輯；取消新截圖恢復原文件；關閉編輯器回常駐｜P4-05｜medium｜否｜實機流程驗證
- [x] P4-07 自動化測試：`ImageRendererTests`（AT-07）、`ClipboardFailureTests`／`PngSaveFailureTests`（AT-09）、`ImageFileLoaderTests`（AT-10）、`DirtyTrackingTests`｜P4-01～P4-05｜medium｜可｜全數通過
- [x] P4-08 Agent 端實機驗證：複製→PowerShell 讀取剪貼簿影像→與另存 PNG 逐像素比對；開啟 JPEG/BMP；唯讀資料夾另存失敗後文件仍在；剪貼簿被占用模擬（另一程序 OpenClipboard）後提示｜P4-06｜high｜否｜證據記錄
- [x] P4-09 使用者實機驗證請求：MT-08（PowerPoint 貼上與 PNG 一致；Word／Teams 可接收）、MT-09（磁碟不可寫、剪貼簿占用、顯示器拔除）｜P4-08｜low｜否｜於控制點提出
- [x] P4-11 更新 `tools/smoke/Invoke-Phase2Smoke.ps1` 為 Phase 3 之後的行為（以編輯器 `Ctrl+C` 取得選取結果，比對後關閉編輯器），P5-07 以發佈版重跑各階段腳本前必須完成｜P4-06｜medium｜否｜腳本語法剖析通過；電腦閒置時實際執行
- [x] P4-10 Phase 收尾儀式（handoff `handoffs/phase-04-image-io.md`）｜全部｜low｜否｜handoff 存在並註記

**Acceptance Criteria（Phase 門檻）**
1. FR-24、FR-26～FR-32 可操作；AT-07、AT-09、AT-10 通過。
2. 剪貼簿與 PNG 可視像素一致（Agent 逐像素比對 + 使用者 PowerPoint 回報）。

**Handoff**：`handoffs/phase-04-image-io.md`（2026-10-18；880 個自動化測試通過；煙霧驗證 Phase 4 22／22、Phase 3 35／37（失敗的 2 項為待辦 P5-11 的 Delete 鍵）、Phase 2 重跑 38／38）。
- **Status:** complete

### Phase 5: 完整驗證與發佈

**目標**：繁中介面與操作性總檢、錯誤恢復總檢、資源與效能量測、完整文件、`win-x64` 自包含可攜發佈與 ZIP、全部自動化測試與實機驗收記錄、最終報告。
**依賴**：Phase 4。
**分支**：`feature/phase-05-release`。

Checklist
- [x] P5-00 ADR-0017（發佈與封裝策略：自包含、可選單檔、不簽章）｜無｜low｜否｜ADR Accepted（依試發佈的事實決定：資料夾版為主、單檔版經煙霧驗證後才提供、只保留 zh-Hant 附屬組件、Version 1.0.0、授權檔）
- [x] P5-01 繁中介面與操作性總檢：所有 UI 文字在 `Strings.resx`、ToolTip、AccessKey、Tab 順序、焦點樣式、圖示附文字｜Phase 4｜medium｜否｜掃描 XAML/程式碼無硬編碼文字；實機鍵盤導覽
- [x] P5-02 錯誤恢復總檢：顯示器變更、剪貼簿、檔案、設定損毀、超大圖片、快捷鍵衝突；訊息統一繁中｜Phase 4｜medium｜否｜各情境有測試或實機證據
- [x] P5-03 資源與效能量測：30 次「截圖→取消→編輯→複製→儲存→關閉」循環的 GDI 物件數、Handle、Working Set 趨勢；FHD／4K 擷取與開啟編輯器延遲（附量測條件）｜Phase 4｜high｜否｜數據記錄於 `docs/ACCEPTANCE.md`，無持續成長趨勢
- [x] P5-04 文件：`README.md`、`docs/ARCHITECTURE.md`（模組關係、資料模型、座標系統、狀態機）、`docs/USER_GUIDE.md`、`docs/ACCEPTANCE.md`（AT 結果 + MT 記錄與證據）、`docs/KNOWN_LIMITATIONS.md` 完整版、第三方 NOTICE（若有）｜Phase 4｜medium｜可（USER_GUIDE、KNOWN_LIMITATIONS）｜文件存在、相對路徑、與程式一致
- [x] P5-05 發佈：`tools/Publish-Release.ps1`（`dotnet publish -c Release -r win-x64 --self-contained true`）產生 `publish/ScreenshotApp-1.0.0-win-x64/` + ZIP；單檔版 `publish/ScreenshotApp-1.0.0-win-x64-singlefile/`（`PublishSingleFile` + `IncludeNativeLibrariesForSelfExtract`）並驗證圖示、資源與截圖功能｜P5-01～P5-03｜medium｜否｜發佈目錄可執行；ZIP 存在；單檔版通過煙霧驗證或明確標示未提供
- [x] P5-06 完整自動化測試執行與報告（AT-01～AT-10 對應測試全數通過，記錄數量與時間）｜Phase 4｜low｜否｜`dotnet test` 輸出記錄
- [x] P5-07 Agent 端最終煙霧驗證（以發佈版 exe 執行 Phase 1～4 的煙霧腳本）｜P5-05｜medium｜否｜證據記錄
- [x] P5-08 使用者實機驗收總表：MT-01～MT-11 清單、證據欄位、未驗證標示（MT-11 需乾淨機器）｜P5-04｜low｜否｜`docs/ACCEPTANCE.md` 完整
- [x] P5-09 最終報告：需求完成情況（FR-01～FR-35）、未完成／未驗證項目、測試結果、發佈檔案位置、已知限制｜全部｜medium｜否｜報告交付使用者
- [x] P5-11 待辦（使用者指示先列待辦、繼續後續工作）：選取標註後按 Delete 沒有刪除——Phase 3 煙霧驗證與診斷腳本以 `SendInput` 送出 Delete 皆無效，同樣送法的 `Ctrl+Z`、`Ctrl+Y`、Enter 有效；先確認實體鍵盤是否有效，再查路由或可執行條件，寫能重現的測試後修正（調查紀錄見 `findings.md`「待辦：Delete 鍵」）｜無｜xhigh｜否｜重現測試先紅後綠；Phase 3 煙霧腳本「選取箭頭後按 Delete 刪除」與「刪除後 Ctrl+Z 恢復箭頭」通過（結案：不是產品缺陷。診斷版的輸入紀錄顯示 Delete 確實刪除了箭頭；腳本以「偏紅」像素判斷，已知圖樣在該範圍本來就有 560 個符合條件的像素。腳本改為比較與原圖不同的像素，發佈版重跑兩項皆通過。判別測試 `SelectTool_ClickOnAnnotationWithoutMoving_EndsDragSoDeleteAndUndoWork` 直接通過，保留為回歸測試）
- [x] P5-10 Phase 收尾儀式（handoff `handoffs/phase-05-release.md`、版本標記 `v1.0.0`）｜全部｜low｜否｜handoff 存在並註記

**Acceptance Criteria（Phase 門檻）**
1. 規格第 10 節交付物齊全；所有 AT 通過；MT 有證據或明確標示「尚未驗證」。
2. 發佈目錄在本機可執行完整流程。

**Handoff**：`handoffs/phase-05-release.md`（2026-10-18；894 個自動化測試通過；發佈版煙霧驗證資料夾版 Phase 1～4 為 31／38／38／25、單檔版 Phase 1～2 為 31／38，全數通過；30 次完整循環 GDI 中位數 128→128；最終 ZIP 於 v1.0.0 的 commit 重新產生）。
- **Status:** complete

### Phase 6: AGENTS.md（使用者要求，v1.0.0 之後）

**目標**：產生給 AI 程式代理的工作指引 `AGENTS.md`：只收錄從程式碼與設定看不出來的慣例、決策原因與陷阱，並以指向規格、ADR、架構與驗收文件的方式處理其餘內容；內容與目前的程式碼和文件一致。
**依賴**：Phase 5。
**分支**：`feature/phase-06-agents-md`。

Checklist
- [x] P6-01 記錄使用者對 Phase 5 控制點問題的回覆（全部依預設）
- [x] P6-02 撰寫 `AGENTS.md`｜驗收：逐條對照程式碼、腳本與文件確認無誤；只含看不出來的慣例與陷阱
- [x] P6-03 收尾：CHANGELOG、計畫與進度；commit 並 fast-forward 合併回 `main`；`v1.0.0` 標籤維持原位

- **Status:** complete

### Phase 7: 右鍵選單與裁切後填滿畫布（使用者要求，v1.0.0 之後）

**目標**：畫布依情境提供三份右鍵選單（畫布空白處、標註上、裁切工具中），並加入再製（`Ctrl+D`）、反轉箭頭方向、重新編號所有步驟、清除所有標註與還原完整圖片；套用裁切後以裁切範圍填滿畫布（符合視窗上限 400%），復原回到裁切前的狀態並重新符合視窗。裁切維持非破壞性，文件模型與輸出不變。
**依賴**：Phase 6。
**分支**：`feature/phase-07-context-menu-crop-fill`（改動超過 3 個檔案）。

Checklist
- [x] P7-01 規格先行：FR-14 右鍵選單、FR-16 再製與反轉箭頭、FR-18 重新編號、FR-21 裁切後填滿畫布、FR-25 `Ctrl+D`、新增 FR-36（右鍵選單內容）；文件狀態加修訂註記｜無｜medium｜否｜規格與使用者確認的方案一致
- [x] P7-02 命令與工作階段：`CompositeCommand`（依序執行、反序復原）；`EditorSession` 新增還原完整圖片、清除所有標註、編輯選取的標註（與雙擊相同）、再製、反轉箭頭方向、重新編號所有步驟，各至多一筆歷史｜無｜high｜否｜`EditCommandTests`、`EditorSessionTests` 先紅後綠
- [x] P7-03 縮放：`EditorZoom.Fit` 可放大到最大階段；只顯示裁切範圍時符合視窗上限 400%、整張圖片維持 100%；離開裁切工具或復原、重做後顯示的裁切範圍與上次不同時回到符合視窗｜無｜high｜否｜`EditorZoomTests`；Phase 3 煙霧第 9 步的縮放斷言
- [x] P7-04 右鍵選單：三份 XAML 選單（Click 處理，與既有標註選單相同）、開啟前同步設定可用與顯示狀態、`Ctrl+D` 再製；介面文字在 `Strings.resx`｜P7-02｜high｜否｜`EditorWindowTests`（具名項目存在、依情境選擇選單與狀態）、`StringsResourceTests`
- [x] P7-05 文件：ADR-0010、ADR-0012 修訂；ARCHITECTURE、USER_GUIDE、KNOWN_LIMITATIONS、ACCEPTANCE、CHANGELOG `[Unreleased]`｜P7-01～P7-04｜medium｜否｜與程式一致、相對路徑
- [x] P7-06 驗證：Release 建置 0 警告、完整測試；電腦閒置時以 Phase 3 煙霧腳本（含新斷言）實機確認，否則標示「尚未驗證」｜P7-01～P7-05｜medium｜否｜證據記錄（Release 0 警告、933 passed；煙霧腳本語法與內嵌 C# 通過，實機執行等待使用者閒置，結果記錄於 ACCEPTANCE 第 2 節 Phase 7，未執行前為「尚未驗證」）
- [x] P7-07 收尾：findings、progress、handoff；Phase 標記 complete 並重新 attest；使用者沒有要求提交，不 commit｜全部｜low｜否｜handoff 存在
- [x] P7-08 版本與文件對齊（Key Question 12）：`Version` 1.1.0（新功能，語意化版本次版號）與 manifest；CHANGELOG `[Unreleased]` 改為 `[1.1.0] - 2026-10-19`；README、USER_GUIDE、KNOWN_LIMITATIONS 的版本與發佈檔名；ACCEPTANCE、AGENTS.md、ADR-0017、規格修訂紀錄與目前狀態一致｜P7-01～P7-07｜medium｜否｜以 `1.0.0`／`v1.0` 搜尋確認只剩歷史紀錄與規格範圍用語
- [x] P7-09 發佈：Release 建置 0 警告、完整測試；commit 前試發佈確認兩種版本與 ZIP；電腦閒置時以發佈版執行 Phase 3 煙霧腳本，否則標示「尚未驗證」｜P7-08｜medium｜否｜`publish/ScreenshotApp-1.1.0-win-x64*`（Release 0 警告、933 passed；資料夾版 275 檔／155.9 MB、ZIP 67.3 MB，單檔版 8 檔／149.4 MB、ZIP 63.5 MB；使用者一直在使用電腦，發佈版煙霧驗證尚未驗證）
- [x] P7-10 提交與合併：`phase-07` commit、本機標籤 `v1.1.0`（不推送），以該 commit 重新發佈（產品版本內嵌 commit），fast-forward 合併回 `main`｜P7-09｜low｜否｜`main` 與標籤指向同一個 commit

**Handoff**：`handoffs/phase-07-context-menu-crop-fill.md`（2026-10-19；933 個自動化測試通過；v1.1.0，發佈版煙霧驗證尚未驗證）。
- **Status:** complete

## 追蹤矩陣：FR → 模組 → 測試 → 驗收證據

| FR | 模組／檔案 | 自動化測試 | 驗收證據 | Phase |
|----|-----------|-----------|---------|-------|
| FR-01 | Platform/TrayService | — | Agent 煙霧 + MT-01 | 1/2/4 |
| FR-02 | Platform/HotkeyService、Settings/HotkeyGesture | HotkeyGestureTests、HotkeyServiceTests | Agent 煙霧 + MT-02 | 1/2 |
| FR-03 | HotkeyService.Rebind、SettingsWindow | HotkeyServiceTests | Agent 煙霧 + MT-02（占用情境） | 1 |
| FR-04 | Platform/SingleInstanceGuard | — | Agent 煙霧（處理程序數） + MT-01 | 1 |
| FR-05 | Platform/StartupService | StartupServiceTests | Agent 煙霧（登錄值） | 1 |
| FR-06 | AppController.Exit、TrayService | AppStateMachineTests（Phase 2） | Agent 煙霧 + MT-01 | 1/2/4 |
| FR-07 | AppController 截圖流程、CaptureService | CaptureFlowTests | Agent 截圖證據 + MT-03 | 2 |
| FR-08 | SelectionOverlayWindow、SelectionController | SelectionLogicTests | Agent 截圖 + MT-03/04 | 2 |
| FR-09 | MonitorService、CoordinateMapper | CoordinateMapperTests（AT-01） | MT-04/05 | 2 |
| FR-10 | SelectionController | SelectionLogicTests（AT-02） | Agent 煙霧 + MT-03 | 2 |
| FR-11 | AppController → EditorWindow | CaptureFlowTests | Agent 煙霧 + MT-03 | 2/3 |
| FR-12 | AppController 錯誤路徑 | CaptureFlowTests | MT-09 | 2 |
| FR-13 | docs/KNOWN_LIMITATIONS.md | — | 文件 | 2/5 |
| FR-14 | EditorWindow、EditorSession | EditorWindowTests（右鍵選單，Phase 7） | Agent 截圖 + MT-07 | 3/7 |
| FR-15 | Editor/Tools/* | AnnotationGeometryTests | Agent 截圖 | 3 |
| FR-16 | Model/Annotation*、Commands、EditorSession（再製、反轉箭頭、清除所有標註） | AnnotationGeometryTests（AT-04）、EditorSessionTests | Agent 截圖；Phase 7 項目見 ACCEPTANCE 第 3 節使用者驗證步驟 | 3/7 |
| FR-17 | TextTool、TextAnnotation | TextAnnotationTests（AT-06） | MT-06 | 3 |
| FR-18 | StepNumberTool、StepNumberAnnotation、EditorSession.RenumberSteps | AnnotationGeometryTests、EditorSessionTests | Agent 截圖；重新編號見 ACCEPTANCE 使用者驗證步驟 | 3/7 |
| FR-19 | 屬性列、AnnotationStyle | EditorSessionTests（樣式） | Agent 截圖 | 3 |
| FR-20 | CropTool、SetCropCommand、EditorSession.ResetCrop | CropCommandTests（AT-03）、EditorSessionTests | Agent 截圖；Phase 3 煙霧第 9 步（右鍵還原完整圖片） | 3/7 |
| FR-21 | EditorWindow 縮放、CoordinateMapper、EditorZoom.Fit（裁切後放大） | CoordinateMapperTests（AT-01 縮放平移）、ImageRendererTests、EditorZoomTests | MT-07；Phase 3 煙霧第 9 步（裁切後填滿） | 3/7 |
| FR-22 | Commands/UndoRedoStack、CompositeCommand | UndoRedoStackTests（AT-05）、EditCommandTests | Agent 煙霧 | 3/7 |
| FR-23 | UnsavedChangesDialog、DirtyTracking | DirtyTrackingTests | Agent 煙霧 | 4 |
| FR-24 | Export/ImageFileLoader | ImageFileLoaderTests（AT-10） | Agent 煙霧 | 4 |
| FR-25 | EditorWindow InputBindings | EditorWindowTests（Ctrl+D，Phase 7） | Agent 煙霧 | 3/7 |
| FR-26 | Export/ImageRenderer | ImageRendererTests（AT-07） | Agent 像素比對 | 3/4 |
| FR-27 | ImageRenderer | ImageRendererTests（AT-07） | Agent 像素比對 | 4 |
| FR-28 | Export/ClipboardService | ClipboardFailureTests | Agent 剪貼簿讀取 + MT-08 | 4 |
| FR-29 | ClipboardService 重試 | ClipboardFailureTests（AT-09） | MT-09 | 4 |
| FR-30 | Export/PngFileService | PngSaveFailureTests（AT-09） | Agent 煙霧 | 4 |
| FR-31 | 全專案（無自動存檔） | 程式碼審視 | 文件 | 4/5 |
| FR-32 | ClipboardService + ImageRenderer | ImageRendererTests | MT-08 | 4 |
| FR-33 | Settings/AppSettings、SettingsStore | SettingsStoreTests（AT-08） | Agent 煙霧 | 1 |
| FR-34 | SettingsStore 復原 | SettingsStoreTests（AT-08） | Agent 煙霧 | 1 |
| FR-35 | Strings.resx、所有 XAML | StringsResourceTests（資源鍵、XAML 硬編碼掃描） | 實機鍵盤導覽 | 1/5 |
| FR-36 | EditorWindow（三份右鍵選單、PrepareContextMenu）、EditorSession | EditorWindowTests、EditorSessionTests | Phase 3 煙霧第 9 步（畫布選單）；其餘見 ACCEPTANCE 第 3 節 Phase 7 使用者驗證步驟 | 7 |

## 需使用者實機驗證的項目（MT）與提出時機

| MT | 內容 | 提出控制點 | 原因 |
|----|------|-----------|------|
| MT-01 | 首次啟動只進匣、重複啟動、退出重啟 | Phase 1 完成後 | Agent 可部分驗證，匣圖示視覺需真人確認 |
| MT-02 | 快捷鍵被占用提示；快捷鍵在桌面／瀏覽器／PowerPoint 可觸發截圖 | 占用提示：Phase 1 完成後；觸發截圖：Phase 2 完成後 | Phase 1 尚無截圖動作；觸發需前景應用程式切換 |
| MT-03 | 選取後自動開編輯器；取消不留遮罩 | Phase 3 完成後 | 編輯器於 Phase 3 才存在 |
| MT-04 | 單螢幕 100/125/150/200% 尺寸正確 | Phase 2 完成後 | 需變更系統縮放 |
| MT-05 | 雙螢幕混合 DPI、負 X、上下排列、非主螢幕 | Phase 2 完成後 | 需實體多螢幕配置 |
| MT-06 | 微軟注音輸入與重編 | Phase 3 完成後 | IME 需真人輸入 |
| MT-07 | 25/100/400% 操作後匯出一致 | Phase 3 完成後 | 需人工檢視 |
| MT-08 | PowerPoint 貼上與 PNG 一致；Word／Teams 可接收 | Phase 4 完成後 | 需 Office |
| MT-09 | 磁碟不可寫、剪貼簿占用、顯示器拔除 | Phase 4 完成後 | 拔除顯示器需實體操作 |
| MT-10 | 30 次循環資源趨勢 | Phase 5（Agent 量測）後由使用者確認 | Agent 可量測 |
| MT-11 | 乾淨 Windows 11 無 .NET Runtime 可執行 | Phase 5 完成後 | 需乾淨機器 |

## ADR 清單（`docs/adr/NNNN-<slug>.md`）

| 編號 | 題目 | Phase |
|------|------|-------|
| 0001 | 技術堆疊與單一專案配置（.NET 10 WPF + Win32、xunit、nuget.config） | 1 |
| 0002 | 座標系統：實體像素為唯一真值，集中於 CoordinateMapper | 1 |
| 0003 | 單一執行個體：Mutex + EventWaitHandle | 1 |
| 0004 | 設定持久化：settings.json、原子寫入、損毀回復 | 1 |
| 0005 | 文字資源策略：resx + 靜態存取 + XAML 標記延伸 | 1 |
| 0006 | 系統匣與 WinForms 使用邊界 | 1 |
| 0007 | 截圖策略：逐顯示器 BitBlt + GetDIBits | 2 |
| 0008 | 覆蓋層定位（HwndSource 直接建立於顯示器實體邊界）與輸入座標（取自事件本身的實體像素） | 2 |
| 0009 | 應用程式狀態機與重入保護 | 2 |
| 0010 | 文件模型與命令模式（Undo/Redo） | 3 |
| 0011 | 共用 AnnotationPainter：顯示與匯出同一繪製碼 | 3 |
| 0012 | 畫布縮放模型：100% = 實體像素 | 3 |
| 0013 | 文字輸入策略：WPF TextBox 覆蓋 + IME | 3 |
| 0014 | 剪貼簿格式策略：DIB + PNG 雙格式與重試 | 4 |
| 0015 | 檔案寫入與背景編碼：temp→move、Freeze 跨執行緒 | 4 |
| 0016 | 未匯出變更保護流程 | 4 |
| 0017 | 發佈與封裝策略：自包含 win-x64、可選單檔、不簽章 | 5 |

## Key Questions

1. 使用者實機顯示器配置（數量、解析度、縮放）為何？→ 2026-10-18 查詢：單一顯示器 3456×2160、縮放 175%。混合 DPI 與多螢幕本機無法驗證，需使用者以 MT-04／MT-05 回報，否則標示「尚未驗證」。
2. 使用者是否安裝 PowerPoint／Word／Teams？→ Phase 4 控制點詢問；未安裝則 MT-08 標示「尚未驗證」。
3. Git 策略假設（feature branch + ff merge）是否接受？→ 使用者授權 Phase 1 時未覆寫，依計畫採用（2026-10-18）。
4. Phase 2 授權？→ 2026-10-18 使用者以「載入專案狀態，開始開發」授權（與 Phase 1 授權句式相同）。MT-01、MT-02（占用提示）尚未回報，維持「尚未驗證」，於 Phase 2 控制點與 MT-02（觸發）、MT-04、MT-05 一併請使用者回報。
5. Phase 3 授權？→ 2026-10-18 使用者以「使用 planning-with-files 載入專案狀態，繼續開發」授權（與 Phase 1、2 授權句式相同）。Phase 1、2 的 MT 尚未回報，維持「尚未驗證」，於 Phase 3 控制點與 MT-03（編輯器部分）、MT-06、MT-07 一併請使用者回報。
6. Phase 3 收尾時機？→ 2026-10-18 使用者以「完成 phase 3」要求收尾。煙霧驗證後半段需要電腦閒置，使用者正在使用電腦而無法執行，列為「尚未驗證」並保留腳本供之後補完。
7. Phase 4 授權與 MT 回報？→ 2026-10-18 使用者以「使用 planning-with-files 載入專案狀態，繼續開發，完成 MT-01, MT-02, MT-03, MT-06, MT-08 驗證，問題：若擷取畫面時，背景全白，則十字準星看不清」回覆：「繼續開發」與先前授權句式相同，視為授權 Phase 4；詢問後使用者確認五項全數通過、十字游標是唯一問題，已登錄於 `docs/ACCEPTANCE.md`；十字游標問題列為 P2-15 先行修正，使用者同意先單獨 commit 並合併回 `main`，並自行結束執行中的程式以便建置 Release。MT-08 是以 Phase 3 版本（剪貼簿只有 24 位元 DIB）驗證，Phase 4 加入 PNG 格式後需再確認；使用者已安裝 Office（回答 Key Question 2）。
8. Delete 鍵沒有刪除選取標註的問題如何處理？→ 2026-10-18 使用者指示「Delete 鍵失敗原因，整理目前資訊後，先列為待辦，先繼續處理後續問題」：調查紀錄寫入 `findings.md`，列為 P5-11，Phase 4 其餘工作繼續。
9. Phase 5 授權？→ 2026-10-18 使用者以「使用 planning-with-files 載入專案狀態，繼續」回覆（與 Phase 3、4 授權句式相同），視為授權 Phase 5。Phase 4 控制點的問題（十字游標、MT-08 重新確認、Delete 實體鍵盤、未修改的截圖不詢問）與 MT-04、MT-05、MT-07、MT-09 沒有回報，維持「尚未驗證」，於下一次回報時一併提出，並再次告知剪貼簿內容遺失一事。
10. Phase 5 控制點的三個問題？→ 2026-10-18 使用者回覆「都依照預設，並產生 AGENTS.md，並提交」：剛截取或剛開啟、沒有修改的圖片維持不詢問；MT-11 標示「尚未驗證」；不推送到 `upstream`。另要求產生 `AGENTS.md` 並提交（Phase 6）。
11. 右鍵選單與裁切後填滿畫布的方案？→ 2026-10-19 使用者先要求「深度思考、列出建議、使用者決定後再實作」，收到方案後回覆「都依照建議，開始實作」：右鍵選單納入畫布 A1～A5（復原／重做、還原完整圖片、縮放、複製／另存、清除所有標註）、標註 B1～B4（編輯文字或修改編號、再製 `Ctrl+D`、反轉箭頭方向、重新編號所有步驟）、裁切 C1～C3（套用、取消、還原完整圖片），不含「在此加入文字／編號」與「裁切至此標註」；填滿上限 400%；第一次開啟的小圖不放大（維持 100%）。這次沒有要求提交（Phase 7）。
12. Phase 7 完成後的處理？→ 2026-10-19 使用者指示「更新文件，對齊專案狀態，新增版號，建置最終執行版本，提交後合併回 main」：版本定為 1.1.0（新增功能且相容，語意化版本次版號遞增），比照 v1.0.0 加上本機標籤 `v1.1.0`；發佈在 commit 之後以該 commit 執行；不推送 `upstream`（沒有要求）。命中判定是否限制在裁切範圍內沒有回覆，維持現狀。

## Decisions Made

| Decision | Rationale |
|----------|-----------|
| Phase 劃分沿用規格第 9 節五階段，另加 Phase 0 規劃 | 規格已將 FR 分配至各階段，沿用可直接對應驗收 |
| `ImageRenderer` 基礎版提前至 Phase 3 | 讓編輯器的複製／儲存按鈕為真實功能，避免占位按鈕；Phase 4 負責完整化與強化 |
| Phase 2 選取完成先複製到剪貼簿 | 在編輯器完成前提供可驗證的真實結果（Agent 可讀剪貼簿比對尺寸），不是假功能 |
| ~~選取座標以 `GetCursorPos` 實體像素為準，覆蓋層以 `SetWindowPos` 定位~~（Phase 2 已取代：覆蓋層以 `HwndSource` 直接建立於顯示器邊界；座標取自滑鼠事件本身，見下方決策） | 避免 WPF DIP 往返換算與跨 DPI 視窗初始定位錯誤 |
| 標註繪製集中於 `AnnotationPainter`（DrawingContext） | 顯示與匯出共用同一繪製碼，確保 PNG／剪貼簿／畫面一致 |
| 100% 縮放定義為 1 圖片像素 = 1 實體像素 | 符合「尺寸以最終圖片像素為準」且在高 DPI 螢幕上所見即所得 |
| 測試框架 xunit 2.9.x，WPF 物件測試以 STA helper 執行 | 範本內建、本機快取可用 |
| 專案層級 `nuget.config` | 本機未註冊 nuget.org，不改使用者全域設定 |
| 每 Phase 一個 feature branch，完成時 ff 合併回 `main` | 符合「≥3 檔案開分支」規則，`main` 保持通過控制點的狀態 |
| ADR 存於 `docs/adr/`，於各 Phase 開頭撰寫 | 架構決策在實作前定案並可追蹤 |
| 自訂顏色使用 WinForms `ColorDialog` | WPF 無內建色彩對話框；WinForms 已因 NotifyIcon 引用，避免額外套件 |
| 不複製 ShareX／Greenshot 程式碼 | GPL 授權；僅研究行為 |
| GDI SafeHandle 由 P1-03 移至 P2-04 | 唯一使用者為 CaptureService；與使用端一起實作與測試，避免 Phase 1 出現未使用程式碼 |
| 應用程式狀態機由 P1-04 移至 P2-08（與 ADR-0009 同步） | Phase 1 只有常駐狀態，狀態機在截圖流程出現時才有使用者 |
| Phase 1 系統匣選單只放「設定」「結束」 | 規格禁止占位按鈕；「開始截圖」「開啟圖片」隨功能於 Phase 2、4 加入 |
| DPI 感知僅在非 PerMonitorV2 時記錄，驗證改用外部查詢 | NFR-09 日誌只保留錯誤；`GetDpiAwarenessContextForProcess` 可區分 PMv1／PMv2 |
| 快捷鍵註冊以外部 `RegisterHotKey` 衝突檢查驗證；MT-02 觸發部分移至 Phase 2 | Phase 1 尚無截圖動作，不以日誌或假提示冒充 |
| 收尾儀式改為先標記 complete 並 attest，再 commit | 確保 commit 內容、磁碟與 attestation 一致 |
| Phase 1 只委派不涉及建置的圖示產生（P1-14），其餘自行實作 | 單一專案並行 `dotnet build/test` 會造成 obj/bin 衝突；圖示產生不建置專案，可安全並行 |
| 系統匣圖示在快捷鍵註冊之後才顯示 | 圖示出現即代表常駐服務已就緒，消除外部檢查與註冊之間的時間窗 |
| 無法開啟既有具名 Mutex 時視為已有執行個體 | 以系統管理員身分執行的既有執行個體會讓一般權限開啟失敗；避免啟動當機與重複圖示 |
| 煙霧驗證腳本納入 `tools/smoke/`，證據存於 `docs/evidence/phase-NN/` | Phase 5 需以發佈版重跑各階段腳本；scratchpad 會隨工作階段消失 |
| 覆蓋層改用 `HwndSource` 直接以目標顯示器實體像素邊界建立，不用 `Window` + `SetWindowPos` | WPF 原始碼確認 PMv2 下初始 DPI 取自 HWND（`GetDpiForWindow`）；出生即在目標顯示器就沒有跨 DPI 移動路徑。本機只有單一顯示器，跨 DPI 路徑永遠不會被執行到，不能依賴它 |
| 顯示設定變更以覆蓋層 hook 處理 `WM_DISPLAYCHANGE`，不訂閱 `SystemEvents.DisplaySettingsChanged` | 覆蓋層關閉即解除，不留靜態事件訂閱 |
| 快照格式 `Bgr32`（非 Bgra32），`GetDIBits` 直接寫入 `WriteableBitmap` | BitBlt 結果的 alpha 未定義（多為 0），Bgra32 會變成全透明；直接寫入避免多一份 4K 緩衝 |
| `AppStateMachine` 的 Editing、`CoordinateMapper` 的縮放平移換算延至 Phase 3 | 與 SafeHandle、狀態機後移同一原則：在有使用者時才加入；AT-01 縮放平移部分隨 P3-05 驗證 |
| 選取包含起點與終點像素；在起點像素放開視為零尺寸，不產生文件並維持選取狀態 | 可選到顯示器最後一列／行；誤點不會丟掉剛凍結的畫面（與 Windows 剪取工具一致），Esc／右鍵才取消 |
| 截圖流程抽出 `CaptureFlow` + `ICaptureEnvironment` | 讓「失敗、取消、重入時恢復視窗並回到 Resident」可用測試替身驗證，不必在測試中建立系統匣與全螢幕視窗 |
| Phase 2 剪貼簿改放 24 位元 DIB 位元組，不用 `Clipboard.SetImage` | 煙霧驗證量到 `SetImage` 每次留下 1 個 GDI 物件（NFR-04）；DIB 由 Windows 自動轉換為 CF_BITMAP；24 位元避免接收端誤用 alpha。Phase 4 在此基礎上加入 PNG 格式 |
| 隱藏自家視窗前關閉其 DWM 轉場動畫，恢復顯示後再開啟 | 只等 `DwmFlush` 時快照截到設定視窗淡出中的半透明殘影；關閉動畫讓隱藏在下一個合成畫面立即生效，不需猜測等待時間 |
| `MonitorInfo` 只保留 `Index`、`Bounds` | 覆蓋層的 DPI 由 WPF 依 HWND 取得；工作區、DPI、主螢幕、裝置名稱在 Phase 2 沒有使用者 |
| 煙霧驗證以已知圖樣視窗逐像素比對，證據只裁切圖樣範圍；開始前等使用者閒置 | 單螢幕上唯一能證明「無 DPI 偏移、沒有截入自家視窗或選單」的強證據；不把使用者桌面內容存進儲存庫；模擬輸入不干擾正在使用電腦的人 |
| 剪貼簿 DIB 格式以單元測試驗證，複製與 GDI 資源由煙霧驗證涵蓋 | 單元測試寫入真實剪貼簿會覆蓋使用者內容 |
| 選取座標改取自滑鼠事件本身（WPF DIP 經 `CoordinateMapper` 以覆蓋層 DPI 換回實體像素），不在處理當下讀 `GetCursorPos` | 煙霧驗證量到 UI 執行緒延後處理按下事件時，起點落在游標稍後的位置（選取只剩拖曳路徑的 25%／75%）；DIP 往返誤差已證明為 0 |
| 選取框改為實體像素計算的環形幾何，遮罩與選取框不做邊緣平滑 | 175% 下以 DIP 筆劃畫外框會蓋住選取範圍最後一列像素 |
| 標註為不可變 record：矩形／橢圓共用 `BoxAnnotation`、直線／箭頭共用 `SegmentAnnotation`；幾何一律為整數圖片像素 | Undo 只需保存前後兩個值；整數像素讓輸出與測試可精確比對 |
| 命令縮為新增、刪除、替換、裁切、層序五類；皆以 Id 找標註，Execute／Undo 為「設為某值」 | 不可變模型下移動、調整大小、樣式、文字、編號都是「把同一個標註設為新值」，分成多個類別只會重複相同程式碼；設值語意重複執行無害 |
| 拖曳中只更新預覽，放開時才執行一筆命令；文件只經由命令修改 | 文件狀態永遠等於歷史紀錄的結果，取消拖曳只需丟棄預覽，一次拖曳只形成一筆 |
| `ExportedVersion`／`IsDirty` 移至 P4-05；編輯器「開啟」按鈕留在 P4-04 | 有使用者時才加入、不放占位按鈕。Phase 3 期間新截圖直接取代目前文件、關閉編輯器直接丟棄，於 handoff、已知限制與控制點訊息揭露 |
| Image ↔ ViewDip 由 `CoordinateMapper` 以「比例＋畫布原點」換算；控制點層在縮放容器之外；命中容差以螢幕像素定義 | 輸入、控制點與縮放捲動共用同一組純函式，AT-01 縮放平移部分測的是實際使用的程式；控制點在各縮放下維持固定螢幕尺寸 |
| 狀態機：截圖可由常駐或編輯開始並記住先前狀態，結束時回到先前狀態；另加 `BeginEditing`／`EndEditing` | `CaptureFlow` 的回復邏輯不必改；取消新截圖自然回到原本的編輯狀態 |
| 工具邏輯集中於 `EditorSession`（內部以拖曳類別區分），不另建 `Editor/Tools/ITool` 類別階層 | 各工具的差異只在按下時開始哪一種拖曳；集中後工具行為可直接以單元測試驗證 |
| 裁切工具不提供平移：控制點以外的位置一律框選新範圍 | 進入裁切工具時範圍是整張圖片，範圍內拖曳若是平移，最常見的「直接框選」就無從開始 |
| 控制點位移以指標移動量四捨五入計算；平移仍用限制在圖片內的所在像素 | 控制點畫在像素格線上，「所在像素」相減會多算一個像素且到不了圖片邊緣；平移時抓住的位置不應離開圖片 |
| 測試的 GDI 物件量測等連續兩次讀數相同才回傳 | WPF 繪製資源在終結器之後才由繪製執行緒刪除，立即讀取會被其他測試遺留的物件干擾 |
| 煙霧腳本：每次模擬輸入前偵測使用者輸入並檢查前景與目標視窗，編輯視窗以 PrintWindow 擷取，剪貼簿以序號判斷是否還原 | 第一次試跑時使用者回到電腦前，滑鼠操作落到其他程式、證據截到其他程式；保護必須在每一步而不只在開始前 |
| Phase 3 在煙霧驗證部分完成時收尾，未執行的項目標示「尚未驗證」 | 使用者要求收尾，而後半段驗證需要電腦閒置；邏輯已由 774 個自動化測試涵蓋，腳本保留供補完 |
| 剪貼簿重試沿用 .NET 10 內建（`OleSetClipboard`／`OleFlushClipboard` 各 10 次 × 100 ms），不加外層重試；取代 P4-02 原本的「重試 5 次 × 100ms」 | 外層重試會與內建重試相乘，剪貼簿被占用時 UI 執行緒可能阻塞約 10 秒；內建重試已是「有限次短暫重試」 |
| 剪貼簿同時放 24 位元 DIB 與 `PNG`，PNG 與存檔共用同一個編碼函式；位元組在背景執行緒產生 | 支援 PNG 的程式拿到與 PNG 檔相同的位元組，不經接收端轉換；耗時的編碼不阻塞 UI |
| 剛截取或剛開啟的圖片沒有未匯出的變更；以編輯歷史的狀態版本判斷 | 明確需求以「自最後一次成功匯出後曾修改」為條件，優先於流程描述的「未匯出內容」；重截不必多一次確認；修改後復原回原狀不詢問。於控制點告知使用者，可改為新文件一律詢問 |
| 開啟的圖片以白色背景合成透明度，轉為全不透明的 `Bgr32` | 24 位元 DIB 沒有 alpha，若保留透明度，剪貼簿 DIB 的透明處會變黑、與 PNG 不一致 |
| 登出或關機時若有未匯出的變更，拒絕結束工作階段並詢問 | 「關閉或結束時不得無聲丟棄資料」；`Application.Shutdown` 關閉視窗時不能取消，只能在工作階段結束前攔截 |
| MT 回饋修正在獨立分支 `fix/crosshair-contrast` 完成，經使用者同意先單獨 commit 並 fast-forward 合併回 `main`，之後才開始 Phase 4 | 使用者可以立即以新版重新確認；`main` 因此在控制點之外多了一個已測試的修正（Release 825 個測試通過），但不含任何未完成的 Phase 4 工作 |
| 選取覆蓋層改用自繪的黑芯白邊十字游標（單色 AND／XOR、不使用反相），尺寸取系統游標在該 DPI 的標準尺寸，比例與系統十字相同；每種尺寸只建立一次並沿用到程式結束 | 系統十字游標全為反相像素，在暗化成 50% 灰的白色畫面上與底色只差 1 個色階；黑芯白邊對任何底色都有 ≥127 的對比。游標是 USER 物件，快取讓數量不隨截圖次數增加，也不會銷毀仍在顯示中的游標；不調整遮罩透明度，因為那只會把問題移到另一種底色 |
| `ImageRenderer` 改為一律以原始圖片尺寸合成，再取出裁切範圍（整張時直接回傳） | WPF 的平滑像素會隨繪製位置改變：平移到裁切原點再繪製時，小字級文字與斜線在範圍內有幾十到上百個像素與未裁切時不同；整張合成後再取出，裁切只去掉範圍外的像素。代價是每次匯出合成整張圖片（最壞情況與未裁切相同），記錄於 ADR-0011 修訂與已知限制 |
| Delete 鍵問題列為待辦 P5-11，不阻擋 Phase 4 收尾 | 使用者指示「整理目前資訊後，先列為待辦，先繼續處理後續問題」，並要求修正超過 3 次仍失敗的項目列入待辦、不拖累進度 |
| 「使用 planning-with-files 載入專案狀態，繼續」視為授權 Phase 5；未回報的 MT 維持「尚未驗證」並於結尾一併提出 | 與 Phase 3、4 的授權句式相同；先做不依賴答案的工作，問題一次提出 |
| 發佈：資料夾版為主、單檔版經煙霧驗證後才提供；框架附屬組件只保留 zh-Hant；`Version` 1.0.0；隨附 Runtime 授權檔；ZIP 在 v1.0.0 的 commit 之後產生（ADR-0017） | 試發佈實測：12 種非繁中語言 204 檔／17 MB 用不到；單檔版不加 `IncludeNativeLibrariesForSelfExtract` 會留下 5 個 DLL；產品版本內嵌建置當時的 commit |
| 常見 I/O 與登錄錯誤以 `ErrorText` 轉成繁體中文，其他情況沿用原始訊息 | P5-02「訊息統一繁中」；.NET 對拒絕存取、找不到路徑、檔案被占用等的訊息是英文，其餘 Win32 錯誤訊息由 Windows 依系統語言提供 |
| P5-11 以「判別測試 → 暫時的診斷版建置（輸入紀錄寫入 HelpText 與暫存檔）」查明，結論為腳本判定錯誤，不修改產品程式碼 | 一次閒置即取得決定性紀錄；判別測試保留為回歸測試；診斷版只在分離的 worktree 建置，用完移除 |
| 資源與延遲量測加在 Phase 4 腳本（`-Cycles`），不另建 Phase 5 腳本 | Phase 4 腳本已有截圖、複製、另存、關閉與詢問處理的基礎設施；P5-07 以發佈版重跑時即取得發佈版的 MT-10 數據 |
| Phase 4 循環的 GDI 判定改為比較前後各 10 次的中位數，並固定存檔資料夾的內容 | 每次開啟 Windows 存檔對話框時 GDI 約 126～134 且有下一次即回落的單次尖峰，最大值比較會誤判；三次執行的數據與判定修改都記錄於 ACCEPTANCE |
| 字型沿用範圍依實際行為更正註解與已知限制（只在編輯視窗開著時沿用），不改行為 | 規格只要求可修改基本字型；更正文件是最小變更 |
| `docs/ARCHITECTURE.md` 委派 subagent，限定只用讀寫檔工具 | 需要通讀整個程式碼庫；不開主控台以免干擾同時進行的 GUI 驗證；主 agent 抽查後修正三處 |
| 收尾沿用既定儀式：commit、fast-forward 合併回 `main` 並加上本機標籤 `v1.0.0`（不推送） | 計畫 P5-10 已列入且隨 Phase 5 授權；標籤與合併都在本機、可移動；MT 回報有問題時另開修正分支 |
| 使用者確認全部依預設：未修改的截圖不詢問、MT-11 標示「尚未驗證」、不推送 `upstream` | 2026-10-18 使用者回覆「都依照預設」（Key Question 10） |
| `AGENTS.md` 只收錄從程式碼與設定看不出來的慣例、決策原因與陷阱，其餘以指向規格、ADR、架構與 findings 的方式處理；逐條對照程式碼與腳本確認 | 每次都會載入，篇幅即成本；目錄結構與一般指令可直接查得，重複寫入只會過期。煙霧腳本的安全規則與判定陷阱、發佈要在 commit 之後、Release 建置被執行中的程式鎖住等，都是看不出來且曾造成損失的事 |
| `v1.0.0` 標籤維持在 Phase 5 的 commit，Phase 6 只合併回 `main` | `AGENTS.md` 是發佈之後的儲存庫文件，不影響發佈檔案；移動已建立的版本標籤會讓發佈檔案與標籤對不上 |
| 裁切維持非破壞性；「填滿」是畫布的符合視窗規則：只顯示裁切範圍時上限 400%（`EditorZoom.Fit` 的 `enlarge`），顯示整張圖片時仍 100% | 規格 FR-20 要求非破壞性 `CropRect` 且裁切不改寫標註座標；把文件換成裁切後的圖片違反規格。使用者選擇上限 400%、第一次開啟的小圖不放大（Key Question 11）。規則由「顯示範圍是否為整張圖片」推得，不另加旗標 |
| 填滿的觸發：`EditorWindow._shownCrop` 記住上次在裁切工具以外顯示的裁切範圍，不同時切回符合視窗模式；檢查放在 `UpdateDisplayedRegion` 的提早返回之前 | 一個條件涵蓋套用、復原、重做、還原完整圖片；只進出裁切工具時保留使用者的縮放。自我檢查發現「裁切工具中復原再離開」顯示範圍不變，若放在提早返回之後會留下過時的紀錄，之後重做不會填滿 |
| 新增第六類命令 `CompositeCommand`（依序執行、反序復原），ADR-0010 修訂 | 清除所有標註與重新編號所有步驟各應只算一筆復原；專用的清除命令只解決一種操作。再製與反轉只是一筆新增或替換 |
| 三份右鍵選單放在 XAML 資源，沿用既有標註選單的 `Click` 處理；開啟前同步設定可用與顯示狀態；項目以 `LogicalTreeHelper.FindLogicalNode` 依名稱找到 | 與既有選單一致；`RoutedCommand` 的可用狀態以背景優先權重新查詢，選單剛開啟時可能顯示上一刻的狀態。資源中的 `x:Name` 不產生欄位（建置時 CS0103 發現），以名稱查找並由 `EditorWindowTests` 在執行期確認每個名稱 |
| 右鍵點在空白處比照左鍵取消選取；裁切工具中不選取 | 畫布選單開啟時若仍顯示選取外框，使用者會以為選單作用在該標註上（advisor 建議） |
| `EditorWindowTests` 以未啟動的 `AppController` 建立但不顯示 `EditorWindow` | `Load` 只需要控制器的設定；不註冊快捷鍵、不顯示系統匣圖示，設定與日誌寫在暫存目錄。版面需要顯示視窗才會計算，填滿的縮放改由煙霧腳本驗證 |
| 實機驗證只擴充 Phase 3 煙霧腳本第 9 步（裁切後縮放三項、右鍵畫布選單還原完整圖片兩項），不另寫腳本；右鍵位置取編輯視窗中央 | 第 9 步已有裁切、`Get-ZoomText` 與剪貼簿比對（advisor 建議）。腳本只把執行緒設為 PMv2，UI Automation 的座標是否為實體像素沒有把握，改用 `GetWindowRect`；符合視窗時視窗中央落在裁切範圍中央附近的空白處，點錯時找不到畫布選單的項目而判定失敗 |
| 使用者這次沒有要求提交：完成後不 commit、不合併，變更留在 `feature/phase-07-context-menu-crop-fill` | 使用者全域指示「我要求時才 commit」；先前的 Phase 6 是使用者明確說「並提交」才提交 |
| （上一列已由 Key Question 12 取代）版本定為 1.1.0，比照 v1.0.0 加本機標籤 `v1.1.0`、不推送；版本發佈列入 Phase 7（P7-08～P7-10），不另開 Phase | 新增相容的功能與行為調整，語意化版本遞增次版號；發佈只是把 Phase 7 的成果定版，工作量小，沿用同一分支與 `phase-07` commit |
| 順序：文件與版本 → Release 建置與完整測試 → commit 前試發佈（記錄檔案數與大小）→ commit、標籤 → 以該 commit 重新發佈 → fast-forward 合併 | 產品版本內嵌建置當時的 commit，最終 ZIP 必須在 commit 之後產生；試發佈先確認封裝沒有問題、取得要寫進文件的數字，避免為了記錄發佈結果再多一個 commit（比照 v1.0.0） |
| 使用者在使用電腦、沒有閒置時，發佈版煙霧驗證標示「尚未驗證」，不阻擋提交與合併；之後閒置時補跑並以新的 commit 記錄 | 使用者明確要求提交與合併；AGENTS.md 只允許在閒置時執行煙霧腳本。單檔版依 ADR-0017 第 2 點要以該版本的發佈檔通過煙霧驗證才算提供，文件註明驗證前優先使用資料夾版 |
| 文件對齊範圍：版本與發佈檔名（README、USER_GUIDE、KNOWN_LIMITATIONS）、CHANGELOG 版本段落、ACCEPTANCE 的發佈紀錄與使用者驗證步驟、AGENTS.md、ADR-0017 修訂、規格修訂紀錄；v1.0.0 的歷史紀錄（CHANGELOG `[1.0.0]`、ACCEPTANCE Phase 5、ADR 原文）不改 | 歷史紀錄描述當時的狀態，改寫會失去依據；需要沿用的步驟加註改用新版 |

## Errors Encountered

| Error | Attempt | Resolution |
|-------|---------|------------|
| CS0104 `Application` 名稱衝突（WPF + WinForms + ImplicitUsings） | 1 | csproj 加入 `<Using Remove="System.Windows.Forms" />` |
| NU1102 `coverlet.collector` 6.0.4 找不到（僅離線來源） | 1 | 測試專案不引用 coverlet；加入專案層級 `nuget.config` 指向 nuget.org |
| Bash heredoc 寫入長篇 `task_plan.md` 失敗（unexpected EOF） | 1 | 改用 Write 工具寫入大型檔案 |
| Phase 1 再次以 Bash heredoc 寫入長 Python 腳本失敗（同一錯誤） | 2 | 任何超過數行的檔案內容一律用 Write 工具，不再嘗試 heredoc |
| 煙霧驗證：系統匣圖示先於快捷鍵註冊出現，外部檢查誤判且可能干擾註冊 | 1 | 圖示改在註冊之後才顯示 |
| 煙霧驗證：從桌面根節點搜尋 UI Automation 太慢、設定視窗內容未就緒、`SetFocus` 偶爾未生效 | 2 | 先以 Win32 找視窗再搜尋子樹、等待內容就緒、焦點重試且未取得焦點時不送出按鍵 |
| 煙霧驗證：截圖遇開窗動畫或視窗被遮擋；WinForms 下拉選單不支援 `PrintWindow` | 2 | 延遲後以 `PrintWindow` 擷取視窗；選單改由螢幕擷取 |
| 既有具名 Mutex 無法開啟時拋出 `UnauthorizedAccessException`（自我審查發現） | 1 | 以 `MutexAcl` 測試重現後捕捉，視為已有執行個體 |
| 決策紀錄寫成「Phase 1 不委派 Subagent」，與實際委派圖示產生不符（advisor 審查發現） | 1 | 更正決策列並重新 attest |
| 自訂顏色流程沒有任何驗證；補驗證時發現色彩清單 4 欄會截斷「自訂 #12ABEF」 | 1 | 煙霧腳本以視窗訊息操作 Windows 色彩對話框；清單改為 3 欄 |
| pwsh 7.6 `Add-Type` 在 C# 中使用 WinForms／GDI+ 型別：缺組件、型別轉送衝突 | 4 | C# 只用基本型別；WinForms 操作留在 PowerShell；圖樣視窗改為獨立 pwsh 處理程序 |
| 煙霧腳本：嚴格模式下空陣列經函式回傳為 `$null`，`.Count` 拋錯 | 1 | 一律以 `@(...)` 包住函式回傳值 |
| 煙霧驗證：GDI 物件每個循環 +1 | 1 | 診斷測試分離出 `Clipboard.SetImage` 洩漏，改以 DIB 位元組提供剪貼簿圖片，歸零 |
| 煙霧驗證：設定視窗遮住的區域 98.8% 像素差 1 個色階（淡出殘影被截入） | 1 | 隱藏前關閉該視窗的 DWM 轉場動畫 |
| 煙霧驗證：故障日誌比對失敗（記錄行尾的 `\r` 被納入比對） | 1 | 正規表示式排除行尾 `\r` |
| 煙霧驗證：使用者正在操作電腦時圖樣被遮住、自我檢查逾時 | 1 | 腳本開始前等待使用者閒置 20 秒（最多 15 分鐘，否則不做任何變更即結束） |
| 煙霧驗證：選取尺寸只剩拖曳路徑的 25%／75%，30 次循環中 4 次失敗 | 1 | 在處理當下讀 `GetCursorPos` 會讀到游標稍後的位置；改取事件本身的位置 |
| 煙霧驗證：覆蓋層選取範圍最後一列像素與快照不同 | 1 | DIP 筆劃外框在 175% 下蓋住最後一列；改為實體像素環形幾何、不做邊緣平滑 |
| Phase 3 煙霧試跑：使用者回到電腦前，滑鼠點擊／拖曳／Ctrl+滾輪落到 QuiteRSS 與瀏覽器，螢幕擷取的證據截到其他程式 | 1 | 已告知使用者並刪除截圖；每次模擬輸入前偵測使用者輸入、檢查前景與目標視窗，改用 PrintWindow，剪貼簿以序號判斷 |
| Phase 3 煙霧驗證：執行到 Ctrl+Y 之後前景變成 Windows Terminal（沒有使用者輸入） | 2 | 與同時執行的指令有關；腳本加入點擊編輯視窗標題列或狀態列恢復前景，驗證期間不執行其他指令 |
| 裁切範圍為整張圖時無法框選（範圍內拖曳被當成平移） | 1 | 移除平移，控制點以外一律框選 |
| 裁切右、下控制點拖到圖片邊緣差一個像素（subagent 測試發現） | 1 | 控制點位移改以指標移動量四捨五入 |
| CS9124：主要建構函式參數同時被擷取又用來初始化欄位 | 1 | 預覽改為可為 null 的欄位，未移動時回傳原標註 |
| 完整測試時 `SafeHandleTests` 固定失敗、單獨執行通過 | 1 | GDI 量測輔助改為等數量穩定 |
| Firecrawl 搜尋額度不足 | 1 | 改用 mysearch 擷取 WPF 原始碼，只讀需要的段落 |
| MT 回報：擷取全白畫面時十字游標看不清 | 1 | 以腳本確認系統十字全為反相像素（灰 127 上對比 1）；改用自繪黑芯白邊游標（P2-15） |
| Release 建置失敗：使用者執行中的 ScreenshotApp 占用 `bin\Release` 的執行檔 | 1 | 不結束使用者的程式；先以 Debug 組態建置與測試，使用者自行結束程式後 Release 建置 0 警告、825 個測試通過 |
| 測試的 LibraryImport 使用應用程式組件的 `BITMAPINFOHEADER` 出現 SYSLIB1051 | 1 | 測試內宣告同名結構 |
| 對照圖腳本在非 PMv2 執行緒建立游標，PMv2 繪製時被放大 1.5 倍 | 1 | 先切換執行緒 DPI 感知再建立游標（與應用程式相同情境） |
| AT-07 新測試輸出出現純綠像素：文字提交後仍為選取狀態，`SetColor` 套用到文字而非預設值 | 1 | 測試在變更顏色前取消選取（測試寫法問題，非產品缺陷） |
| Phase 4 煙霧驗證：`Invoke-Button` 的 `$condition` 與 `Wait-Until` 參數同名，腳本區塊取到參數本身而例外 | 1 | 改名 `$byId`；Phase 2 腳本同樣修正 |
| Phase 4 煙霧驗證：腳本寫入剪貼簿後未更新序號，中途失敗時誤判為使用者寫入而沒有還原使用者原本的剪貼簿 | 1 | 三支腳本都在自己寫入後立即更新序號；使用者原本的剪貼簿內容已遺失，於 Phase 4 控制點告知 |
| Phase 4 煙霧驗證：另存到唯讀檔時沒有出現程式的錯誤訊息，存檔對話框一直開著，後續步驟被「忙碌」保護擋下 | 2 | 實測為 Windows 存檔對話框自行拒絕唯讀檔；腳本改為記錄並回答每個提示、最後取消存檔，檢查目標檔不變與編輯器恢復可用 |
| Phase 4 煙霧驗證：新截圖前的詢問沒有出現（原因未確定），之後偵測到使用者操作而停止 | 2 | 腳本加入診斷（可見視窗與啟用狀態、編輯器截圖），錯誤訊息框改為先送 `IDOK` 再送 `WM_CLOSE` 關閉；第 4 次執行 22／22 通過。推測先前是上一步的錯誤訊息框沒有關閉、編輯器仍被停用而由忙碌保護擋下（未證實） |
| Phase 3 煙霧驗證：縮放與文字輸入的檢查在動作沒有發生時仍判定通過（按鍵因 Windows Terminal 取走前景沒有送達） | 1 | 縮放比較前要求狀態列顯示目標百分比；文字以 UI Automation 填入並先確認畫布出現文字；工具與複製改用 UI Automation；更正 `findings.md` 先前的通過紀錄 |
| Phase 3 煙霧驗證：以 `SendInput` 送 Ctrl+滾輪，編輯器維持 100% | 2 | 診斷確認產品正常：先按住 Ctrl 再 `PostMessage(WM_MOUSEWHEEL)` 可縮放；腳本改用此方式 |
| Phase 3 煙霧驗證：選取箭頭後按 Delete 沒有刪除（同樣送法的 Ctrl+Z、Enter 有效） | 2 | 兩支診斷腳本排除送鍵與前景問題，程式碼中找不到攔截 Delete 的處理；原因未查明，依使用者指示列為待辦 P5-11 |
| Phase 3 煙霧驗證：裁切後輸出與裁切前同一範圍有 58 個像素不同（都在文字範圍） | 1 | 以測試重現為合成器問題（平移後繪製改變平滑像素）；改為整張合成後再取出裁切範圍，第 1 次修正即通過，煙霧驗證重跑通過 |
| advisor 呼叫逾時 | 1 | 依自己的判斷繼續，之後第二次呼叫成功並採納建議（診斷版加檔案紀錄、停損原則、證據只抄小檔） |
| Bash heredoc 內的 `\\` 變成 `\`（Python 正規表示式出錯、字串出現跳脫警告） | 1 | 含反斜線的腳本改用 Write 工具寫成檔案再執行 |
| P5-11：Delete「沒有刪除」的判定（Phase 3、4 共三次） | 3 | 診斷版輸入紀錄證明 Delete 正常；腳本以偏紅像素判斷，已知圖樣在範圍內本來就有 560 個符合條件的像素 → 改為比較與原圖不同的像素 |
| Phase 4 第 13 步：右鍵取消前前景仍是其他程式的視窗（覆蓋層剛顯示、尚未取得前景） | 1 | 加入 `Wait-OverlayForeground` |
| Phase 4 第 13 步：GDI「後 5 次最大值 > 前 5 次最大值」判定失敗（132→134、128→132） | 2 | 逐次數據為單次尖峰、基準不變；每次刪除存檔、改以中位數判定，第 3 次執行通過 |
| CS0103：`Window.Resources` 中 `ContextMenu` 的 `x:Name` 項目沒有產生欄位 | 1 | 資源以延遲載入建立，XAML 編譯器不為其中的名稱產生欄位；改以 `LogicalTreeHelper.FindLogicalNode` 依名稱查找，`EditorWindowTests` 確認每個名稱在執行期都找得到 |
| Python 以 `write_bytes` 寫入 progress.md 時開檔失敗（OSError Errno 22） | 1 | 檔案可能正被其他程序開啟（findings.md 同樣的寫法先前成功）；確認檔案未被截斷後改用 Edit 工具 |

## Notes

- Phase 狀態只使用 `pending`、`in_progress`、`complete`；人工控制點時不得有 `in_progress`。
- 重大決策前重讀 Goal 與 Next Step；所有錯誤即時記錄於上表。
- 本檔案由 Main Agent 單一寫入；Subagent 回報經整合後才更新。
