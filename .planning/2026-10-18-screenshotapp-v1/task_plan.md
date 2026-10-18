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

Phase 2 已完成（人工控制點）。等待使用者回報 MT（Phase 1 的 MT-01、MT-02 占用提示；Phase 2 的 MT-02 觸發、MT-03 取消部分、MT-04、MT-05，步驟見 `docs/ACCEPTANCE.md`）並授權 Phase 3；授權後從 `main` 建立 `feature/phase-03-editor-core`，將 Phase 3 標記 `in_progress`，先撰寫 ADR-0010～0013。

## Current Phase

Phase 2（截圖與區域選取）已完成（2026-10-18，handoff：`handoffs/phase-02-capture-selection.md`）。Phase 3 等待授權，尚未開始。

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

**Acceptance Criteria（Phase 門檻）**
1. FR-07～FR-13 可操作；AT-01、AT-02 通過；覆蓋層無 DPI 偏移（Agent 截圖證據 + 使用者 MT-04/05 回報）。
2. GDI 資源 30 次循環無成長趨勢（附量測數據）。

**Handoff**：`handoffs/phase-02-capture-selection.md`（2026-10-18；314 個自動化測試通過、煙霧驗證 38 項全數通過、30 次循環 GDI 物件恆為 38）。
- **Status:** complete

### Phase 3: 圖片編輯核心（FR-14～FR-25）

**目標**：圖片文件模型、非破壞性標註八工具、裁切、縮放平移、文字輸入（IME）、步驟編號、樣式、Undo/Redo、編輯器快捷鍵；`ImageRenderer` 基礎版讓複製／儲存按鈕為真實功能。
**依賴**：Phase 2（P2-01、P2-03、P2-08）。
**分支**：`feature/phase-03-editor-core`。

