# Handoff — Phase 4：圖片輸入輸出

**日期**：2026-10-18
**狀態**：完成（2026-10-18）；Delete 鍵問題依使用者指示列為待辦 P5-11
**分支**：`feature/phase-04-image-io` → fast-forward 合併回 `main`（之前另有 MT 回饋修正 `617f64a` 已單獨合併）

## 0. 本階段開始前：MT 回報與十字游標修正

- 使用者回報 MT-01、MT-02（占用提示與觸發）、MT-03、MT-06、MT-08 通過（MT-08 以 Phase 3 版本驗證，當時剪貼簿只有 24 位元 DIB），並回報「擷取全白畫面時十字準星看不清」。
- 根因：覆蓋層使用系統十字游標，它的每個像素都是反相；白色畫面被 50% 暗化成灰 127 後，反相結果是 128，對比只有 1 個色階。
- 修正（P2-15，經使用者同意單獨 commit 並合併回 `main`）：`Capture/CrosshairCursor` 以 `CreateCursor` 自繪黑芯白邊十字，尺寸取系統游標在該 DPI 的標準尺寸，熱點在中心像素，每種尺寸建立一次沿用到結束；`CrosshairCursorTests` 51 個；對照圖 `docs/evidence/phase-02/crosshair-contrast.png`。

## 1. 完成內容

| Item | 產出 |
|------|------|
| P4-00 | `docs/adr/0014`（剪貼簿格式與重試）、`0015`（檔案寫入與背景編碼）、`0016`（未匯出變更保護），皆 Accepted |
| P4-01 | `ImageRenderer` 已是唯一合成器；新增 AT-07 測試：同一組編輯在 25%、100%、400% 畫布縮放下完成，輸出逐位元組相同。煙霧驗證發現裁切後小字級文字與斜線的平滑像素與未裁切時不同，改為先合成整張圖片再取出裁切範圍（`Render_CroppedOutput_EqualsSameRegionOfUncroppedOutput` 先紅後綠，ADR-0011 第 4 點修訂） |
| P4-02 | `Export/ClipboardService.cs`：`ClipboardPayload`（24 位元 DIB＋`PNG`，可在背景執行緒產生）、可注入的 `TrySetImage`、`DescribeError`（`CLIPBRD_E_CANT_OPEN` 專屬訊息）；重試沿用 .NET 內建 |
| P4-03 | `PngFileService.Encode`（剪貼簿與存檔共用）；編輯器另存改為背景編碼與寫檔、初始資料夾為上次成功儲存的位置、成功後寫入 `LastSaveDirectory` |
| P4-04 | `Export/ImageFileLoader.cs`（subagent 實作）；系統匣「開啟圖片」、編輯器「開啟」按鈕與 `Ctrl+O`；開檔在背景執行緒解碼 |
| P4-05 | `UndoRedoStack.Version`、`ImageDocument.ExportedVersion`／`IsDirty`／`MarkExported`；`Editor/UnsavedChangesDialog`；新截圖、開啟圖片、關閉編輯器、系統匣結束、登出或關機的詢問 |
| P4-06 | 單一文件、複製或儲存後留在編輯、取消新截圖恢復原文件、關閉編輯器回常駐（沿用 Phase 3，加上詢問） |
| P4-07 | 新增 `DirtyTrackingTests`（7）、`ImageFileLoaderTests`（28，subagent 撰寫 26 個、主 agent 補 2 個）；`UndoRedoStackTests` +5（狀態版本）、`ClipboardServiceTests` +9、`PngFileServiceTests` +1（執行緒集區存檔）、`ImageRendererTests` +5（AT-07 與裁切一致性）；全專案 880 個（十字游標修正時為 825） |
| P4-08 | `tools/smoke/Invoke-Phase4Smoke.ps1`：第 4 次與強化後的第 5 次執行皆 22 項全數通過（見第 3 節）；Phase 3 腳本的 Ctrl+滾輪改為按住 Ctrl 後直接送滾輪訊息、刪除後復原的檢查改為箭頭確實被刪除才判定 |
| P4-09 | `docs/ACCEPTANCE.md` 加入 Phase 4 驗證步驟（十字游標、MT-08 重新確認、MT-09、未匯出變更、開啟圖片） |
| P4-11 | `tools/smoke/Invoke-Phase2Smoke.ps1` 改為從編輯器以「複製」按鈕取得截圖結果並關閉編輯器；以 Phase 4 的 Release 建置重跑，38 項全數通過 |
| 文件 | `README.md`、`docs/KNOWN_LIMITATIONS.md`、`CHANGELOG.md` |

