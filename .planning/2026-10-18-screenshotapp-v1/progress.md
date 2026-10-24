# Progress Log — ScreenshotApp v1.0

## Session: 2026-10-18（Phase 0 規劃）

### Current Status
- **Phase:** 0 - 規劃與環境確認 → 完成；Phase 1 等待使用者授權
- **Started:** 2026-10-18
- **Branch:** `main`（基準 commit）

### Actions Taken
- 讀取規格文件（469 行），擷取 FR-01～FR-35、AT-01～AT-10、MT-01～MT-11 與固定決策。
- 載入 planning-with-files 3.24.1，以 `init-session.sh --gated "ScreenshotApp v1"` 建立 `.planning/2026-10-18-screenshotapp-v1/`（Gated + Slug Mode，自動 attest）；`.mode` 追加 `inject-smart`。
- 環境驗證：.NET SDK 10.0.402、WindowsDesktop 10.0.13；在 scratchpad 建立 WPF + WinForms 煙霧專案與 xunit 煙霧專案並成功建置／執行（1 passed）。
- 發現本機 NuGet 僅有 VS 離線來源；確認 nuget.org 可連線（HTTP 200）；以專案層級 `nuget.config` 解決。
- `git init -b main`；建立 `.gitignore`（排除 bin/obj/publish 與 planning session 狀態檔）、`nuget.config`、`CHANGELOG.md`。
- 撰寫 `findings.md`（需求、環境發現、技術決策）與完整 `task_plan.md`（Phase 0～5、每項 ID／依賴／Effort／委派／驗收、追蹤矩陣、MT 時機表、ADR 清單、決策與錯誤表）。
- 徵詢 advisor：採納「所有 Phase 維持 pending 以便控制點暫停」「inject-smart」「feature branch + ff merge」「註解不得引用編號」「MT 項目排程」等建議。
- 撰寫 `handoffs/phase-00-planning.md`。

### Test Results
| Test | Expected | Actual | Status |
|------|----------|--------|--------|
| `dotnet build -c Release`（WPF + WinForms 煙霧專案） | 0 error | 0 error, 0 warning | PASS |
| `dotnet test`（xunit net10.0-windows + UseWPF 煙霧專案） | 1 passed | 1 passed（8 ms） | PASS |
| `curl https://api.nuget.org/v3/index.json` | HTTP 200 | HTTP 200（0.25 s） | PASS |

### Errors
| Error | Resolution |
|-------|------------|
| CS0104 `Application` 名稱衝突 | `<Using Remove="System.Windows.Forms" />` |
| NU1102 `coverlet.collector` 6.0.4 | 不引用 coverlet；專案層級 `nuget.config` |
| Bash heredoc 寫入長檔失敗 | 改用 Write 工具 |

### Next
- 等待使用者授權 Phase 1。授權後：建立 `feature/phase-01-platform-foundation`、Phase 1 → `in_progress`、撰寫 ADR-0001～0006、建立 Solution 骨架。


## Session: 2026-10-18（Phase 1 平台基礎）

### Current Status
- **Phase:** 1 - Windows 平台基礎 → in_progress
- **Branch:** `feature/phase-01-platform-foundation`

### Actions Taken
- 使用者以「開始開發」授權 Phase 1；Key Question 3（Git 策略）未覆寫，依計畫採用。
- 徵詢 advisor 後調整計畫（GDI SafeHandle 移至 P2-04、狀態機移至 P2-08、系統匣不放占位項目、DPI 只記錄異常、快捷鍵以外部 RegisterHotKey 衝突檢查驗證、收尾先 attest 再 commit），重新 attest。
- P1-00：`docs/adr/0001`～`0006` 六份 ADR（Accepted）。
- P1-01：`ScreenshotApp.sln`（`--format sln`）、`ScreenshotApp.csproj`、`ScreenshotApp.Tests.csproj`、`app.manifest`（PerMonitorV2 + Common Controls v6）。
- P1-03／P1-05／P1-07／P1-08／P1-09／P1-10／P1-12：NativeMethods（LibraryImport）、Strings／StrExtension／Strings.resx、HotkeyGesture／HotkeyService、SingleInstanceGuard、StartupService、AppSettings／SettingsStore／ColorText／CommonColors、ErrorLog。
- P1-04／P1-06／P1-11：GlobalExceptionHandler、AppController、TrayService、ColorPicker、SettingsWindow、App 啟動流程。
- P1-14：委派背景 subagent 產生 `tools/New-AppIcon.ps1` 與 `Resources/app.ico`（不涉及建置，無衝突）。

### Test Results
| Test | Expected | Actual | Status |
|------|----------|--------|--------|
| `dotnet build -c Release`（骨架） | 0 error | 0 error、1 warning（WFO0003） | PASS（警告已處理） |
| `dotnet test`（骨架 smoke） | 1 passed | 1 passed | PASS |
| `dotnet build -c Release`（核心程式碼） | 0 error, 0 warning | 0 error, 0 warning | PASS |
| `dotnet test`（Phase 1 測試套件） | 全數通過 | 113 passed（第一次 112/113，資源鍵掃描抓到間接引用，修正後全過） | PASS |

### Errors
| Error | Resolution |
|-------|------------|
| Bash heredoc 寫入長 Python 腳本失敗（unexpected EOF，Phase 0 已記錄的同一問題） | 改用 Write 工具；之後超過數行的內容一律用 Write |
| WFO0003：WinForms 分析器要求移除 manifest 的 DPI 設定 | WPF 啟動流程必須用 manifest；於 csproj 以 NoWarn 抑制並註明原因 |
| CS0103 `Path`／`File` 不存在 | WPF 隱含 using 不含 `System.IO`；程式檔明確加入，測試專案以 `<Using Include="System.IO" />` |
| `EveryResourceKey_IsReferenced` 失敗（`Error_Unexpected`、`Error_StartupFailed`） | 鍵以參數間接傳遞；改為呼叫端以字面鍵取字串 |

### Actions Taken（續）
- P1-14 圖示由背景 subagent 完成：`tools/New-AppIcon.ps1` 可重現產生 8 個尺寸；16～64 以 `System.Drawing.Icon` 載入與檔案像素逐點一致。256 以該建構子會回傳 64，原因是 System.Drawing 的選圖邏輯（ICO 規格以 0 表示 256），檔案本身正確，系統匣只用小尺寸不受影響。
- P1-01 補完 `README.md`；`ApplicationIcon` 與內嵌 `app.ico` 接入 csproj。
- P1-15 撰寫 `tools/smoke/Invoke-Phase1Smoke.ps1`（Win32 + UI Automation），以 NotifyIcon 回呼訊息模擬雙擊與右鍵選單，透過「結束」選單項目走真實結束流程；涵蓋設定視窗變更快捷鍵、衝突保留原設定、開機啟動登錄值。
- 自我審查：既有執行個體以系統管理員身分執行時 `new Mutex` 會拋出 `UnauthorizedAccessException`（發生在全域例外處理掛上之前）；先以 `MutexAcl` 測試重現，再修正為視為已有執行個體。
- 建立 `docs/ACCEPTANCE.md` 初版與 `docs/evidence/phase-01/`；ADR-0003 補充管理員執行個體情境。
- 刪除煙霧驗證產生的 `%LocalAppData%\ScreenshotApp`（只含預設值），還原為從未執行狀態；腳本已改為自動還原。
- 撰寫 `handoffs/phase-01-platform-foundation.md`。

### Smoke Verification（P1-15）
| 執行 | 結果 | 備註 |
|------|------|------|
| 第 1 輪（21 項） | 15 PASS／4 FAIL | 系統匣圖示先於快捷鍵註冊出現（競態）→ 圖示改在註冊後顯示 |
| 第 2 輪（21 項） | 21 PASS | 之後擴充設定視窗操作與開機啟動登錄值，共 28 項 |
| 擴充後前 6 輪 | 不穩定：2 輪中途例外、2 輪各 1 FAIL | UIA 全樹搜尋慢、視窗內容未就緒、`SetFocus` 未生效；皆為測試工具問題，已修正 |
| 最後 7 輪（28 項） | 28 PASS，連續無失敗 | 環境：Windows 11 Pro 10.0.26200，單一顯示器 3456×2160 @175% |