Checklist
- [ ] P3-00 ADR-0010～0013（文件模型與命令模式、共用 AnnotationPainter、畫布縮放模型、文字輸入策略）｜無｜medium｜否｜四份 ADR Accepted
- [ ] P3-01 文件模型：`ImageDocument`（凍結 `SourceBitmap`、`PixelWidth/Height`、`CropRect`、`Annotations`（索引即 Z-order）、`HistoryVersion`、`ExportedVersion`、`IsDirty`、`NextStepNumber`）；`Annotation` 基底（Id、Style）與 `RectangleAnnotation`、`EllipseAnnotation`、`LineAnnotation`、`ArrowAnnotation`、`TextAnnotation`（Text、FontFamily、FontSizePx）、`StepNumberAnnotation`（Number、Center、Radius）；`AnnotationStyle{Color, StrokeWidthPx, FontFamily, FontSizePx}`；幾何：`Bounds`、`HitTest(imagePt, tolerance)`、`Translate`、控制點列舉與 `ApplyHandle`｜P2-01｜xhigh｜否｜AT-04 模型測試
- [ ] P3-02 命令模式：`Editor/Commands/IEditCommand{Execute, Undo, Description}`、`UndoRedoStack`（上限 100、新命令清除 Redo、版本遞增）、`AddAnnotationCommand`、`RemoveAnnotationCommand`、`MoveAnnotationCommand`、`ResizeAnnotationCommand`、`ChangeStyleCommand`、`ChangeTextCommand`、`ChangeNumberCommand`、`SetCropCommand`、`ReorderZCommand`｜P3-01｜high｜可（含測試）｜AT-05：全部可逆、一次拖曳一筆、新修改清除 Redo、上限 100
- [ ] P3-03 `Editor/Rendering/AnnotationPainter`（`DrawingContext` 於圖片像素座標繪製所有標註：矩形／橢圓／直線／箭頭頭部幾何／文字 `FormattedText`（像素字級、換行）／步驟編號圓形+置中數字）；顯示與匯出共用｜P3-01｜xhigh｜否｜渲染快照測試：同一文件在兩種縮放下輸出一致（AT-07 前置）
- [ ] P3-04 `Editor/EditorWindow.xaml` 版面：工具列（八工具 ToggleButton，圖示+文字、ToolTip、AccessKey）、屬性列（8 常用色 + 自訂、線寬、字級、字型）、Undo／Redo／複製／儲存／開啟按鈕、畫布（`ScrollViewer` + 縮放容器 + `Image` + `AnnotationCanvas` 自訂元素）、狀態列（圖片尺寸、裁切尺寸、游標圖片像素、縮放 %）；`EditorViewModel`｜P3-01、P3-03｜xhigh｜否｜實機：開啟後以符合視窗縮放顯示；所有控制項可鍵盤到達
- [ ] P3-05 縮放與平移：符合視窗、100%（1 圖片 px = 1 實體 px，`scale = zoom / dpiScale`）、25%～400% 階段、Ctrl+滾輪以游標為中心、空白鍵／中鍵拖曳平移、視窗調整不改變模型；`CoordinateMapper` 加入 Image ↔ ViewDip（縮放、平移）換算｜P3-04｜high｜否｜AT-01（縮放與平移部分）往返誤差 ≤ 1px；實機：各縮放下控制點尺寸固定、模型座標不變
- [ ] P3-06 工具控制器：`Editor/Tools/ITool`（以圖片像素座標接收 Down/Move/Up/Key）、`SelectTool`（由上而下命中、拖曳移動、控制點調整、Delete）、`ShapeTool`（矩形／橢圓／直線／箭頭拖曳建立，零尺寸不建立）、`StepNumberTool`（點擊建立、自動遞增、刪除不重排）、`CropTool`、`TextTool`；一次拖曳僅在 Up 時提交一筆命令｜P3-02、P3-04｜xhigh｜否｜AT-04 + 實機操作截圖
- [ ] P3-07 文字輸入：畫布上 WPF `TextBox` 覆蓋（`AcceptsReturn`、IME 可用、字級=像素字級隨縮放容器縮放）、Ctrl+Enter／點擊外部=提交、Esc=取消、空字串=取消或刪除、雙擊重編、建立與修改各為一筆命令；編輯中 Ctrl+C／Ctrl+Z 交給 TextBox｜P3-06｜xhigh｜否｜AT-06 模型測試 + 實機輸入中英文與換行
- [ ] P3-08 樣式與屬性列：預設紅色、3px、20px；變更選取物件樣式→`ChangeStyleCommand`；未選取時變更工具預設並回寫設定；自訂顏色使用 WinForms `ColorDialog`｜P3-04、P3-06｜medium｜否｜實機：樣式變更可 Undo；尺寸以圖片像素為準
- [ ] P3-09 裁切：`CropTool` 拖曳建立／調整控制點、Enter 或雙擊套用、重複調整、最小 1×1、夾取於原圖、裁切外暗化顯示、Undo/Redo｜P3-02、P3-06｜high｜否｜AT-03 + 實機
- [ ] P3-10 Z-order：上移／下移／最上／最下（右鍵選單 + Ctrl+]／Ctrl+[）｜P3-02、P3-06｜low｜否｜AT-04 Z-order 測試
- [ ] P3-11 編輯器快捷鍵：Ctrl+Z／Y／C／S、Delete、Esc（取消選取或工具操作）；文字編輯中不攔截｜P3-06、P3-07｜medium｜否｜實機驗證各鍵
- [ ] P3-12 `Export/ImageRenderer` 基礎版（`RenderTargetBitmap(crop.W, crop.H, 96, 96, Pbgra32)`、`Translate(-crop.X,-crop.Y)`、`DrawImage` + `AnnotationPainter`、Freeze）+ 複製按鈕接 `ClipboardService` + 儲存按鈕接 `PngFileService` 基礎版（SaveFileDialog、預設檔名）｜P3-03｜high｜否｜輸出尺寸 = CropRect；複製／儲存為真實功能
- [ ] P3-13 `AppController` 整合：`AppStateMachine` 加入 Editing（取消選取時返回先前狀態）；選取完成→`ImageDocument`→開啟／重用唯一 `EditorWindow`；取消選取時恢復原編輯視窗；關閉編輯器回常駐不退出｜P3-04、P2-08｜medium｜否｜`AppStateMachineTests` 涵蓋 Editing；實機流程驗證
- [ ] P3-14 自動化測試：`ImageDocumentTests`、`AnnotationGeometryTests`（AT-04）、`UndoRedoStackTests`（AT-05）、`CropCommandTests`（AT-03）、`TextAnnotationTests`（AT-06：繁中、英文、Unicode、換行、空字串）、`AnnotationPainterRenderTests`（STA helper）｜P3-01～P3-12｜high｜可（P3-02 Subagent 含命令測試）｜全數通過
- [ ] P3-15 Agent 端實機驗證：啟動→截圖→編輯器開啟；以模擬輸入建立矩形／箭頭／文字／編號→截圖檢視；Undo/Redo；縮放 25/100/400% 後複製輸出尺寸一致｜P3-13｜high｜否｜證據記錄
- [ ] P3-16 使用者實機驗證請求：MT-06（微軟注音）、MT-07（多縮放一致性）｜P3-15｜low｜否｜於控制點提出
- [ ] P3-17 Phase 收尾儀式（handoff `handoffs/phase-03-editor-core.md`）｜全部｜low｜否｜handoff 存在並註記

**Acceptance Criteria（Phase 門檻）**
1. FR-14～FR-25 可操作；AT-03～AT-06 通過；原始 Bitmap 在編輯中不被修改（測試以像素雜湊驗證）。
2. 縮放／平移／視窗調整不改變輸出（AT-07 前置測試）。

**Handoff**：尚未建立。
- **Status:** pending

### Phase 4: 圖片輸入輸出（FR-24、FR-26～FR-32）

**目標**：唯一 `ImageRenderer` 完整化、剪貼簿（含重試與多格式）、PNG 另存（原子寫入、背景編碼）、開啟既有圖片、未匯出變更保護、文件切換行為。
**依賴**：Phase 3。
**分支**：`feature/phase-04-image-io`。

Checklist
- [ ] P4-00 ADR-0014～0016（剪貼簿格式策略、檔案寫入與背景編碼、未匯出變更保護流程）｜無｜medium｜否｜三份 ADR Accepted
- [ ] P4-01 `ImageRenderer` 完整化：像素精確（寬高嚴格 = CropRect）、裁切外標註不出現、顯示縮放不影響、執行緒親和（UI 執行緒渲染後 Freeze）｜P3-12｜high｜可（含 AT-07 測試）｜AT-07：不同畫布縮放輸出尺寸與像素雜湊一致；裁切外圖形不出現
- [ ] P4-02 `ClipboardService` 強化：`DataObject` 同時放 Bitmap(DIB) 與 PNG 串流；`CLIPBRD_E_CANT_OPEN` 重試 5 次 × 100ms；失敗回傳錯誤並保留文件；成功後狀態列輕量提示並更新 `ExportedVersion`｜P4-01｜high｜否｜AT-09（注入失敗）；實機：PowerShell 讀取剪貼簿尺寸與 PNG 一致
- [ ] P4-03 `PngFileService`：預設檔名 `Screenshot_yyyyMMdd_HHmmss_fff.png`、初始目錄 = 上次成功位置或圖片資料夾、覆寫確認、背景執行緒 `PngBitmapEncoder`、temp→`File.Move(overwrite)`、失敗提示保留文件、成功更新 `LastSaveDirectory` 與 `ExportedVersion`｜P4-01、P1-10｜high｜否｜AT-09（唯讀目錄）；實機另存尺寸 = CropRect
- [ ] P4-04 開啟既有圖片：`Export/ImageFileLoader`（PNG/JPEG/BMP → Bgra32 凍結；尺寸上限 1 億像素；格式錯誤／損壞友善提示）、Tray 選單新增「開啟圖片」與編輯器「開啟」按鈕、另存預設不同名避免覆寫來源｜P3-13｜medium｜可（含 AT-10 測試）｜AT-10：有效 PNG/JPEG/BMP、格式錯誤、損壞、超大尺寸
- [ ] P4-05 未匯出變更保護：`IsDirty = HistoryVersion != ExportedVersion`；`UnsavedChangesDialog`（複製／儲存／捨棄／取消）；套用於新截圖、開啟圖片、關閉編輯器、Tray 結束｜P4-02、P4-03｜high｜否｜實機四種路徑皆提示；取消時文件不變
- [ ] P4-06 文件切換與視窗行為：單一文件；複製／儲存後留在編輯；取消新截圖恢復原文件；關閉編輯器回常駐｜P4-05｜medium｜否｜實機流程驗證
- [ ] P4-07 自動化測試：`ImageRendererTests`（AT-07）、`ClipboardFailureTests`／`PngSaveFailureTests`（AT-09）、`ImageFileLoaderTests`（AT-10）、`DirtyTrackingTests`｜P4-01～P4-05｜medium｜可｜全數通過
- [ ] P4-08 Agent 端實機驗證：複製→PowerShell 讀取剪貼簿影像→與另存 PNG 逐像素比對；開啟 JPEG/BMP；唯讀資料夾另存失敗後文件仍在；剪貼簿被占用模擬（另一程序 OpenClipboard）後提示｜P4-06｜high｜否｜證據記錄
- [ ] P4-09 使用者實機驗證請求：MT-08（PowerPoint 貼上與 PNG 一致；Word／Teams 可接收）、MT-09（磁碟不可寫、剪貼簿占用、顯示器拔除）｜P4-08｜low｜否｜於控制點提出
- [ ] P4-10 Phase 收尾儀式（handoff `handoffs/phase-04-image-io.md`）｜全部｜low｜否｜handoff 存在並註記

**Acceptance Criteria（Phase 門檻）**
1. FR-24、FR-26～FR-32 可操作；AT-07、AT-09、AT-10 通過。
2. 剪貼簿與 PNG 可視像素一致（Agent 逐像素比對 + 使用者 PowerPoint 回報）。

**Handoff**：尚未建立。
- **Status:** pending

### Phase 5: 完整驗證與發佈

**目標**：繁中介面與操作性總檢、錯誤恢復總檢、資源與效能量測、完整文件、`win-x64` 自包含可攜發佈與 ZIP、全部自動化測試與實機驗收記錄、最終報告。
**依賴**：Phase 4。
**分支**：`feature/phase-05-release`。

Checklist
- [ ] P5-00 ADR-0017（發佈與封裝策略：自包含、可選單檔、不簽章）｜無｜low｜否｜ADR Accepted
- [ ] P5-01 繁中介面與操作性總檢：所有 UI 文字在 `Strings.resx`、ToolTip、AccessKey、Tab 順序、焦點樣式、圖示附文字｜Phase 4｜medium｜否｜掃描 XAML/程式碼無硬編碼文字；實機鍵盤導覽
- [ ] P5-02 錯誤恢復總檢：顯示器變更、剪貼簿、檔案、設定損毀、超大圖片、快捷鍵衝突；訊息統一繁中｜Phase 4｜medium｜否｜各情境有測試或實機證據
- [ ] P5-03 資源與效能量測：30 次「截圖→取消→編輯→複製→儲存→關閉」循環的 GDI 物件數、Handle、Working Set 趨勢；FHD／4K 擷取與開啟編輯器延遲（附量測條件）｜Phase 4｜high｜否｜數據記錄於 `docs/ACCEPTANCE.md`，無持續成長趨勢
- [ ] P5-04 文件：`README.md`、`docs/ARCHITECTURE.md`（模組關係、資料模型、座標系統、狀態機）、`docs/USER_GUIDE.md`、`docs/ACCEPTANCE.md`（AT 結果 + MT 記錄與證據）、`docs/KNOWN_LIMITATIONS.md` 完整版、第三方 NOTICE（若有）｜Phase 4｜medium｜可（USER_GUIDE、KNOWN_LIMITATIONS）｜文件存在、相對路徑、與程式一致
- [ ] P5-05 發佈：`dotnet publish -c Release -r win-x64 --self-contained true -o publish/win-x64` + ZIP；可選 `PublishSingleFile=true` 版本並驗證圖示、資源與截圖功能｜P5-01～P5-03｜medium｜否｜發佈目錄可執行；ZIP 存在；單檔版通過煙霧驗證或明確標示未提供
- [ ] P5-06 完整自動化測試執行與報告（AT-01～AT-10 對應測試全數通過，記錄數量與時間）｜Phase 4｜low｜否｜`dotnet test` 輸出記錄
- [ ] P5-07 Agent 端最終煙霧驗證（以發佈版 exe 執行 Phase 1～4 的煙霧腳本）｜P5-05｜medium｜否｜證據記錄
- [ ] P5-08 使用者實機驗收總表：MT-01～MT-11 清單、證據欄位、未驗證標示（MT-11 需乾淨機器）｜P5-04｜low｜否｜`docs/ACCEPTANCE.md` 完整
- [ ] P5-09 最終報告：需求完成情況（FR-01～FR-35）、未完成／未驗證項目、測試結果、發佈檔案位置、已知限制｜全部｜medium｜否｜報告交付使用者
- [ ] P5-10 Phase 收尾儀式（handoff `handoffs/phase-05-release.md`、版本標記 `v1.0.0`）｜全部｜low｜否｜handoff 存在並註記

**Acceptance Criteria（Phase 門檻）**
1. 規格第 10 節交付物齊全；所有 AT 通過；MT 有證據或明確標示「尚未驗證」。
2. 發佈目錄在本機可執行完整流程。

**Handoff**：尚未建立。
- **Status:** pending

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
| FR-14 | EditorWindow、EditorViewModel | — | Agent 截圖 + MT-07 | 3 |
| FR-15 | Editor/Tools/* | AnnotationGeometryTests | Agent 截圖 | 3 |
| FR-16 | Model/Annotation*、Commands | AnnotationGeometryTests（AT-04） | Agent 截圖 | 3 |
| FR-17 | TextTool、TextAnnotation | TextAnnotationTests（AT-06） | MT-06 | 3 |
| FR-18 | StepNumberTool、StepNumberAnnotation | AnnotationGeometryTests | Agent 截圖 | 3 |
| FR-19 | 屬性列、AnnotationStyle | ChangeStyleCommand 測試 | Agent 截圖 | 3 |
| FR-20 | CropTool、SetCropCommand | CropCommandTests（AT-03） | Agent 截圖 | 3 |
| FR-21 | 縮放容器、EditorViewModel.Zoom | AnnotationPainterRenderTests | MT-07 | 3 |
| FR-22 | Commands/UndoRedoStack | UndoRedoStackTests（AT-05） | Agent 煙霧 | 3 |
| FR-23 | UnsavedChangesDialog、DirtyTracking | DirtyTrackingTests | Agent 煙霧 | 4 |
| FR-24 | Export/ImageFileLoader | ImageFileLoaderTests（AT-10） | Agent 煙霧 | 4 |
| FR-25 | EditorWindow InputBindings | — | Agent 煙霧 | 3 |
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

## 需使用者實機驗證的項目（MT）與提出時機

| MT | 內容 | 提出控制點 | 原因 |
|----|------|-----------|------|
| MT-01 | 首次啟動只進匣、重複啟動、退出重啟 | Phase 1 完成後 | Agent 可部分驗證，匣圖示視覺需真人確認 |
| MT-02 | 快捷鍵被占用提示；快捷鍵在桌面／瀏覽器／PowerPoint 可觸發截圖 | 占用提示：Phase 1 完成後；觸發截圖：Phase 2 完成後 | Phase 1 尚無截圖動作；觸發需前景應用程式切換 |
| MT-03 | 選取後自動開編輯器；取消不留遮罩 | Phase 3 完成後 | 編輯器於 Phase 3 才存在 |
| MT-04 | 單螢幕 100/125/150/200% 尺寸正確 | Phase 2 完成後 | 需變更系統縮放 |
| MT-05 | 雙螢幕混合 DPI、負 X、上下排列、非主螢幕 | Phase 2 完成後 | 需實體多螢幕配置 |
| MT-06 | 微軟注音輸入與重編 | Phase 3 完成後 | IME 需真人輸入 |
| MT-07 | 25/100/400% 操作後匯出一致 | Phase 4 完成後 | 需人工檢視 |
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

## Notes

- Phase 狀態只使用 `pending`、`in_progress`、`complete`；人工控制點時不得有 `in_progress`。
- 重大決策前重讀 Goal 與 Next Step；所有錯誤即時記錄於上表。
- 本檔案由 Main Agent 單一寫入；Subagent 回報經整合後才更新。