## 2. 設計原因

- **剪貼簿沿用 .NET 內建重試**：.NET 10 的剪貼簿核心對 `OleSetClipboard` 與 `OleFlushClipboard` 各自最多重試 10 次、間隔 100 ms；外層再重試 5 次會讓被占用的剪貼簿阻塞 UI 執行緒約 10 秒。
- **DIB＋PNG、PNG 與存檔共用編碼**：支援 PNG 的程式（Office、瀏覽器）拿到與 PNG 檔逐位元組相同的資料，不經接收端轉換；DIB 供只認得點陣圖的程式。
- **合成在 UI 執行緒、編碼與寫檔在背景執行緒**：`RenderTargetBitmap` 需要 Dispatcher；凍結的圖片可交給其他執行緒；記錄合成當時的文件版本，編碼期間的修改仍是未匯出的變更。
- **以狀態版本判斷未匯出**：每個狀態一個版本，復原與重做回到該狀態的版本；修改後復原回匯出時的狀態不詢問；捨棄最舊一筆時更新最底層的版本。
- **剛截取或剛開啟的圖片沒有未匯出的變更**：明確需求以「自最後一次成功匯出後曾修改」為條件，優先於流程描述的「未匯出內容」；重截不必多一次確認。於控制點告知使用者。
- **結束程式前先詢問、再 `Shutdown`**：`Application.Shutdown` 關閉視窗時不能取消；登出或關機則在工作階段結束前拒絕並詢問。
- **對話框期間與匯出期間忽略新的要求**：詢問、開檔、背景匯出，或編輯器開著自己的對話框（Windows 會停用擁有者視窗）時，新截圖、開啟圖片與結束要求一律忽略，避免對話框重疊或在途中更換文件。
- **開啟的圖片以白色背景合成透明度**：24 位元 DIB 沒有 alpha，保留透明度會讓剪貼簿 DIB 的透明處變黑、與 PNG 不一致。

## 3. 驗證結果與證據

| 驗證 | 結果 | 證據 |
|------|------|------|
| `dotnet build ScreenshotApp.sln -c Release` | 0 警告、0 錯誤 | 本機輸出 |
| `dotnet test ScreenshotApp.sln -c Release` | 880 passed／0 failed | `docs/ACCEPTANCE.md` 第 1 節 |
| Phase 4 煙霧驗證（Release，單一顯示器 3456×2160、175%） | 22／22 通過：剪貼簿 PNG＋點陣圖像素相同、另存 PNG 與剪貼簿 PNG 逐位元組相同、記住儲存位置、唯讀檔由 Windows 拒絕後恢復、剪貼簿占用約 1.2 秒後提示、新截圖／關閉編輯器／開啟圖片／系統匣結束前詢問、開啟 BMP 逐像素相同、開啟 JPEG、損壞的 PNG 顯示原因、系統匣開啟圖片、沒有變更時關閉不詢問 | `docs/evidence/phase-04/` |
| Phase 3 煙霧驗證（同上，補完 Phase 3 收尾時未執行的部分） | 35 通過、2 失敗：覆蓋層游標（48×48、反相 0 像素）、文字輸入、縮放 400%／25% 輸出相同、裁切輸出與裁切前同一範圍相同、新截圖取代與取消恢復、第二個執行個體、10 次編輯循環資源無成長皆通過；「選取後按 Delete 刪除」與其「刪除後復原」失敗（待辦 P5-11） | `docs/evidence/phase-03/` |
| Phase 2 煙霧驗證（P4-11 更新後） | 38／38 通過，30 次循環 GDI 恆為 41、USER 59～60、Handle 635～637 | `docs/evidence/phase-02/phase2-results.txt` |

- Phase 4 煙霧驗證前 3 次的失敗都是腳本問題（PowerShell 變數名稱衝突、唯讀檔提示由 Windows 存檔對話框顯示、錯誤訊息框沒有關閉），以及途中偵測到使用者操作而停止；第 1 次中途失敗時沒有還原使用者原本的剪貼簿內容（腳本已修正）。
- 較早一次 Phase 3 補跑中，縮放與文字的檢查在動作沒有發生時判定通過（按鍵因 Windows Terminal 取走前景沒有送達）；腳本已改為確認動作發生才比較，上表為修改後的結果。