### Test Results（最終）
| Test | Expected | Actual | Status |
|------|----------|--------|--------|
| `dotnet build ScreenshotApp.sln -c Release` | 0 error, 0 warning | 0 error, 0 warning | PASS |
| `dotnet test ScreenshotApp.sln -c Release` | 全數通過 | 114 passed（11 個測試類別） | PASS |
| `tools/smoke/Invoke-Phase1Smoke.ps1` | 全數通過 | 28 PASS、0 FAIL | PASS |
| 程式碼註解掃描（編號、文件路徑） | 無違規 | 無違規 | PASS |

### Next
- Phase 1 已完成並合併回 `main`。等待使用者回報 MT-01、MT-02（占用提示）並授權 Phase 2。

### 控制點前補正（advisor 審查後）
- 更正計畫決策列：Phase 1 只委派不涉及建置的圖示產生（原寫「不委派」與事實不符），重新 attest。
- 自訂顏色（`ColorPicker`）原本沒有任何驗證：煙霧腳本新增「開啟 Windows 色彩對話框 → 以視窗訊息填入 RGB → 確定 → 清單選取自訂色並寫入設定檔 → 選回紅色」。
- 截圖證據發現色彩清單 4 欄時「自訂 #12ABEF」被截斷，改為 3 欄（3×3）。
- 結果清單改為逐行輸出（不截斷），執行檔以儲存庫相對路徑記錄。
- handoff 補充風險：設定檔讀取失敗後再套用會覆寫原檔。
- 煙霧驗證：31 項全數通過，連續 4 次無失敗；`dotnet test` 114 passed；證據更新於 `docs/evidence/phase-01/`。


## Session: 2026-10-18（Phase 2 截圖與區域選取）

### Current Status
- **Phase:** 2 - 截圖與區域選取 → in_progress
- **Branch:** `feature/phase-02-capture-selection`

### Actions Taken
- 使用者以「載入專案狀態，開始開發」授權 Phase 2；MT-01、MT-02（占用提示）尚未回報，維持「尚未驗證」。
- 查 WPF 原始碼確認 `WM_DPICHANGED` 處理與 PMv2 初始 DPI 取自 HWND（記錄於 findings）；徵詢 advisor 後改採「HwndSource 直接建立於顯示器實體邊界」，計畫與決策更新後重新 attest。
- P2-00：`docs/adr/0007`～`0009`（截圖策略、覆蓋層定位與輸入、狀態機與重入）。
- P2-12：`docs/KNOWN_LIMITATIONS.md` 初稿。
- P2-01＋P2-03 委派 subagent：`PixelPoint`、`PixelRect`、`CoordinateMapper` 與 145 個測試；主 agent 審查 API、語意與註解後整合。
- 主 agent 實作：`NativeMethods` 擴充、`GdiHandles`（三種 SafeHandle）、`MonitorService`、`CaptureService`、`DesktopSnapshot`、`DragSelection`、`SelectionOverlay`（HwndSource）、`SelectionController`、`CaptureFlow`／`ICaptureEnvironment`／`DesktopCaptureEnvironment`、`AppStateMachine`、`Export/ClipboardService`、系統匣「開始截圖」、`AppController` 接線、資源字串。

### Test Results
| Test | Expected | Actual | Status |
|------|----------|--------|--------|
| subagent：`dotnet build` / `dotnet test` | 0 warning / 全過 | 0 warning / 259 passed | PASS |
| 整合後 `dotnet build -c Release` | 0 error, 0 warning | 0 error, 0 warning | PASS |
| 整合後 `dotnet test -c Release` | 全數通過 | 302 passed | PASS |

### Actions Taken（續：實機煙霧驗證與修正）
- 撰寫 `tools/smoke/Show-TestPattern.ps1`（已知圖樣視窗，螢幕上 96 萬像素逐一一致）與 `tools/smoke/Invoke-Phase2Smoke.ps1`。
- 試跑發現並修正：`Clipboard.SetImage` GDI 洩漏（改 DIB）、設定視窗淡出殘影被截入（關閉轉場動畫）、日誌比對的行尾 `\r`、嚴格模式 `$null.Count`。
- 第一次正式 30 次循環：GDI／USER／Handle／記憶體皆持平，但 4 個失敗——選取起點落在游標稍後的位置（改取事件本身的位置）、175% 下外框蓋住最後一列（改實體像素環形外框）。
- 使用者正在操作電腦時圖樣被遮住：腳本改為開始前等待閒置 20 秒。
- 新增 `ClipboardServiceTests`（DIB 格式，不寫入真實剪貼簿）；ADR-0008 更新輸入座標決策；`docs/ACCEPTANCE.md`、`README.md`、`CHANGELOG.md`、handoff 草稿。
- 修正後重跑正式 30 次循環（等待使用者閒置 164 秒後開始）：**38 項全數通過**；GDI 物件 31 個取樣恆為 38、USER 52～54、Handle 595～600、私有記憶體 286～381 MB 起伏無成長。
- 煙霧腳本的剪貼簿備份改為盡力還原所有格式（正式執行時只會還原文字，使用者原本的非文字剪貼簿內容已被覆蓋，於控制點告知）；以 PowerShell 剖析器確認語法、以實際圖片備份還原確認邏輯。
- 撰寫 `handoffs/phase-02-capture-selection.md`；`docs/ACCEPTANCE.md` 加入 Phase 2 自動化測試、煙霧驗證結果與使用者驗證步驟。

### Test Results（最終）
| Test | Expected | Actual | Status |
|------|----------|--------|--------|
| `dotnet build ScreenshotApp.sln -c Release` | 0 error, 0 warning | 0 error, 0 warning | PASS |
| `dotnet test ScreenshotApp.sln -c Release` | 全數通過 | 314 passed（20 個測試類別） | PASS |
| `tools/smoke/Invoke-Phase2Smoke.ps1 -Cycles 30` | 全數通過 | 38 PASS、0 FAIL | PASS |
| 程式碼註解掃描（編號、文件路徑） | 無違規 | 無違規 | PASS |

### Smoke Verification（P2-11）
| 執行 | 結果 | 備註 |
|------|------|------|
| 試跑 1 | 中途例外 | 嚴格模式 `$null.Count`（測試工具問題） |
| 試跑 2（3 循環） | 34 PASS／3 FAIL | 設定視窗淡出殘影、GDI 每循環 +1、日誌行尾 `\r` |
| 試跑 3（5 循環） | 36 PASS／1 FAIL | USER 物件 +1 後持平（循環數太少無法判斷） |
| 試跑 4 | 圖樣自我檢查逾時 | 使用者正在操作電腦；腳本改為先等閒置 |
| 正式 1（30 循環） | 34 PASS／4 FAIL | 選取起點延後（改取事件位置）、外框蓋住最後一列（實體像素外框）；資源全部持平 |
| 正式 2（30 循環） | 38 PASS／0 FAIL | 環境：Windows 11 Pro 10.0.26200，單一顯示器 3456×2160 @175% |

### Next
- Phase 2 已完成並合併回 `main`。等待使用者回報 MT 並授權 Phase 3。


## Session: 2026-10-18（Phase 3 圖片編輯核心）

### Current Status
- **Phase:** 3 - 圖片編輯核心 → in_progress
- **Branch:** `feature/phase-03-editor-core`

