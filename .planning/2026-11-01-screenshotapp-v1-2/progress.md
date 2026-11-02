# Progress Log

## Session: 2026-11-01

### Current Status
- **Phase:** 12 - 截圖歷史（complete）；Phase 13 pending，停在階段控制點
- **Started:** 2026-11-01

### Actions Taken
- 以 `init-session.ps1 -Gated` 建立 `.planning/2026-11-01-screenshotapp-v1-2/`（PLAN_ID `2026-11-01-screenshotapp-v1-2`），`.mode` 加入 `inject-smart`，與封存的 v1 計畫一致。
- 讀取 `docs/ScreenshotApp.Spec.md`、`docs/ARCHITECTURE.md`、價值機會文件中與七項功能相關的段落、封存的 v1 計畫格式與實測發現。
- 以 CodeGraph 讀取 `SelectionOverlay`、`SelectionController`、`DragSelection`、`DesktopSnapshot`、`Annotation`、`AnnotationPainter`、`AnnotationLayer`、`ImageRenderer`、`EditorSession`、`AppController`、`HotkeyService`、`TrayService` 的現況，記錄於 `findings.md`。
- 撰寫 `task_plan.md`：Phase 0（完成）、Phase 8～14、追蹤矩陣、風險、待使用者決定的 Q1～Q3 與自行決定的假設。
- 以 AskUserQuestion 提出 Q1～Q3，使用者回答：截圖歷史預設開啟並存於磁碟、長截圖手動與自動捲動都提供、尺規做編輯器尺寸標註與截圖時量測。依此改寫 task_plan 的範圍、Phase 11～13 與風險表。
- 未修改任何程式碼、規格或 `docs/`。

### Phase 8 開始
- 使用者要求載入狀態並開始實作；已恢復三份規劃文件、讀取相關 ADR 與規格，確認工作目錄基準 `6556bab`。
- 已建立 `feature/phase-08-v1-2-spec`。依本次使用者 Git 規則，不自行 commit；保留每個 Phase 完成後的控制點。
- 原生計畫格式驗證通過（8 個 Phase）。
- 已修訂規格 FR-37～FR-43、相關既有條文、AT-11～16、MT-12～15、Phase 8～14 與排除範圍；補上目標規格與已交付產品的界線。
- ADR-0007、0016 補上 v1.2 隱私需求修訂；CHANGELOG 與 ACCEPTANCE 明列本次只改文件、新功能尚未實作／驗證。
- 將不可成立的「效果後所有像素皆不等於原值」改為演算法期望值及不透明覆蓋驗收；計畫同步修正。
- 已固定尺規中心距離、歷史保留 1～500 筆與長截圖 32,768px／100,000,000 像素上限；效果參數及捲動實測細節留待各自 Phase 的 ADR。文件檢查與交接整理進行中。

### Phase 8 完成
- P8-01～P8-04 文件工作完成，交接位於 `handoffs/phase-08-v1-2-spec.md`；Phase 9 尚未開始。
- 靜態檢查確認 FR-01～43、AT-01～16、MT-01～15 完整且無重複，7 筆新需求對應與 Phase 8～14 均存在。
- 9 份 Markdown 的 UTF-8／LF 與 57 個本機相對連結檢查通過；`git diff --check` 通過。最後一致性檢查修正取消時回復先前狀態、關閉歷史仍保留舊條目，以及先整張合成再裁切的敘述。
- 只有文件變更；依 P8-04 與使用者最小驗證規則，未執行建置、產品測試、完整品質腳本或 GUI 煙霧驗證。
- 未 commit、未合併、未推送、未建立 PR。保留 `feature/phase-08-v1-2-spec` 的未提交變更。

### Test Results
| Test | Expected | Actual | Status |
|------|----------|--------|--------|
| 規劃階段未執行建置或測試 | — | — | 不適用 |
| Phase 8 需求／驗收編號與映射 | 無缺漏、無重複、七項均有映射 | FR 43、AT 16、MT 15、7 筆映射、7 個開發階段 | 通過 |
| Phase 8 文件格式與相對連結 | UTF-8、LF、本機連結有效 | 9 份 Markdown、57 個本機連結 | 通過 |
| `git diff --check` | 無空白格式錯誤 | 無輸出、exit 0 | 通過 |
| 產品／測試／工具差異 | 無變更 | 無差異 | 通過 |