## 4. 風險

- **未匯出變更的詢問流程只有部分實機確認**：詢問路徑（新截圖、關閉編輯器、開啟圖片、系統匣結束、登出或關機）依賴非同步流程與對話框期間的忙碌保護；版本判斷有單元測試，介面流程以煙霧腳本驗證（見第 6 節）。登出或關機需要實際登出才能驗證。
- **程式自己的存檔失敗訊息難以實機觸發**：Windows 存檔對話框（預設建立測試檔）會先拒絕唯讀檔與沒有權限的位置；「寫入時才失敗」（磁碟已滿、檔案被鎖、隨身碟拔除）由單元測試涵蓋（唯讀目標、資料夾不存在），實機只能由使用者在對應情境確認。
- **PowerPoint 改貼 PNG**：剪貼簿加入 PNG 後，支援 PNG 的程式會改用它；MT-08 必須以新版重新確認。
- **剪貼簿被占用時 UI 約 1 秒沒有回應**：.NET 內建重試在 UI 執行緒以 `Thread.Sleep` 等待；實測約 1.1～1.2 秒後顯示原因。
- **開啟超大圖片的記憶體**：上限 1 億像素時峰值約 1.1 GB（subagent 實測）；超過上限只讀標頭即拒絕。
- **煙霧腳本曾造成使用者剪貼簿內容遺失**：第一次 Phase 4 煙霧驗證中途失敗時沒有還原剪貼簿，已修正三支腳本，於 Phase 4 控制點告知使用者。
- **Delete 鍵可能無效（待辦 P5-11）**：煙霧驗證以 SendInput 送出 Delete 時沒有刪除選取的標註，之後的 `Ctrl+Z` 也沒有作用；可能是以選取工具點選標註但沒有移動之後仍處於拖曳狀態。實體鍵盤是否受影響尚未確認，已請使用者以實體鍵盤確認。
- **每次匯出合成整張圖片**：裁切一致性的代價；開啟的超大圖片只保留一小塊時，每次複製或另存仍需整張圖片大小的暫時記憶體（1 億像素約 400 MB）。
- **PNG 有 alpha 通道**：合成結果中文字邊緣有 alpha 254 的像素，另存的 PNG 為 RGBA；肉眼無法分辨，未處理。

## 5. 後續工作（Phase 5）

1. 從 `main` 建立 `feature/phase-05-release`，先寫 ADR-0017（發佈與封裝策略）。
2. P5-11：查明並修正 Delete 鍵問題（先以 `EditorSessionTests` 判別「點選沒有移動後仍在拖曳」，再查視窗與輸入層），之後以發佈版重跑 Phase 1～4 的腳本（P5-07）。
3. 繁中介面與操作性總檢、錯誤恢復總檢、30 次完整循環的資源量測（含另存與開啟圖片）。
4. 文件完整版（`docs/ARCHITECTURE.md`、`docs/USER_GUIDE.md`、`docs/KNOWN_LIMITATIONS.md`）與 `win-x64` 自包含發佈。
5. 依使用者回報處理 MT-04、MT-05、MT-07、MT-08（重新確認）、MT-09 與十字游標的重新確認；若使用者希望未修改的截圖也要詢問，調整新文件的初始匯出版本。

## 6. 未驗證項目

- 登出或關機時的未匯出變更詢問（需要實際登出）。
- 程式自己的存檔失敗訊息（磁碟已滿、檔案被鎖、隨身碟拔除）只有單元測試；唯讀檔與沒有權限的位置由 Windows 存檔對話框先行拒絕。
- 使用者實機：MT-08 重新確認（剪貼簿加入 PNG 後貼入 PowerPoint、Word、Teams）、MT-09（磁碟不可寫、剪貼簿占用、顯示器中途拔除）、十字游標修正的重新確認、Delete 鍵的實體鍵盤確認；MT-04、MT-05、MT-07 尚未回報（MT-07 的縮放一致性已由 Agent 端煙霧驗證確認）。
- 多螢幕、混合 DPI 與其他縮放比例（本機只有一個 175% 顯示器）。
- Phase 1 煙霧腳本在 Phase 4 沒有重跑（P5-07 以發佈版重跑全部腳本）。