### Actions Taken
- 使用者以「使用 planning-with-files 載入專案狀態，繼續開發」授權 Phase 3（Key Question 5）；Phase 1、2 的 MT 尚未回報，維持「尚未驗證」。
- 徵詢 advisor 後調整計畫：標註改為不可變 record（Box／Segment 合併）、命令縮為五類、Image ↔ ViewDip 由 `CoordinateMapper` 擁有且控制點層在縮放容器外、狀態機可由編輯開始截圖並回到先前狀態、`ExportedVersion`／`IsDirty` 與「開啟」按鈕移至 Phase 4；更新 P3-01／02／04／05／13／15／16、追蹤矩陣、MT-07 時機與 Decisions 後重新 attest。
- P3-00：`docs/adr/0010`～`0013`（文件模型與命令、共用 AnnotationPainter、畫布縮放模型、文字輸入策略）。
- 實作：`Editor/Model`（Annotation 系列、BoxHandles、Geometry2D、TextLayout、ImageDocument）、`Editor/Commands`（UndoRedoStack、五個命令）、`Editor/Rendering/AnnotationPainter`、`Export/ImageRenderer`、`Export/PngFileService`、`CoordinateMapper` 縮放換算與 `ImageViewTransform`、`Editor/EditorSession`（工具邏輯）、`EditorZoom`、`Settings/StyleChoices`（設定視窗改用共用選項）、`AnnotationLayer`、`HandleLayer`、`EditorWindow`、`AppStateMachine` Editing、`AppController` 開啟／重用編輯器與預設樣式回寫、第二個執行個體在編輯中喚醒編輯視窗、66 個介面文字（移除 Phase 2 暫用的剪貼簿通知文字）。
- 委派兩個 subagent（各自 worktree，避免單一專案並行建置衝突）撰寫單元測試：模型／命令／工作階段／文字；合成器／裁切／座標縮放／縮放階段／狀態機／PNG。

### Test Results
| Test | Expected | Actual | Status |
|------|----------|--------|--------|
| `dotnet build ScreenshotApp.sln -c Release`（編輯器程式碼） | 0 error, 0 warning | 0 error, 0 warning | PASS |
| `dotnet test`（既有 314 個，含資源鍵與 XAML 硬編碼掃描） | 全數通過 | 314 passed | PASS |
| subagent（合成器／裁切／縮放／狀態機／PNG）：`dotnet test --filter` 6 個類別 | 全數通過、測試專案 0 warning | 227 passed；回報產品 2 個 CS9124 警告與橘底白字對比偏低 | PASS |
| 合併後 `dotnet build` / `dotnet test` | 0 warning / 全數通過 | 0 warning / 467 passed | PASS |
| subagent（模型／命令／工作階段／文字）：5 個類別 | 全數通過 | 300/307；7 個失敗皆為同一產品缺陷：裁切右、下控制點拖到圖片邊緣差 1 像素 | 發現缺陷 |
| 修正控制點位移後 `dotnet test` | 全數通過 | 773/774；`SafeHandleTests` 在完整執行時固定失敗（單獨執行通過） | FAIL → 修正 |
| GDI 量測改為等數量穩定後 `dotnet test` ×5 | 全數通過 | 774 passed ×5 | PASS |

### Actions Taken（續：實機煙霧驗證與修正）
- 撰寫 `tools/smoke/Invoke-Phase3Smoke.ps1`。第一次試跑（2 循環）：編輯器開啟、100% 畫布與已知圖樣逐像素一致、3px 矩形筆劃 1764/1764 純紅且在範圍內側皆通過；之後使用者回到電腦前，QuiteRSS 與瀏覽器來到前景，按鍵因前景檢查未送出，但滑鼠點擊／拖曳／Ctrl+滾輪可能落在那些視窗（已告知使用者）；以螢幕擷取保存的證據截到其他程式，已刪除。
- 腳本強化：每次模擬輸入前偵測使用者輸入（`GetLastInputInfo` 對照腳本最後一次模擬輸入時間），滑鼠操作前確認前景與目標位置都是 ScreenshotApp 的視窗，偵測到即停止且不再送出任何輸入；編輯視窗改以 `PrintWindow` 擷取；剪貼簿在驗證期間被其他程式寫入時不還原；預設閒置要求 60 秒。
- 審查修正：裁切範圍為整張圖時拖曳只會被當成平移、無法框選 → 改為「控制點調整、其他位置框選新範圍」，移除平移裁切範圍；雙擊步驟編號時編號欄位可能尚未啟用 → 延後移入焦點；編號欄位 Esc 會套用輸入的數字 → 先還原；`MoveDrag`／`ResizeDrag` 的 CS9124；步驟編號亮色門檻 160 → 150（橘底改配黑字）。
- 第三次煙霧驗證（使用者閒置後開始）：前 12 項通過（含 Ctrl+Z／Ctrl+Y），之後前景變成 Windows Terminal 而沒有使用者輸入，腳本依保護規則停止；研判是同時執行的 subagent 開啟主控台視窗，改為 subagent 結束後再跑。PrintWindow 證據只含編輯視窗。
- 合併 subagent 測試後修正：控制點拖曳位移改以「連續座標差四捨五入」計算（原本以捨去後且限制在圖片內的像素相減，在格線上按下會多算一個像素，右、下邊到不了圖片邊緣）；`ResizeDrag` 同步修正。測試輔助 `GdiObjectCount` 改為等連續兩次讀數相同才回傳（WPF 繪製資源在終結器後才由繪製執行緒刪除，干擾以整個處理程序計數的量測）。
- 移除兩個 subagent 的暫時 worktree 與分支（測試檔已合併；分支沒有額外 commit）。

### Actions Taken（續：收尾）
- 第五次煙霧驗證在第一個模擬輸入前偵測到使用者操作而停止（使用者正在使用電腦，保護機制如預期運作）。
- 使用者以「完成 phase 3」要求收尾：取消排隊中的煙霧驗證；P3-15 標示部分完成，未執行的項目在 `docs/ACCEPTANCE.md` 與 handoff 標示「尚未驗證」，腳本保留供電腦閒置時補完。
- 證據 `docs/evidence/phase-03/`（第四次執行的結果清單與兩張 PrintWindow 截圖）；`docs/ACCEPTANCE.md` 加入 Phase 3 自動化測試對照（AT-01 縮放平移、AT-03～AT-06、AT-07 前置）與煙霧驗證結果；`CHANGELOG.md`；handoff `handoffs/phase-03-editor-core.md`。

### Test Results（最終）
| Test | Expected | Actual | Status |
|------|----------|--------|--------|
| `dotnet build ScreenshotApp.sln -c Release` | 0 error, 0 warning | 0 error, 0 warning | PASS |
| `dotnet test ScreenshotApp.sln -c Release` | 全數通過 | 774 passed（連續 5 次） | PASS |
| `tools/smoke/Invoke-Phase3Smoke.ps1` | 全數通過 | 前 12 項 PASS；之後前景被其他視窗取走而停止，後半段尚未驗證 | 部分完成 |

### Smoke Verification（P3-15）
| 執行 | 結果 | 備註 |
|------|------|------|
| 試跑 1（2 循環） | 16 PASS／15 FAIL | 使用者回到電腦前；前 9 項通過後，滑鼠操作落到其他程式（保護不足，已強化） |
| 2 | 未開始 | 等待閒置時因裁切設計修正而中止 |
| 3 | 12 PASS 後停止 | 前景變成 Windows Terminal（同時有 subagent 在執行指令） |
| 4 | 12 PASS 後停止 | 前景變成 Windows Terminal（同時有指令在執行）；加入前景恢復 |
| 5 | 開始前停止 | 偵測到使用者操作 |
| 6 | 取消 | 使用者要求收尾 |

### Next
- Phase 3 已完成並合併回 `main`。等待使用者回報 MT 並授權 Phase 4；電腦閒置時可補跑 Phase 3 煙霧驗證後半段。


## Session: 2026-10-18（MT 回報、十字游標修正、Phase 4 授權）

### Current Status
- **Phase:** Phase 3 complete；Phase 4 已授權、尚未開始（Key Question 7）
- **Branch:** `fix/crosshair-contrast`（自 `main` 建立）