### Errors
| Error | Resolution |
|-------|------------|
| 根目錄沒有 `handoffs/` | 沿用封存計畫慣例，交接放在目前計畫目錄下 |
| 收尾 patch 的 Checklist 行未匹配 | 整批未套用；確認現況後拆開修訂，改用限定 P8 編號更新 |

### Phase 9 開始
- 已以 resolver 載入三份規劃檔，格式驗證通過；建立 feature/phase-09-overlay-magnifier，保留 Phase 8 未提交變更。
- 已讀座標、覆蓋層、狀態機與剪貼簿 ADR，使用 CodeGraph 確認覆蓋層及 CaptureFlow 的來源與影響範圍。

### Phase 9 實作進度
- 已新增 ADR-0019、MagnifierGeometry、MagnifierInput、OverlayResult；完成快照取樣、覆蓋層放大鏡、Ctrl 輸入、C 取色與 AppController 文字剪貼簿通知。
- 新增幾何、取樣、Ctrl 狀態、CaptureFlow 與剪貼簿替身測試；Release 定向測試執行中。
- 使用者追加授權：每個 Phase 完成後提交。將先獨立提交既有 Phase 8 文件，再於 Phase 9 驗證完成後提交本階段。

- Phase 8 文件已獨立提交：b48b6fe。
- 初次定向測試 53 通過／1 失敗：測試假設 WPF 越界 CopyPixels 的固定例外型別，實際為 OverflowException；改驗證越界被拒絕，不綁定平台例外型別。
- tools/Invoke-Quality.ps1 通過：Release 0 警告／0 錯誤，986／986 測試通過，報告 artifacts/quality/3d78aed591154d9bbba2991737a07060/tests.trx。
- 接續補齊實機煙霧腳本、文件與收尾交接。

- 補上未顯示覆蓋層的 RenderTargetBitmap 像素驗證。首次直接 Render 子元素時包含父層位移而得到空白；改由根覆蓋層繪製並讀取面板範圍後通過，正在擴充四角案例。
- 新增 Phase 9 煙霧腳本；以既有 Phase 4 的 LibraryOnly 入口重用輸入守衛、PrintWindow 與剪貼簿工具。
- 已嘗試執行煙霧：偵測到既有 ScreenshotApp（PID 28076）而在送出任何輸入前停止，證據為 docs/evidence/phase-09/phase9-results.txt；不結束使用者程式。
- USER_GUIDE、ARCHITECTURE、KNOWN_LIMITATIONS 與 CHANGELOG 已補上開發版功能及驗證界線。

### Phase 9 完成
- P9-00～P9-08 完成，交接位於 handoffs/phase-09-overlay-magnifier.md；P9-07 依計畫允許明列尚未驗證，沒有宣稱實機通過。
- 最終品質檢查：Release 0 警告／0 錯誤，991／991 測試通過、無跳過。新增 34 個案例，包含中央及四角各 32,400 像素的 WPF 繪製比對。
- 最終 TRX：artifacts/quality/abd42783c6ba4cb6a08b3fb249fa709b/tests.trx；提交的摘要為 docs/evidence/phase-09/automated-validation.json。
- 煙霧腳本與共用工具 AST 語法通過；實機因既有 ScreenshotApp 而 NOT_RUN，未送出模擬輸入。混合 DPI／多螢幕、Ctrl／C／拖曳等桌面操作尚未驗證。
- 36 份變更／新增檔案的文字格式、11 份 Markdown／158 個本機相對連結檢查通過；git diff --check 通過。
- 使用者已授權每個 Phase 完成後提交；Phase 8 為 b48b6fe，Phase 9 以本次收尾提交記錄。不合併、不推送、不建立 PR；Phase 10 未開始。

### Phase 10 開始
- 使用者明確要求載入 planning-with-files、實作 Phase 10 並提交。已讀計畫、進度、發現及 Phase 9 交接。
- 乾淨基準 e57ef62，已開 feature/phase-10-mosaic-blur；以 CodeGraph 取得模型、繪製及互動的影響範圍。

- 已新增 ADR-0020，修訂模型／共用繪製 ADR；完成效果模型、純像素演算法、受限弱參照快取、繪製參數與編輯器強度／工具整合。接續補上獨立像素參考測試與操作命令測試。

