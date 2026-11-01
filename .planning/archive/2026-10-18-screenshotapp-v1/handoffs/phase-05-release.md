# Handoff — Phase 5：完整驗證與發佈

**日期**：2026-10-18
**狀態**：完成（2026-10-18）；v1.0.0
**分支**：`feature/phase-05-release` → fast-forward 合併回 `main`，標記 `v1.0.0`

## 1. 完成內容

| Item | 產出 |
|------|------|
| P5-00 | `docs/adr/0017-release-packaging.md`：資料夾版為主、單檔版經煙霧驗證後才提供、框架附屬組件只保留 zh-Hant、`Version` 1.0.0、隨附 Runtime 授權檔、不簽章（依試發佈的實測大小與檔案數決定） |
| P5-01 | 靜態檢查：程式碼中的中文字串只有故障日誌內容與程式錯誤的例外訊息，介面文字皆來自 `Strings.resx`；各視窗存取鍵不重複、按鈕皆有工具提示。實機：Phase 3 腳本加入 Tab 巡覽，焦點經過全部可用的工具列、屬性列與縮放控制項 |
| P5-02 | `Resources/ErrorText.cs`：另存、開啟圖片讀檔、設定檔讀寫、登入時啟動失敗時，常見原因改以繁體中文說明（`ErrorTextTests` 13 個，先紅後綠）；`docs/ACCEPTANCE.md` 錯誤恢復總檢表（10 種情境的處理、訊息與證據） |
| P5-03 | Phase 4 腳本加入 `-Cycles`（預設 30）的完整循環資源量測與延遲量測；以發佈版量測（見第 3 節） |
| P5-04 | `docs/USER_GUIDE.md`（新）、`docs/ARCHITECTURE.md`（新，subagent 撰寫、主 agent 審閱修正）、`THIRD-PARTY-NOTICES.md`（新）、README 改寫、`docs/KNOWN_LIMITATIONS.md`（發佈與執行環境、字型沿用範圍）、`docs/ACCEPTANCE.md`；ADR-0002／0009／0010／0016 補上與目前程式碼不同之處的修訂說明 |
| P5-05 | `tools/Publish-Release.ps1`：`dotnet publish -c Release -r win-x64 --self-contained true`，產生資料夾版與單檔版（`PublishSingleFile` + `IncludeNativeLibrariesForSelfExtract`），複製授權檔與文件並建立 ZIP |
| P5-06 | Release 建置 0 警告 0 錯誤；894 個測試全數通過（4 秒） |
| P5-07 | 發佈版煙霧驗證：資料夾版 Phase 1～4、單檔版 Phase 1～2 全數通過（見第 3 節） |
| P5-08 | `docs/ACCEPTANCE.md` MT-01～MT-11 總表與 Phase 5 使用者驗證步驟（發佈版、單檔版、MT-11 乾淨機器） |
| P5-11 | 查明不是產品缺陷：Delete 一直正常，煙霧腳本以「偏紅」像素判斷箭頭，已知圖樣在該範圍本來就有 560 個符合條件的像素；腳本改為比較與原圖不同的像素，發佈版重跑通過 |

## 2. 設計原因