### Actions Taken
- 使用者回覆「繼續開發，完成 MT-01, MT-02, MT-03, MT-06, MT-08 驗證，問題：若擷取畫面時，背景全白，則十字準星看不清」。「繼續開發」視為授權 Phase 4；五項 MT 是否全數通過待確認後再登錄。
- 根因（腳本實測）：系統十字游標 165 個墨水像素全為反相；以 `DrawIconEx` 畫在灰 127／128 上最大對比 1（白 255、黑 0 上為 255）。覆蓋層 50% 暗化把白色畫面變成灰 127。
- 徵詢 advisor 後的做法：修正做成獨立單元（分支、測試），完成後一次詢問 MT 結果與是否先 commit 合併；不在使用者使用電腦時跑煙霧驗證。
- 先寫 `CrosshairCursorTests`（紅燈：型別不存在），再實作 `Capture/CrosshairCursor`（`CreateCursor` 黑芯白邊單色游標、`GetSystemMetricsForDpi(SM_CXCURSOR)` 尺寸、每種尺寸快取）、`SafeCursorHandle`、`NativeMethods` 三個 API；覆蓋層移除 XAML 的 `Cursor="Cross"`，改在 `HwndSource` 建立後依 DPI 設定。測試輔助加入 `UserObjectCount`（與 GDI 量測共用穩定讀數迴圈）。
- `Invoke-Phase3Smoke.ps1` 加入 `Test-OverlayCursor`：覆蓋層出現後把游標移到覆蓋層上，以 `GetCursorInfo` + `GetIconInfo` 檢查顯示中的游標沒有反相像素、尺寸等於系統游標、熱點在中心（腳本語法剖析與內嵌 C# 編譯通過；本次未執行，使用者正在使用電腦）。
- 對照圖 `docs/evidence/phase-02/crosshair-contrast.png`（PMv2 情境下 `DrawIconEx` 繪製 48 像素游標、放大 4 倍）：系統十字在灰底消失，新十字在白、灰、黑底都清楚。
- 文件：`CHANGELOG.md`（Fixed）、`docs/KNOWN_LIMITATIONS.md`（自繪游標不套用指標色彩設定；指標大小是否套用尚未驗證）、`docs/ACCEPTANCE.md`（測試類別、煙霧檢查項目、使用者回報的問題）。

### Test Results
| Test | Expected | Actual | Status |
|------|----------|--------|--------|
| `dotnet build ScreenshotApp.Tests -c Debug`（實作前） | 編譯失敗（紅燈） | CS0246 找不到 `CursorShape`／`SafeCursorHandle` | PASS（紅燈） |
| `dotnet test --filter CrosshairCursorTests -c Debug` | 全數通過 | 51 passed | PASS |
| `dotnet build ScreenshotApp.sln -c Debug` | 0 error, 0 warning | 0 error, 0 warning | PASS |
| `dotnet test ScreenshotApp.sln -c Debug` | 全數通過 | 825 passed | PASS |
| `dotnet build -c Release` | 0 error | 失敗：使用者執行中的 ScreenshotApp（PID 49264）占用 `bin\Release\ScreenshotApp.exe` | 待使用者結束程式 |
| `dotnet build ScreenshotApp.sln -c Release`（使用者結束程式後） | 0 error, 0 warning | 0 error, 0 warning | PASS |
| `dotnet test ScreenshotApp.sln -c Release` | 全數通過 | 825 passed | PASS |

### Actions Taken（續）
- 一次詢問使用者三件事，皆採建議：五項 MT 全數通過、十字游標是唯一問題（登錄於 `docs/ACCEPTANCE.md`，MT-08 註明以 Phase 3 版本驗證、Phase 4 加入 PNG 後再確認）；修正現在單獨 commit 並合併回 `main`；使用者自行從系統匣結束程式。
- commit `phase-02: …`（分支 `fix/crosshair-contrast`）並 fast-forward 合併回 `main`；接著從 `main` 建立 `feature/phase-04-image-io` 開始 Phase 4。

### Actions Taken（Phase 4）
- 從 `main` 建立 `feature/phase-04-image-io`，Phase 4 → `in_progress`。讀 Phase 3 handoff 的風險、後續工作與未驗證項目。
- 徵詢 advisor 後定案：剪貼簿沿用 .NET 內建重試（取代原計畫的外層 5 次）；「未匯出」以編輯歷史的狀態版本判斷、剛截取或剛開啟的圖片沒有未匯出變更；開啟的圖片以白色背景合成透明度；結束程式前詢問（`Shutdown` 關閉視窗時不能取消）；登出或關機時攔截。
- ADR-0014（剪貼簿格式與重試）、ADR-0015（檔案寫入與背景編碼）、ADR-0016（未匯出變更保護）。
- TDD：`UndoRedoStack.Version`、`ImageDocument.ExportedVersion`／`IsDirty`／`MarkExported`（`UndoRedoStackTests` +5、`DirtyTrackingTests` 7）；`ClipboardPayload`（DIB＋PNG，可在背景執行緒產生）、`ClipboardService.TrySetImage` 可注入、`DescribeError`（`ClipboardServiceTests` +9，以 COM `IDataObject.GetData` 驗證兩種格式轉成 HGLOBAL，不碰使用者剪貼簿）；`PngFileService.Encode`（剪貼簿與存檔共用）。
- 編輯器：複製與另存改為背景編碼、進行中停用兩個命令、成功才以合成當時的版本標記匯出、另存記住資料夾；`ConfirmDiscardAsync`、`HasUnexportedChanges`、`CloseWithoutConfirmation`、`OnClosing` 攔截；「開啟」按鈕與 `Ctrl+O`；`UnsavedChangesDialog`（WPF，預設為另存、Esc 取消）。
- 控制器：新截圖前、開啟圖片前、關閉編輯器、系統匣結束、登出或關機的詢問；詢問或開檔期間、編輯器背景匯出中、編輯器開著自己的對話框時忽略新的截圖／開啟／結束要求；系統匣「開啟圖片」；開檔在背景執行緒解碼。
- 委派 subagent（worktree）實作 `ImageFileLoader` 與 AT-10 測試（白名單：載入器、測試、`Strings.resx` 結尾新增 `ImageLoad_` 鍵）。
- `ImageRendererTests` 加入同一組編輯在 25%／100%／400% 畫布縮放下輸出逐位元組相同（AT-07）；`PngFileServiceTests` 加入執行緒集區存檔。
- 煙霧腳本：新增 `tools/smoke/Invoke-Phase4Smoke.ps1`（以產生器複製 Phase 3 的基礎設施，加入 UI Automation 按鈕、檔案對話框、剪貼簿兩種格式、占用模擬、未匯出詢問各路徑；語法與內嵌 C# 編譯通過，尚未執行）；`Invoke-Phase2Smoke.ps1` 改為從編輯器以「複製」按鈕取得截圖結果（P4-11，語法通過，尚未執行）。
- 文件：README、KNOWN_LIMITATIONS、CHANGELOG、ACCEPTANCE（Phase 4 驗證步驟）。
- 使用者仍在使用電腦（閒置 1 秒），煙霧驗證延後。

### Actions Taken（Phase 4 整合與煙霧驗證）
- subagent 完成 `ImageFileLoader` 與 `ImageFileLoaderTests`（26 個；以約 3.6 萬個截斷或竄改的變體實測例外型別；以記憶體峰值證明讀標頭時不解碼像素）。合併到主工作目錄（`Strings.resx` 以修補檔套用，與主 agent 新增的鍵不重疊），移除暫時 worktree 與分支。
- 修正 subagent 測試類別摘要（實際在背景執行緒開檔），新增執行緒集區載入與拒絕存取兩個測試（28 個）；已知限制加入「只缺後段影像資料的檔案仍可開啟（WIC 以黑色補足）」。
- AT-07 新測試第一次失敗：文字提交後仍為選取狀態，`SetColor` 套用到文字；測試先取消選取後通過（測試寫法問題）。
- 自我審查修正：`RequestCloseEditor` 也檢查忙碌狀態；背景匯出進行中視為忙碌；`OpenFileDialog` 依有無擁有者分別呼叫；`IsBusy` 移至私有成員區。
- 以離屏繪製檢查未匯出變更對話框的版面（四個按鈕、存取鍵）。

### Test Results（Phase 4）
| Test | Expected | Actual | Status |
|------|----------|--------|--------|
| `dotnet build ScreenshotApp.sln -c Release` | 0 error, 0 warning | 0 error, 0 warning | PASS |
| `dotnet test ScreenshotApp.sln -c Release` | 全數通過 | 876 passed | PASS |