- 初次 Release 建置成功：0 警告／0 錯誤。已新增獨立像素參考、幾何、快取、層序／裁切／匯出及編輯器操作測試。
- 探索時嘗試讀取不存在的 ClipboardImageData.cs；已由 Export 目錄確認實際型別位於 ClipboardService.cs，未改動檔案。

- 定向測試首次建置失敗：EditorSessionTests 缺少 StaRunner 命名空間；已補上匯入，接續重跑。

- 定向測試 202 通過／2 失敗：測試誤以為控制點越過對邊會停止；既有 BoxHandles 規則為翻轉。保留產品慣例，修正測試為精確縮至 1×1，另驗證翻轉結果。

- 定向測試 204／204 通過。4K 初次量測：馬賽克約 100ms，模糊約 630ms；改為逐列更新垂直滑動總和以改善記憶體區域性，另把每塊平均值移到填色迴圈外。
- 已確認使用者既有 ScreenshotApp PID 28076 仍執行；實機煙霧將在安全守衛記錄 NOT_RUN，不操作使用者程式。
- .editorconfig 不存在；沿用專案現行格式，不新增格式設定。

- 優化後 35 個像素／繪製測試通過；4K 重算中位數馬賽克約 51ms、模糊約 424ms，保留大範圍 UI 停頓限制，不宣稱實機流暢。
- Phase 10 煙霧腳本 AST 語法通過；實際啟動在既有 ScreenshotApp 守衛停止，產生 NOT_RUN，未送出鍵鼠輸入。使用者文件、架構、限制與 CHANGELOG 已同步。

### Phase 10 完成
- P10-00～P10-08 完成；交接 handoffs/phase-10-mosaic-blur.md。P10-07 依計畫允許明列尚未驗證；沒有宣稱實機通過。
- 最終品質檢查：Release 0 警告／0 錯誤，1036／1036 測試通過，新增 45 案例，無跳過。TRX：artifacts/quality/5749cfa3a7854440abe9089faab3828f/tests.trx。
- 4K 量測及摘要放在 docs/evidence/phase-10/；已確認量測的組件 SHA-256 與最終品質檢查組件一致。
- 文件、驗收紀錄、CHANGELOG、交接及計畫同步完成；Phase 11 未開始。準備以使用者授權在目前分支提交，不合併、不推送、不建立 PR。

- 收尾格式驗證把「### Phase 10 執行錯誤與處理」誤判為新增階段，已改為二級章節，保留原生八個 Phase 結構後重新驗證。

- 收尾檢查 35 份檔案、12 份 Markdown／154 個本機連結均有效；發現量測 JSON 的序列化換行為 CRLF，已修正工具輸出與證據為 LF。最終格式、計畫驗證及 git diff --check 通過後提交。

- 納入新增檔案後，暫存差異檢查指出交接文件兩處 Markdown 換行用的尾端空白；已改成空白行並更新同一筆提交。提交前驗證與實機未驗證界線均保留。

## Phase 11 開始（2026-11-02）
- 已以 resolver 載入三份規劃與 Phase 10 交接；基準 352b53a，工作目錄乾淨，建立 feature/phase-11-ruler-eyedropper。
- 範圍為尺寸標註、覆蓋層量測及編輯器取色；先讀規格與相關 ADR，使用 CodeGraph 取得模型、繪製、覆蓋層影響範圍。

- 新增 ADR-0021、共用 PixelMeasurement、尺規模型及共用繪製；完成編輯器尺規／暫時取色與文字輸入保留路徑，接續覆蓋層量測與測試。

- Release 建置 0 警告／0 錯誤。新增量測、尺規命令／輸出、取色與隱藏覆蓋層狀態測試。定向測試 38 通過／1 失敗：文字資源掃描不辨識條件式鍵，改成兩個明確 Strings.Get 呼叫。

- 定向 74／74 與第一輪品質檢查 1071／1071 通過。煙霧腳本 AST 通過，執行時因既有 ScreenshotApp PID 28076 停在守衛，NOT_RUN，未操作桌面。
- 收尾檢查發現右下邊界浮點容差可能將圖片內點轉為越界像素；先新增回歸案例確認 CopyPixels 例外，再於座標換算後驗證像素範圍，拒絕而不夾取。準備重跑最終品質檢查。