- **資料夾版為主**：單檔版第一次執行要把 WPF 原生程式庫解壓到暫存資料夾（實測位置 `%TEMP%\.net\ScreenshotApp\<識別碼>\`），暫存資料夾受限時無法啟動；資料夾版沒有這個前提。
- **只保留 zh-Hant 附屬組件**：介面只有繁體中文；WPF 文字輸入框的右鍵選單等框架文字需要 zh-Hant，其餘 12 種語言共 204 個檔案、17 MB 用不到。
- **ZIP 在 v1.0.0 的 commit 之後產生**：產品版本內嵌建置當時的 commit（`1.0.0+<commit>`），在 commit 之前發佈會內嵌上一個 commit。
- **ErrorText 只對應常見情況**：.NET 對拒絕存取、找不到路徑、路徑太長、檔案被占用的例外訊息是英文資源字串；其他 Win32 錯誤的訊息由 Windows 依系統語言提供，原樣顯示即可。
- **P5-11 先判別再動手**：先以 `EditorSessionTests` 排除工作階段層，再以暫時的診斷版建置記錄按鍵、滑鼠與可執行判斷（同時寫入 UI Automation HelpText 與暫存檔），一次閒置就取得決定性的紀錄；確認是腳本問題後沒有修改產品程式碼。
- **GDI 判定改為中位數**：每次循環都開啟 Windows 存檔對話框時，GDI 物件約 126～134 且有下一次即回落的單次尖峰；以最大值比較前後幾次會把尖峰誤判成成長。逐次數據、三次執行的結果與判定的修改都記錄在 ACCEPTANCE。

## 3. 驗證結果與證據

| 驗證 | 結果 | 證據 |
|------|------|------|
| `dotnet build ScreenshotApp.sln -c Release` | 0 警告、0 錯誤 | 本機輸出 |
| `dotnet test ScreenshotApp.sln -c Release` | 894 passed／0 failed（4 秒） | `docs/ACCEPTANCE.md` 第 1 節 |
| `tools/Publish-Release.ps1` | 資料夾版 275 檔、約 156 MB，ZIP 約 67 MB；單檔版約 149 MB，ZIP 約 64 MB；ZIP 以發佈資料夾為根目錄 | `publish/`（不納入版本控制） |
| 發佈版 Phase 1（資料夾版／單檔版） | 31／31、31／31 | `docs/evidence/phase-05/release-phase1-results.txt`、`singlefile-phase1-results.txt` |
| 發佈版 Phase 2（30 次循環） | 38／38、38／38；GDI 恆為 41 | `docs/evidence/phase-05/release-phase2-*`、`singlefile-phase2-*` |
| 發佈版 Phase 3（資料夾版） | 38／38：Tab 巡覽、Delete 刪除（箭頭範圍與原圖不同的像素 0）、Ctrl+Z 恢復、10 次編輯循環資源無成長 | `docs/evidence/phase-05/release-phase3-*` |
| 發佈版 Phase 4（資料夾版） | 25／25：原 22 項＋30 次完整循環全部成功、GDI 中位數 128→128（126～132）、USER 92→93～94 | `docs/evidence/phase-05/release-phase4-*` |
| 延遲（中位數） | 按快捷鍵到覆蓋層 154 ms；放開滑鼠到編輯器 91 ms（600×400）、93 ms（1920×1080）；開啟 3840×2160 PNG 285 ms；1920×1080 複製 94 ms、存檔 77 ms；3840×2160 複製 342 ms、存檔 311 ms | `docs/ACCEPTANCE.md` 第 2 節 |
| P5-11 診斷 | Delete：`CanDelete=True`、`DeleteExecuted result=True`，選取清空 | `docs/evidence/phase-05/p5-11-delete-diag-input.log` |

## 4. 風險

- **MT 尚未全部回報**：多螢幕、混合 DPI、其他縮放比例、剪貼簿改為 PNG 後的 Office 貼上、錯誤恢復的實體情境、乾淨機器都需要使用者驗證；本機只有一個 175% 顯示器。
- **未簽章**：下載後可能出現 SmartScreen 警告；企業環境可能禁止執行。
- **煙霧腳本與量測**：延遲含輪詢與計時器解析度誤差（約 16 ms）；資源趨勢只有 30 次循環，記憶體隨垃圾回收起伏，三次執行的私有記憶體趨勢為每次 −0.10、−0.01、+0.99 MB。
- **WPF 快捷鍵的 `e.OriginalSource is not TextBox` 檢查**：依 WPF 的 KeyBinding 路由，命令目標是綁定所在的視窗，這個檢查對按鍵觸發的命令可能沒有作用；實際的保護是文字輸入框先處理 Ctrl+C／Ctrl+Z／Delete 等按鍵、其他修改文件的命令被工作階段的忙碌保護擋下（使用者 MT-06 已確認輸入中 Ctrl+C 複製文字）。程式碼未修改，記錄於 findings。

## 5. 後續工作

1. 依使用者回報處理 MT-04、MT-05、MT-07、MT-08（重新確認）、MT-09、MT-10（確認）、MT-11 與十字游標的重新確認；有問題時在 `main` 開修正分支，修正後重新發佈（版本號視變更決定）。
2. 若使用者希望未修改的截圖也要詢問，調整新文件的初始匯出版本（ADR-0016）。
3. v1.1 候選功能見規格第 11 節。

## 6. 未驗證項目

- 使用者實機：MT-04（100／125／150／200% 縮放）、MT-05（雙螢幕混合 DPI、負座標、上下排列）、MT-07（使用者目視；Agent 端已確認 25%／400% 與 100% 輸出相同）、MT-08 重新確認（剪貼簿加入 PNG 後貼入 PowerPoint、Word、Teams）、MT-09（磁碟不可寫、剪貼簿占用、顯示器中途拔除）、MT-10 使用者確認、MT-11（沒有 .NET Runtime 的乾淨 Windows 11）、十字游標修正的重新確認。
- 登出或關機時的未匯出變更詢問（需要實際登出）。
- 程式自己的存檔失敗訊息（磁碟已滿、檔案被鎖、隨身碟拔除）只有單元測試。
- 單檔版只執行 Phase 1、2 腳本（系統匣、快捷鍵、截圖、編輯器複製）；編輯、開啟圖片與另存以資料夾版驗證（兩者的應用程式組件相同）。
- Windows 10 相容性。