### Smoke Verification（P4-08，使用者閒置時以前景方式執行）
| 執行 | 結果 | 備註 |
|------|------|------|
| 1 | 3 PASS 後腳本例外 | `Invoke-Button` 的 `$condition` 與 `Wait-Until` 參數同名；腳本寫入剪貼簿後未更新序號，結束時沒有還原使用者原本的剪貼簿（已修正三支腳本並記錄於記憶） |
| 2 | 9 PASS／8 FAIL | 剪貼簿兩種格式、另存 PNG 與剪貼簿逐位元組相同、記住儲存位置、剪貼簿占用（約 1.2 秒）通過；另存到唯讀檔時存檔對話框一直開著，後續步驟被忙碌保護擋下 |
| 3 | 10 PASS／3 FAIL | 確認唯讀檔由 Windows 存檔對話框自行拒絕（腳本改為記錄並回答提示、取消存檔，通過）；新截圖前的詢問沒有出現（原因未確定），接著偵測到使用者操作而停止 |
| 4 | 22 PASS／0 FAIL | 加入診斷與錯誤訊息框的關閉處理後全數通過 |
| 5 | 22 PASS／0 FAIL | 腳本改為不送出模擬按鍵（UI Automation 選工具、右鍵取消覆蓋層）後，以含合成器修正的 Release 建置重跑 |

### Actions Taken（Phase 4 煙霧驗證之後的診斷與收尾）
- Phase 3 腳本強化（UI Automation 選工具、填文字、按「複製」，縮放比較前確認狀態列百分比）後重跑：33 PASS／4 FAIL（Delete、縮放 400%／25%、裁切內容）。更正先前縮放與文字檢查的通過紀錄（當時動作沒有發生）。
- 兩支診斷腳本：Ctrl+滾輪需先以 SendInput 按住 Ctrl 再 `PostMessage(WM_MOUSEWHEEL)`（產品正常）；Delete 在非延伸鍵與延伸鍵下都沒有刪除選取的箭頭，同樣送法的 Ctrl+Z、Ctrl+Y、Enter 有效，程式碼中找不到攔截 Delete 的處理。
- 使用者指示「Delete 鍵失敗原因，整理目前資訊後，先列為待辦，先繼續處理後續問題」：調查紀錄寫入 `findings.md`，列為 P5-11（Key Question 8）；advisor 指出 Delete 失敗後的 Ctrl+Z 也沒有作用，可能是點選沒有移動後仍處於拖曳狀態，已補入 findings。
- 使用者指示「後續測試失敗，修正超過 3 次無法改好，則列入待辦，不要拖累進度」。
- 裁切內容差異：暫時的實驗測試重現（Segoe UI 14 在部分裁切原點有 66～91 個像素不同，32 級字沒有）→ 新增 `Render_CroppedOutput_EqualsSameRegionOfUncroppedOutput`（紅燈 4 個）→ `ImageRenderer` 改為整張合成後再取出裁切範圍（第 1 次即綠燈）；刪除實驗檔；ADR-0011 修訂第 4 點；已知限制加入匯出成本。
- Phase 3 腳本：Ctrl+滾輪改為逐格「按住 Ctrl → 送 WM_MOUSEWHEEL → 一定放開 Ctrl」直到狀態列顯示目標百分比；「刪除後 Ctrl+Z 恢復箭頭」改為箭頭確實被刪除才判定。
- 電腦閒置後依序執行三支煙霧腳本（期間不執行其他指令），證據複製到 `docs/evidence/`（Phase 2 的螢幕擷取截圖沿用首次執行的檔案，只更新文字結果）。
- 文件：ACCEPTANCE（測試總數、AT-07、三個階段的煙霧結果、MT-07 註記、請使用者以實體鍵盤確認 Delete）、CHANGELOG、KNOWN_LIMITATIONS、handoff `handoffs/phase-04-image-io.md` 完成；Phase 4 → complete。

### Test Results（Phase 4 收尾）
| Test | Expected | Actual | Status |
|------|----------|--------|--------|
| `dotnet test --filter Render_CroppedOutput_EqualsSameRegionOfUncroppedOutput -c Debug`（修正前） | 失敗（紅燈） | 4 failed（118～136 個像素不同） | PASS（紅燈） |
| `dotnet test`（ImageRenderer、Clipboard、PngFileService、DirtyTracking，Debug，修正後） | 全數通過 | 68 passed | PASS |
| `dotnet build ScreenshotApp.sln -c Release` | 0 error, 0 warning | 0 error, 0 warning | PASS |
| `dotnet test ScreenshotApp.sln -c Release` | 全數通過 | 880 passed | PASS |
| `pwsh -File tools/smoke/Invoke-Phase3Smoke.ps1` | 全數通過 | 35 PASS／2 FAIL（Delete 與其復原，待辦 P5-11） | PARTIAL |
| `pwsh -File tools/smoke/Invoke-Phase4Smoke.ps1`（強化後） | 全數通過 | 22 PASS／0 FAIL | PASS |
| `pwsh -File tools/smoke/Invoke-Phase2Smoke.ps1`（P4-11 更新後） | 全數通過 | 38 PASS／0 FAIL | PASS |

## Session: 2026-10-18（Phase 5 完整驗證與發佈）

### Current Status
- **Phase:** Phase 5 in_progress（Key Question 9）
- **Branch:** `feature/phase-05-release`（自 `main` 40762cc 建立）

### Actions Taken
- 以 planning-with-files 載入狀態：Phase 4 complete、停在控制點；使用者回覆「使用 planning-with-files 載入專案狀態，繼續」，與先前授權句式相同，視為授權 Phase 5。控制點問題沒有回報，維持「尚未驗證」，於本輪結尾一次提出。
- 本專案沒有 CodeGraph 與 codebase-memory 索引，直接讀程式碼。advisor 逾時，依自己的判斷進行。
- P5-11 閱讀結果：`EditorSession.PointerUp` 不論有無移動都會清除拖曳；Phase 3 煙霧腳本中箭頭由 (300,60) 拖到 (450,180)，點選位置 (375,120) 正好在線上；同一次執行中第 7 步以選取工具拖曳矩形成功（若第 6 步的拖曳未結束，第 7 步按下會被忙碌保護擋下），與「點選後一直處於拖曳狀態」的推測不符，需要實證。
- P5-11 判別測試 `SelectTool_ClickOnAnnotationWithoutMoving_EndsDragSoDeleteAndUndoWork`（依煙霧腳本的操作順序）直接通過：session 層點選不移動後不在拖曳中、Delete 與復原有效，問題在視窗輸入層或模擬輸入。
- P5-11 診斷準備：分離的 git worktree（scratchpad）中暫時加上按鍵、滑鼠、Delete 可執行判斷的狀態紀錄（寫入視窗的 UI Automation HelpText），建置成功；由 Phase 3 腳本產生只跑到 Delete 的診斷腳本（SendInput、延伸鍵、PostMessage、右鍵選單四種刪除方式），語法與內嵌 C# 編譯通過。使用者正在使用電腦，背景等待閒置 90 秒。
- P5-05 試發佈（scratchpad）：資料夾版 473 檔／173 MB（12 種非繁中語系 204 檔／17 MB）；單檔版預設在 exe 旁留 5 個 WPF 原生 DLL，加 `IncludeNativeLibrariesForSelfExtract` 才是單一檔案（173 MB）。
- P5-00：ADR-0017（資料夾版為主、單檔版經煙霧驗證後才提供、只保留 zh-Hant 附屬組件、Version 1.0.0、授權檔、不簽章）；csproj 加入 `Version`、`SatelliteResourceLanguages`；`tools/Publish-Release.ps1`；`THIRD-PARTY-NOTICES.md`。
- P5-01 靜態檢查：程式碼中的中文字串只有故障日誌內容與程式錯誤的例外訊息；各視窗存取鍵不重複、按鈕皆有工具提示。
- P5-02：存檔、開檔讀取、設定檔讀寫與登入啟動設定失敗時原本直接顯示 .NET 例外訊息（拒絕存取、找不到路徑、檔案被占用等為英文）→ 新增 `Resources/ErrorText`（TDD：`ErrorTextTests` 13 個先紅後綠，含實際觸發的唯讀、資料夾不存在、檔案被占用），5 個呼叫點改用；`ImageFileLoaderTests`、`SettingsStoreTests` 的斷言改為繁中原因。
- P5-04：`docs/USER_GUIDE.md`（新）、README 改寫（操作細節移到使用說明，加入發佈與文件索引）、ACCEPTANCE 中失效的 README 錨點改指向使用說明；`docs/ARCHITECTURE.md` 委派 subagent（只用讀寫檔工具）。
- P5-05：`tools/Publish-Release.ps1` 實際執行成功（約 23 秒）：資料夾版 275 檔／155.9 MB、ZIP 67.3 MB；單檔版 8 檔／149.3 MB、ZIP 63.5 MB；ZIP 以發佈資料夾為根目錄；`publish/` 已被 gitignore 排除。發佈版的煙霧驗證待電腦閒置。
- 腳本：Phase 4 腳本加入 `-Cycles`（預設 30）的完整循環（取消、截圖、畫矩形、複製、另存、關閉）與資源取樣、截圖延遲，以及 FHD 截圖與開啟 3840×2160 PNG 的開啟／複製／另存延遲；移除未使用的 `ControlWheel`；Phase 3 腳本加入 Tab 鍵盤巡覽檢查（P5-01 實機部分）。三者語法與內嵌 C# 編譯通過，尚未實機執行。
- ACCEPTANCE：P5-02 錯誤恢復總檢表、Phase 5 使用者驗證步驟（發佈版、單檔版、MT-11、尚未回報的 MT）；Phase 4 腳本執行時間說明更新。KNOWN_LIMITATIONS：開頭改為 v1.0.0 清單並加入「發佈與執行環境」。
- 完整測試（Release）：894 passed／0 failed（6 秒）。
- 使用者整段時間都在使用電腦（閒置 0 秒），GUI 項目等待閒置；本輪結尾回報並提出問題。