- 右下邊界回歸與完整 1072／1072 測試通過；另發現 Alt+I 已由字級使用，取色改 Alt+D，尺規 Alt+Q。使用者指南、限制、架構、模型／文字 ADR 與 CHANGELOG 已同步，執行資源更新後的最終品質檢查。

## Phase 11 完成（2026-11-02）

- P11-00～P11-08 完成；交接 handoffs/phase-11-ruler-eyedropper.md。P11-07 以 NOT_RUN 明列實機尚未驗證。
- 最終品質檢查 Release 0 警告／0 錯誤、1072／1072 測試通過，新增 36 案例、無跳過。TRX：artifacts/quality/9272029a78fc4b2c8c2f554ac5795657/tests.trx。
- 規格行為沿用既有 FR-38／41，新增 ADR-0021、修訂模型／文字 ADR，USER_GUIDE、ARCHITECTURE、KNOWN_LIMITATIONS、ACCEPTANCE 與 CHANGELOG 已同步。
- Phase 12 未開始；依使用者要求在目前分支提交，不建立 PR、不推送。

- 最終靜態收尾：31 份檔案 UTF-8／LF、12 份 Markdown／164 個本機相對連結、受測組件 SHA-256 與 git diff --check 通過；計畫原生格式驗證及 attest 完成。

## Phase 12 開始（2026-11-02）
- 已載入三份計畫與 Phase 11 交接；乾淨基準 71fb4ff，建立 feature/phase-12-capture-history。使用者授權完成後提交。

- 已完成 ADR-0022 草稿、明確 JSON 格式、背景序列佇列、歷史視窗及生命週期整合；準備建置與隔離測試。探索測試支援檔時誤用 Helpers 路徑，已找到 TestSupport，未影響產品。

- 初次 Release 建置 0 警告／0 錯誤；定向 71／71 通過。CodeGraph 連線在後續探索中關閉，改用當前檔案補足來源；AppStateMachine 實際位於專案根目錄。PowerShell 的 rg 檔名萬用字元改用 -g。
- 檢查發現關閉歷史時若仍執行啟動維護，會碰到既有歷史；已限制停用時不啟動維護，確保煙霧腳本不改變使用者歷史。背景磁碟 I/O 不持有排程鎖，避免 UI 或有限等待被磁碟鎖拖住。

- 歷史與流程測試 50／50 通過；測試替身的一處可為 null 轉型造成兩個警告，已改用 Assert.IsType。
- 新增 Phase 12 專用煙霧；只接受空白歷史根目錄，避免淘汰及清除既有資料。Phase 2～4 加入停用歷史設定；9～11 原本已預先停用。使用者現有執行個體 PID 27708，將由安全前置檢查記錄 NOT_RUN。

- 第一輪完整品質檢查 1122／1122 通過、Release 0 警告／0 錯誤。
- 收尾回歸先重現兩種缺陷：編輯器／設定存取鍵衝突（2 個失敗）；停用後先前排入的維護仍刪除不完整條目（1 個失敗）。歷史入口改 Alt+X，設定開關 Alt+E、清除 Alt+X；維護與淘汰也檢查寫入世代。接續最終驗證。

## Phase 12 完成（2026-11-02）

- 最終品質檢查 Release 0 警告／0 錯誤、1126／1126 通過，新增 54 案例，無跳過；TRX artifacts/quality/01f15015983647dabd33a7488182e65d/tests.trx。
- 九種標註往返、還原輸出、序列寫入、損毀／唯讀、淘汰／清除、關閉世代與不復活、未匯出保護、替身剪貼簿、有限等待及隱藏視窗驗證完成。
- 使用者文件、架構、設定／隱私 ADR、驗收紀錄與 CHANGELOG 同步；交接 handoffs/phase-12-capture-history.md。
- 煙霧腳本 AST 通過；既有使用者 ScreenshotApp PID 27708 令實機停在守衛，NOT_RUN，未操作桌面或使用者資料。MT-15 與既有煙霧重跑後的檔案比對仍未驗證。
- 36 份檔案初次 UTF-8／LF、180 個本機連結及 git diff --check 通過；收尾新增交接後再檢查。探索時的不存在支援／量測路徑與 rg 萬用字元錯誤均已改用確認存在的來源，CodeGraph 連線中斷後使用當前檔案補足。
- Phase 13 未開始；依使用者授權準備在目前分支提交，不合併、不推送、不建立 PR。

