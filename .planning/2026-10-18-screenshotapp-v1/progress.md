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