### P5-11 診斷與 P5-07 發佈版煙霧驗證（使用者閒置時以前景方式依序執行，期間不執行其他指令）
- P5-11 診斷（診斷版建置）：按下 Delete 時焦點在畫布、沒有拖曳、選取為箭頭 → `CanDelete=True` → `DeleteExecuted result=True`，之後選取為 null；箭頭範圍「偏紅」像素仍為 560。計算確認這 560 個像素全部來自已知圖樣（R = x 低 8 位元 ≥ 200 的 56 欄 × G = y 低 8 位元 ≤ 60 的 10 列），箭頭其實每次都被刪除；先前「Delete 後 Ctrl+Z 沒有移除文字」是 Ctrl+Z 正確地復原了刪除。結論：不是產品缺陷，是煙霧腳本的判定錯誤。同樣的缺陷也讓文字的兩項檢查可能誤判通過（範圍內各有 680／952 個圖樣像素）。
- Phase 3 腳本四項檢查改為「與原圖不同的像素」：箭頭出現（> 300）、文字出現在畫布與複製結果（> 100）、刪除後箭頭範圍恢復原圖（= 0）。
- 資料夾版 Phase 1 31／31、Phase 2 38／38、Phase 3 38／38（Delete 刪除 0 個不同像素、Ctrl+Z 恢復、Tab 巡覽經過全部可用控制項）全數通過。
- Phase 4 第 1 次：原 22 項通過，第 13 步一開始停止（右鍵取消前前景仍是編輯器關閉後被啟用的其他視窗「Window Class」，覆蓋層顯示與取得前景之間有時間差）→ 加入 `Wait-OverlayForeground`。第 2 次：30 次循環成功，GDI 判定失敗（前 5 次最大 132、後 5 次最大 134；逐次 126～134）。第 3 次（每次存檔驗證後刪除檔案，存檔對話框列出的內容固定）：GDI 126～132、19／30 次為 128，仍以最大值判定失敗。分析：+4 的尖峰都在下一次回落，量測結束後回到 126，基準沒有上升 → 改以前後各 10 次的中位數判定（說明列出最小、最大與線性趨勢）。第 4 次（最終）：25／25 通過，GDI 中位數 128→128（126～132，+0.00／次）、USER 92→93～94、Handle −0.20／次。延遲：600×400 按快捷鍵到覆蓋層中位數 154 ms、放開滑鼠到編輯器 91 ms；1920×1080 覆蓋層 155 ms、編輯器 93 ms、複製 94 ms、存檔 77 ms；開啟 3840×2160 PNG 285 ms、複製 342 ms、存檔 311 ms（各 3 次）。
- 單檔版 Phase 1 31／31、Phase 2 38／38；原生程式庫解壓到 `%TEMP%\.net\ScreenshotApp\<識別碼>\`（5 個 DLL），與文件一致。
- 證據（結果文字檔、CSV、P5-11 診斷紀錄）複製到 `docs/evidence/phase-05/`；診斷用 worktree 已移除。

### Actions Taken（Phase 5 收尾）
- 審閱 subagent 的 `docs/ARCHITECTURE.md`（392 行）：抽查狀態機、執行緒模型、合成與發佈設定與程式碼一致；修正使用說明連結、文字輸入中快捷鍵的機制描述、第 14 節腳本範圍。依 subagent 的比對，ADR-0002／0009／0010／0016 補上修訂說明；字型沿用範圍（只在編輯視窗開著時）更正程式註解與已知限制。
- ACCEPTANCE：測試總數、Phase 3 Delete 失敗的查明經過、Phase 5 發佈版煙霧驗證與資源、延遲量測、MT-10 狀態；CHANGELOG 定為 `[1.0.0] - 2026-10-18`；handoff `handoffs/phase-05-release.md`；Phase 5 → complete（6／6）。

### Test Results（Phase 5 收尾）
| Test | Expected | Actual | Status |
|------|----------|--------|--------|
| `dotnet test --filter SelectTool_ClickOnAnnotationWithoutMoving`（P5-11 判別） | 失敗則為工作階段問題 | 1 passed（工作階段正常） | PASS（判別） |
| `dotnet build ScreenshotApp.Tests -c Debug`（ErrorText 實作前） | 編譯失敗（紅燈） | CS0103 找不到 `ErrorText` | PASS（紅燈） |
| `dotnet test --filter ErrorTextTests` | 全數通過 | 13 passed | PASS |
| `dotnet build ScreenshotApp.sln -c Release` | 0 error, 0 warning | 0 error, 0 warning | PASS |
| `dotnet test ScreenshotApp.sln -c Release --no-build` | 全數通過 | 894 passed（4 秒） | PASS |
| `pwsh -File tools/Publish-Release.ps1` | 兩種版本與 ZIP | 資料夾版 275 檔／155.9 MB（ZIP 67.3 MB）、單檔版 149.3 MB（ZIP 63.5 MB） | PASS |
| 發佈版煙霧（資料夾版 Phase 1／2／3／4） | 全數通過 | 31／38／38／25 | PASS |
| 發佈版煙霧（單檔版 Phase 1／2） | 全數通過 | 31／38 | PASS |

## Session: 2026-10-18（Phase 6 AGENTS.md）

### Actions Taken
- 使用者回覆「都依照預設，並產生 AGENTS.md，並提交」：未修改的截圖維持不詢問、MT-11 標示「尚未驗證」、不推送 upstream（Key Question 10）。新增 Phase 6，分支 `feature/phase-06-agents-md`（改動超過 3 個檔案）。
- 依 writing-for-agents 的原則撰寫 `AGENTS.md`（60 行）：只收錄看不出來的慣例、原因與陷阱（介面文字與註解規則、座標與文件模型、單一繪製碼與整張合成、執行緒與 GDI、ErrorText、LF；測試隔離；煙霧腳本的閒置與前景規則、以與原圖不同的像素判定、剪貼簿序號；發佈在 commit 之後、Release 建置被鎖、單一執行個體；Git 分支、commit 格式、不主動推送、attest），其餘以指向規格、ADR、ARCHITECTURE 與 findings 處理。
- 逐條對照：`IEditCommand`、`GdiObjectCountCollection`（`DisableParallelization = true`）、`StaRunner`、`StringsResourceTests` 的三項檢查、各煙霧腳本的保護（Phase 1 以 SendKeys 送鍵且不等待閒置、Phase 2 只在開始前等待閒置、Phase 3、4 每次模擬輸入前檢查使用者操作）。
- CHANGELOG `[Unreleased]` 記錄 `AGENTS.md`；Phase 6 → complete；`v1.0.0` 標籤維持原位。

## Session: 2026-10-19（Phase 7 右鍵選單與裁切後填滿畫布）

### Current Status
- **Phase:** Phase 7 in_progress（Key Question 11）
- **Branch:** `feature/phase-07-context-menu-crop-fill`（自 `main` 51cd11c 建立）

### Actions Taken
- 使用者要求先深度思考右鍵選單內容與「裁切後以裁切範圍填滿畫布、可復原」，讓使用者選擇後再實作。讀編輯器程式碼後提出方案：目前右鍵只在標註上開啟層序與刪除選單；裁切已是非破壞性且可復原，沒有填滿是因為符合視窗上限 100%、手動縮放後套用裁切不重算。「裁切後文件改成裁切後的圖片」違反非破壞性裁切的規格，不列為選項。
- 使用者回覆「都依照建議，開始實作」。新增 Phase 7（分支，改動超過 3 個檔案），計畫重新 attest。這次沒有要求提交，依使用者全域指示不 commit。
- 本專案沒有 CodeGraph 與 codebase-memory 索引，直接讀程式碼。advisor 檢視實作計畫：填滿的觸發條件以「離開裁切工具時顯示的裁切範圍與上次不同」涵蓋套用、復原、重做與還原；資源中選單的具名項目要以執行期測試確認；右鍵空白處比照左鍵取消選取；煙霧驗證只在 Phase 3 第 9 步加縮放斷言，不另寫腳本。
- P7-01 規格：修訂紀錄、FR-14／16／18／20／21／22／25、新增 FR-36、AT-03／04／05。
- P7-02／P7-03 先寫測試（`EditorSessionTests` 22、`EditCommandTests` 1、`EditorZoomTests` 6），建置失敗（CS0246／CS1061，紅燈）後實作 `CompositeCommand`、`EditorSession` 六個操作與三個可否執行的屬性、`EditorZoom.Fit(..., enlarge)`，217 個相關測試通過。
- P7-04：XAML 三份選單、`PrepareContextMenu`、16 個 Click 處理、`Ctrl+D`、`Strings.resx` 24 個鍵（選單文字沿用工具列的復原、重做、複製、另存）。資源中的 `x:Name` 沒有產生欄位（CS0103），改以 `FindLogicalNode` 依名稱查找。`EditorWindowTests` 10 個（未啟動的控制器、建立但不顯示的視窗）通過，確認每個名稱在執行期都找得到。
- 自我檢查發現填滿觸發的邊界情況（裁切工具中復原再離開時顯示範圍不變、紀錄過時，之後重做不填滿），把檢查移到提早返回之前；版面需要顯示視窗才會計算，無法以單元測試重現。
- Phase 3 煙霧腳本第 9 步：裁切後縮放三項、右鍵畫布選單「還原完整圖片」兩項（新增 `RightClick` 與 `Invoke-ContextMenuItem`）；語法剖析與內嵌 C# 以 scratchpad 的檢查腳本編譯通過。
- P7-05 文件：ADR-0010、ADR-0012 修訂；ARCHITECTURE（六類命令、右鍵選單、縮放規則、測試對應）、USER_GUIDE（裁切後填滿、右鍵選單、`Ctrl+D`）、KNOWN_LIMITATIONS（400% 上限、復原不回到裁切前的檢視、再製方向）、ACCEPTANCE（測試數、Phase 7 煙霧檢查、使用者驗證步驟）、CHANGELOG。
- 實機煙霧驗證依 AGENTS.md 等使用者閒置：背景偵測 90 秒閒置（上限 120 分鐘）。
- 規格、使用說明與 CHANGELOG 原寫「無法執行的項目停用」，但既有的層序項目在最上層或最下層時仍可按（沒有效果）；改為列出實際會停用的項目，不改既有項目的行為。
- advisor 審查（程式碼沒有發現缺陷）後修正煙霧腳本：右鍵位置改為視窗水平中央、下緣往上 70 個實體像素（原本的視窗中央在 175% 時離矩形下緣只有幾個圖片像素，容易誤點成標註選單）；UI Automation 查詢暫時視窗的例外視為不是選單。重新剖析與編譯通過。
- `handoffs/phase-07-context-menu-crop-fill.md`；Phase 7 → complete（煙霧實機執行等待閒置）、重新 attest、ledger 加入 phase_complete。

### Test Results（Phase 7）
| Test | Expected | Actual | Status |
|------|----------|--------|--------|
| `dotnet build ScreenshotApp.Tests -c Debug`（實作前） | 編譯失敗（紅燈） | CS0246 `CompositeCommand`、CS1061 `ResetCrop` 等 | PASS（紅燈） |
| `dotnet test --filter EditorSessionTests\|EditCommandTests\|EditorZoomTests\|CropCommandTests\|UndoRedoStackTests`（Debug） | 全數通過 | 217 passed | PASS |
| `dotnet build ScreenshotApp -c Debug`（命名項目改用欄位時） | 0 error | CS0103 ×9（資源中的名稱沒有欄位） | 改以名稱查找 |
| `dotnet test --filter EditorWindowTests\|StringsResourceTests`（Debug） | 全數通過 | 17 passed | PASS |
| Phase 3 煙霧腳本：語法剖析、內嵌 C# 編譯 | 通過 | PARSE OK、C# COMPILE OK | PASS |
| `dotnet build ScreenshotApp.sln -c Release` | 0 warning、0 error | 0 warning、0 error | PASS |
| `dotnet test ScreenshotApp.sln -c Release --no-build` | 全數通過 | 933 passed（894 + 39） | PASS |

## Session: 2026-10-19（Phase 7 收尾：v1.1.0 發佈、提交與合併）

### Actions Taken
- 使用者指示「更新文件，對齊專案狀態，新增版號，建置最終執行版本，提交後合併回 main」（Key Question 12）。Phase 7 加入 P7-08～P7-10 並改回 in_progress，計畫重新 attest。
- 背景閒置偵測（09:05 起）到 09:36 仍沒有出現 90 秒閒置，使用者一直在使用電腦；煙霧驗證不阻擋使用者明確要求的提交與發佈，標示「尚未驗證」。
- P7-08：版本 1.1.0（新增相容的功能，語意化版本遞增次版號）：專案檔 `Version`、`app.manifest` 的 `assemblyIdentity`；CHANGELOG `[Unreleased]` 改為 `[1.1.0] - 2026-10-19`，保留空的 `[Unreleased]`；README（目前版本與發佈檔名）、USER_GUIDE（發佈檔名、從舊版更新）、KNOWN_LIMITATIONS（v1.1.0）；ACCEPTANCE（v1.1.0 發佈版、使用者驗證步驟改用 1.1.0 的 ZIP、Phase 5 步驟註明改用新版）；AGENTS.md（版本範例改為不綁定版號、發佈新版本的步驟、資源中具名元素沒有欄位、未顯示的視窗不計算版面）；ADR-0017 第 4 點修訂（版本遞增方式）；規格修訂紀錄標示 v1.1.0。以 `1.0.0` 搜尋，剩下的都是 v1.0.0 的歷史紀錄。
- ADR-0017 第 2 點：單檔版要以該版本的發佈檔通過系統匣、快捷鍵、截圖與複製的煙霧驗證後才提供；本次沒有閒置可驗證，ACCEPTANCE 與 handoff 註明驗證前優先使用資料夾版。
- P7-09：Release 建置 0 警告、933 passed；執行檔檔案版本 1.1.0.0。commit 前試發佈：資料夾版 275 個檔案、155.9 MB（ZIP 67.3 MB），單檔版 8 個檔案、149.4 MB（ZIP 63.5 MB），23 秒。
- 以 Python 位元組寫入 progress.md 時開檔失敗（Errno 22，檔案可能正被其他程序開啟），檔案未被截斷，改用 Edit 工具。
- P7-10：`phase-07` commit、本機標籤 `v1.1.0`、以該 commit 重新發佈（產品版本內嵌該 commit）、fast-forward 合併回 `main`；不推送 `upstream`。

### Test Results（v1.1.0）
| Test | Expected | Actual | Status |
|------|----------|--------|--------|
| `dotnet build ScreenshotApp.sln -c Release`（1.1.0） | 0 warning、0 error | 0 warning、0 error | PASS |
| `dotnet test ScreenshotApp.sln -c Release --no-build` | 全數通過 | 933 passed | PASS |
| `pwsh -File tools/Publish-Release.ps1`（試發佈） | 兩種版本與 ZIP | 資料夾版 275 檔／155.9 MB（ZIP 67.3 MB）、單檔版 8 檔／149.4 MB（ZIP 63.5 MB） | PASS |
| 發佈版煙霧驗證（資料夾版 Phase 3、單檔版 Phase 1、2） | 全數通過 | 提交與合併時沒有閒置，未執行（之後補跑，見下一段） | 尚未驗證 → 已補跑 |

## Session: 2026-10-19（v1.1.0 發佈版煙霧驗證補跑）

### Actions Taken
- 提交 `6c7c103`、本機標籤 `v1.1.0`（annotated，比照 `v1.0.0`）、以該 commit 重新發佈（兩種版本的產品版本皆為 `1.1.0+6c7c103…`，ZIP 晚於 commit，ZIP 內的執行檔與文件為新版）、fast-forward 合併回 `main`；沒有推送。
- 背景閒置偵測回報閒置後，以前景依序執行三支煙霧腳本，期間不執行其他指令：資料夾版 Phase 3、單檔版 Phase 1、單檔版 Phase 2，全數通過。
- Phase 1 腳本會實際切換「登入時啟動」（寫入後移除 Run 值），結束時只還原 settings.json；確認使用者還原後的設定為不啟用、執行前也沒有 Run 值，沒有遺失使用者的設定。
- 證據（結果文字檔改為 LF、CSV、填滿畫布的編輯視窗 PrintWindow 截圖）複製到 `docs/evidence/phase-07/`；覆蓋層與系統匣的螢幕截圖含使用者桌面，不放入儲存庫。
- ACCEPTANCE（Phase 7 五項檢查改為通過並附縮放值、v1.1.0 發佈版煙霧驗證表、依 ADR-0017 提供單檔版、使用者步驟註明已自動驗證）、handoff 第 3、5、6 節、計畫的 Next Step；以新的 `phase-07` commit 合併回 `main`，`v1.1.0` 標籤維持在 `6c7c103`。

### Test Results（v1.1.0 發佈版煙霧驗證）
| Test | Expected | Actual | Status |
|------|----------|--------|--------|
| `Invoke-Phase3Smoke.ps1 -ExePath publish\ScreenshotApp-1.1.0-win-x64\ScreenshotApp.exe` | 全數通過 | 43 PASS／0 FAIL；套用裁切 400%、Ctrl+Z 100%、Ctrl+Y 400%、右鍵還原完整圖片 100%、Ctrl+Z 恢復裁切；10 次循環 GDI 38～40、USER 52～53 | PASS |
| `Invoke-Phase1Smoke.ps1 -ExePath publish\ScreenshotApp-1.1.0-win-x64-singlefile\ScreenshotApp.exe` | 全數通過 | 31 PASS／0 FAIL | PASS |
| `Invoke-Phase2Smoke.ps1 -ExePath publish\ScreenshotApp-1.1.0-win-x64-singlefile\ScreenshotApp.exe` | 全數通過 | 38 PASS／0 FAIL；30 次循環 GDI 恆為 39、USER 57～59 | PASS |

## Session: 2026-10-24（Apache 2.0 授權與 GitHub README）

### Actions Taken
- 使用者要上傳到 GitHub，要求 README 加入畫面截圖、特點說明與 Apache 版權宣告，並更新其他需要的檔案。分支 `feature/phase-05-github-license`（屬於發佈與文件，歸 Phase 5）。
- `LICENSE`：Apache License 2.0 官方全文原樣複製（SHA-256 `cfc7749b…3d30`，與 apache.org 發佈的檔案相同）。著作權人暫定為 `Anthony Wang`（git 作者）、年份 2026，待使用者確認。
- README：特點（Windows 原生、免安裝、簡單、易用、快速、貼上即用、隱私、繁中介面；延遲數據取自 ACCEPTANCE）、畫面（直接引用證據截圖：區域選取、v1.1.0 編輯器、設定；只含測試圖樣）、從 GitHub Releases 下載、授權段落（Apache 標準宣告）。
- 專案檔加入 `Copyright`（執行檔的著作權資訊）；`tools/Publish-Release.ps1` 把 `LICENSE` 以 `LICENSE.txt` 放進發佈目錄；ADR-0017 第 5 點修訂；THIRD-PARTY-NOTICES 註明本專案授權；AGENTS.md 的規格路徑改為 `docs/ScreenshotApp.Spec.md`；CHANGELOG `[Unreleased]`。
- 沒有執行發佈腳本：會以未提交的工作樹重新產生並覆蓋 `publish/` 中以 `v1.1.0` commit 建置的 ZIP。

### Test Results
| Test | Expected | Actual | Status |
|------|----------|--------|--------|
| `dotnet build ScreenshotApp.sln -c Release` | 0 warning、0 error | 0 warning、0 error | PASS |
| 執行檔 `LegalCopyright` | `Copyright 2026 Anthony Wang` | 相同 | PASS |
| `AssemblyConfigurationTests` | 通過 | 1 passed | PASS |
| `Publish-Release.ps1` 語法解析 | 無錯誤 | 0 個錯誤 | PASS |
| README 相對連結與圖片 | 全部存在 | 14／14 存在 | PASS |
## Session: 2026-10-24（推送與 GitHub Release v1.1.0）

### Actions Taken
- 使用者要求提交、以目前版本建立標籤、發佈執行檔的 Release 並推送，回覆「都照你的建議執行」：著作權人 `Anthony Wang`；沿用既有的 `v1.1.0` 標籤（`6c7c103`）與以該 commit 建置、已通過煙霧驗證的 ZIP，不升版；`.planning/` 與證據檔照原樣公開。
- `1d8a0c8` 以 fast-forward 合併回 `main`。
- 遠端對應（以 SSH 驗證確認）：`origin` 是 `anthonywang6503/ScreenshotApp`（公開，推送前是空的；`p76871024` 是 github.com 的 SSH 主機別名），`upstream` 是 `wanghy6503/ScreenshotApp`（`gh` 以 anthonywang6503 登入時無法存取）。
- 推送：`upstream` 加入 `main`、`feature/phase-05-github-license`、`v1.0.0`、`v1.1.0`；`origin` 加入 `main`、`v1.0.0`、`v1.1.0`（功能分支沒有推送）。
- 以 `gh`（anthonywang6503）在 `origin` 建立 Release `v1.1.0`，附上兩個 ZIP；說明含下載方式、SmartScreen 處理、1.1.0 變更、驗證結果、SHA-256 與授權。
- README 的 Releases 連結改為公開儲存庫 `anthonywang6503/ScreenshotApp`。

### Test Results
| Test | Expected | Actual | Status |
|------|----------|--------|--------|
| 發佈檔的產品版本 | `1.1.0+6c7c103…`（與 `v1.1.0` 標籤相同） | 資料夾版與單檔版皆為 `1.1.0+6c7c10383a92…` | PASS |
| Release 檔案的 SHA-256 與大小 | 與本機 ZIP 相同 | 兩個檔案的 digest 與大小皆相同（70,599,553／66,576,859 bytes） | PASS |
| Release 狀態 | 正式版（非草稿、非預覽版） | draft false、prerelease false | PASS |