- 提交前再檢查系統登出取消：其他程式可取消整次登出，因此 OnSessionEnding 不能先永久停用歷史更新。先新增回歸確認失敗，再把等待與關閉狀態留給真正 Dispose／RequestExit。WPF 事件參數沒有公開建構式，測試改用不需 Reason 的事件替身；初次建置失敗後已修正。準備更新最終 1127 案例證據。

- 最終登出取消回歸及完整品質檢查通過：1127／1127、Release 0 警告／0 錯誤，新增 55 案例。最終 TRX artifacts/quality/61601d1580894aecbde52d9414d26cc7/tests.trx，取代前一輪 1126 案例的證據。
- Phase 12 交接與計畫已完成，Phase 13 pending；本次收尾提交記錄由 Git 保留。

## Phase 13 開始（2026-11-02）
- 已載入現行計畫、發現與 Phase 12 交接；工作目錄乾淨，基準 0d29a59，新增 feature/phase-13-scrolling-capture。
- 現有使用者執行個體 PID 35760 鎖住標準 Release 路徑；改用隔離建置目錄，實機測試先跑守衛，不關閉使用者程式。

- 新增 ScrollFrame／ScrollStitcher／ScrollCaptureSession 與合成像素測試：列雜湊＋KMP 候選、逐像素確認、固定首尾、歧義拒絕、完整列上限、縮圖、穩定輪詢與自動停止。開始隔離 Release 定向測試。

- 純核心定向測試 21／21 通過。新增四邊框 HWND、面板、區域輪詢、自動輸入保護與忙碌狀態整合；範圍重疊面板時取樣前隱藏並等待 DWM。新增系統匣與選用快捷鍵設定，開始整合定向測試。

- 整合測試 78／78、流程／輸入／區域與快捷鍵 71／71 通過。完整品質檢查 1172／1172、Release 0 警告／0 錯誤；透過 BaseOutputPath=bin/phase13/ 避開既有執行檔鎖定。
- 新增可捲動圖樣、Phase 13 煙霧與兩種滾輪探測工具；三支腳本 AST 與內嵌 C# 編譯通過。執行前置守衛偵測既有程式，煙霧與 12 格相容性矩陣為 NOT_RUN，沒有送出桌面輸入或修改使用者資料。
- 首次整合因 RunAsync 新增可選參數導致測試方法群組無法轉型；保留無參數 overload 後修正，後續品質檢查通過。

- 隱藏 WPF 面板／設定版面檢查通過；透明背景的預覽問題只修正暫存繪製工具。長圖歷史回歸 3／3 通過（600×1600、1×32768、2×32768），縮圖尺寸有效且原圖逐像素還原。
- 收尾回歸先重現「清除長截圖快捷鍵後保留舊驗證錯誤」1 案失敗，再統一重新顯示狀態與清除錯誤樣式；開始最終完整品質檢查。

- 最終品質檢查 1176／1176 通過、Release 0 警告／0 錯誤，新增 49 個案例。另將長圖歷史回歸延伸至 ImageRenderer 共用合成，3／3 逐像素一致；更新最終測試組件前再執行品質腳本。
- 自動化摘要、驗收紀錄與 Phase 13 交接已建立；P13-00 與桌面煙霧明示 NOT_RUN，保留未驗證責任與重跑步驟。

## Phase 13 程式交付完成（2026-11-02）

- 最終品質檢查 1176／1176、Release 0 警告／0 錯誤，無跳過，新增 49 案例；TRX artifacts/quality/a36117344e754ca1a8635e8d91c0937d/tests.trx。摘要已記錄受測組件 SHA-256。
- 隱藏 WPF 版面、腳本 AST／內嵌 C#、長圖歷史與共用合成驗證通過；48 份修改／新增檔 UTF-8／LF、222 個本機文件連結與 git diff --check 通過，提交前再檢查新增交接及計畫。
- P13-00 未實測，計畫保留未勾選；桌面煙霧與 MT-13 均尚未驗證。Phase 13 complete 僅表示本次程式與文件交付完成，Phase 14 pending，續作補齊實機證據。
- 依使用者指令準備在目前 feature/phase-13-scrolling-capture 分支提交；不合併、不推送、不建立 PR。
