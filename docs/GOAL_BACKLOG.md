# 工程改善待辦與驗證紀錄

## 執行範圍

- 起始日期：2026-10-29（台灣時間）。起始 commit：`7729736`；工作分支：`fix/engineering-improvements`。
- 執行輪次：**1 / 40**。一輪指一個完整的代理執行回合（包含其工具呼叫）；後續回合接續此紀錄，不重設計數。
- 逐項完成客觀驗證、更新本文件及 CHANGELOG，建立一個完整且可獨立理解的 commit。
- 保留規格與 ADR 已記載的行為；不部署、不合併、不推送、不建立 PR、不修改正式環境基礎設施、不弱化測試、不進行推測性的產品變更。
- 起始已有未追蹤的 `.codegraph/`、`AUTONOMOUS_GOAL.md`、`BUSINESS_VALUE_OPPORTUNITIES.md`、`UIUX_VALUE_OPPORTUNITIES.md`；保留原狀，不納入提交。
- 停止條件：經下列範圍的檢查，不再有高可信度且可獨立驗證的改善，或達到 40 輪。不能把環境限制、未決定需求或尚未執行的檢查記成通過。

## 初始盤點

| 面向 | 目前證據與判斷 |
|---|---|
| Architecture | [架構](ARCHITECTURE.md)、17 份 ADR 與 CodeGraph 原始碼：單一 WPF 程式加測試專案；AppController 協調生命週期，CaptureFlow 已有平台接縫，編輯命令與匯出版本各有明確契約。重點檢查非同步等待前後的資料保護。 |
| Tests | 本輪實跑 **933 通過、0 失敗、0 跳過**；`EditorWindow.ConfirmDiscardAsync` 的非同步決策尚無直接回歸測試，現有 DirtyTrackingTests 只驗證模型的匯出版本。 |
| CI | 未追蹤任何 CI workflow；公開儲存庫 GitHub API 的 workflows、runs 均為 0。可建立僅建置與測試的品質檢查，不包含發佈。 |
| Build | .NET SDK `10.0.403`；Release 建置 **0 警告、0 錯誤**。目前未固定 SDK，也未把警告當成建置失敗。 |
| Open Issues | 連線工具查詢公開儲存庫 `anthonywang6503/ScreenshotApp` 的 open issues 為空；`wanghy6503/ScreenshotApp` 的直接 API 為 404，該遠端狀態無法確認。 |
| TODO / FIXME | 排除 bin/obj 後搜尋產品、測試與工具，沒有 TODO、FIXME、HACK 或 NotImplementedException 命中。缺少標記不代表沒有缺陷。 |
| Dependency Health | 產品沒有直接 NuGet 相依套件；含間接套件的 NuGet 漏洞查詢無命中。xunit `2.9.3` 及其四項間接套件被標示 Legacy，建議替代為 v3；不把淘汰標記等同已知漏洞。 |
| Recent Failures | [驗收紀錄](ACCEPTANCE.md) 與 [實測紀錄](../.planning/2026-10-18-screenshotapp-v1/findings.md) 的 Delete、裁切像素與 GDI 趨勢問題已記載原因與修正；歷史失敗不直接列為未修缺陷。 |
| Developer Documentation | README、架構、規格、ADR 與驗收文件俱全；本輪結果集中於本文件。既有未追蹤分析僅作線索，以目前原始碼與實測重新核對。 |
| Recent Git Changes | 最近功能 commit `6c7c103` 增加右鍵選單與裁切填滿；其後以驗收、授權及下載連結等文件調整為主。優先保護現有編輯及輸出流程。 |

基準命令：

```powershell
dotnet build ScreenshotApp.sln -c Release --nologo
dotnet test ScreenshotApp.sln -c Release --no-build --nologo --logger "trx;LogFileName=baseline.trx" --results-directory artifacts/engineering-goal/baseline
dotnet list ScreenshotApp.sln package --vulnerable --include-transitive --format json --no-restore
dotnet list ScreenshotApp.sln package --deprecated --include-transitive --format json --no-restore
```

TRX 等可重建的本機產物放在忽略的 `artifacts/engineering-goal/`；驗證摘要、命令與失敗原因記錄在此，不能以未提交產物取代交接說明。

## 排序方法

**Priority = Impact × Confidence × Verifiability**，各因子 1–5 分。Impact：1 為局部便利、3 為持續維護效率、5 為保護使用者資料；Confidence：1 為猜測、3 為部分證據、5 為直接證據與明確契約；Verifiability：1 依賴主觀或外部環境、3 可部分本機驗證、5 可固定輸入／失敗注入獨立驗證。分數是工程判斷；新證據出現時重排。相同分數先處理影響使用者資料的項目。

| 順序 | ID | 改善項目 | I | C | V | 乘積 | 狀態 |
|---:|---|---|---:|---:|---:|---:|---|
| 1 | E01 | 確認關閉／替換時，保護背景匯出期間新增的修改 | 5 | 5 | 5 | **125** | 已完成；見完成紀錄 |
| 2 | E02 | 開圖背景解碼完成前新增的修改不得無聲被替換 | 5 | 5 | 5 | **125** | 已完成；見完成紀錄 |
| 3 | E03 | 建立可重現的本機品質檢查與 Windows CI 設定 | 4 | 5 | 4 | **80** | 待執行 |
| 4 | E04 | 編輯歷史、裁切、匯出版本的長操作序列回歸驗證 | 4 | 4 | 5 | **80** | 待核對既有測試缺口 |

### E01：確認流程與背景匯出

- 證據：[EditorWindow](../ScreenshotApp/Editor/EditorWindow.xaml.cs) 的 `ConfirmDiscardAsync` 直接以 Copy/Save 的成功結果允許關閉或替換。相同檔案的匯出方法以合成當時版本標記成功，期間允許編輯；所以成功匯出與「目前內容已全部匯出」不是同一件事。
- 行為依據：[規格](ScreenshotApp.Spec.md) FR-23、[ADR-0016](adr/0016-unexported-changes-protection.md)、[ADR-0015](adr/0015-file-writing-and-background-encoding.md)。不增加新產品選項。
- 驗證：透過替換對話框與外部匯出的最小接縫，控制未完成的 Task；在等待期間新增標註／文字，確認操作中止且修改保留。另檢查成功且無新修改、取消、失敗、捨棄與復原回匯出版本。

### E02：背景開圖與文件替換

- 證據：[AppController](../ScreenshotApp/AppController.cs) 的 `OpenImage` 只在顯示開檔對話框前確認一次；等待背景載入後直接 `OpenEditor`。`_prompting` 會阻擋新的協調要求，但沒有禁止原文件繼續編輯。
- 驗證：先重現載入等待期間的修改；以受控載入結果驗證成功、取消、失敗及新增修改後的文件保護，避免實際檔案對話框或使用者桌面輸入。

### E03：建置與 CI 品質檢查

- 證據：沒有 CI、本機已有零警告與完整測試基準，開發者只能手動記住驗證步驟。
- 驗證：同一本機入口完成還原、Release 零警告建置與所有測試；靜態驗證 Windows workflow、測試失敗與建置警告確實導致非零結束碼。遠端 CI 若未執行，必須明確記為尚未驗證。

### E04：長操作序列

- 證據：右鍵選單增加複合命令與重新編號，現有測試主要驗證單一步驟或短序列；先確認涵蓋缺口再加入有獨立期望值的測試。
- 驗證：固定種子與獨立狀態模型檢查新增／刪除／裁切／復原／重做／匯出／分支／容量淘汰的組合；不改變產品行為、不以實作本身計算期望值。

## 排除與暫緩

- 裁切外灰色區域是否可命中或繪圖：仍有未決定的產品行為，不處理。
- OCR、馬賽克、釘選、安裝程式、自動更新、全面改寫 MVVM：不屬於本次可驗證的工程改善。
- xunit v3 遷移：現有 ADR 指定 xunit 2.9.x、沒有已知漏洞且目前測試通過；先做資料保護與品質檢查，再評估遷移成本與收益。
- 多螢幕混合 DPI、實體顯示器拔除、乾淨 Windows、Office 貼上及登出驗收：需要外部環境或人工操作，保留原驗收狀態。
- 不執行發佈腳本；不以歷史煙霧結果宣稱本次修改已通過桌面驗收。

## 完成紀錄

精確 commit 可由 `git log -- docs/GOAL_BACKLOG.md` 追溯，避免在同一 commit 內自我引用尚未產生的 SHA。

### E01 — 已完成（第 1 輪）

- 根因：確認流程把「舊版本匯出成功」誤當成「目前文件沒有未匯出的修改」，使控制器可繼續關閉／替換。
- 修正：複製與儲存完成後再呼叫 `HasUnexportedChanges`，提交尚未完成的文字輸入並檢查版本。原有四個選項、無修改時不詢問、一般背景匯出期間可繼續編輯等行為保留。
- 回歸：新增 13 個測試，涵蓋兩種匯出、新標註、尚未提交的文字、成功／失敗、復原回匯出版本、捨棄／取消、未修改文件；只有對話框與匯出副作用被替換，判斷走產品的同一方法。
- **紅燈**：僅抽出副作用接縫、保留原判斷時，`ConfirmDiscard_EditDuringExport` 的兩個案例均失敗（預期 false，實際 true），結束碼 1。
- **綠燈**：`EditorWindowTests|DirtyTrackingTests` **30/30 通過**；Release `-warnaserror` 建置 **0 警告、0 錯誤**；因這是截圖、開檔、關閉、結束共用的保護流程，追加完整測試 **946/946 通過、0 跳過**（基準 933＋13）。`git diff --check` 通過。
- 命令：`dotnet test ScreenshotApp.Tests/ScreenshotApp.Tests.csproj -c Release --filter "FullyQualifiedName~EditorWindowTests|FullyQualifiedName~DirtyTrackingTests"`；`dotnet build ScreenshotApp.sln -c Release --nologo -warnaserror`；`dotnet test ScreenshotApp.sln -c Release --no-build --nologo`。TRX 位於 `artifacts/engineering-goal/e01-{red,green,full}/`。
- 文件：更新 ADR-0016、架構、已知限制與 CHANGELOG。未操作使用者剪貼簿、未顯示編輯視窗，未宣稱桌面煙霧驗證完成。
- Commit 主旨：`phase-04: 保護確認匯出期間新增的編輯內容`。

### E02 — 已完成（第 1 輪）

- 根因：開檔對話框前的捨棄確認不涵蓋背景解碼期間的新編輯；原本解碼成功就直接替換文件。
- 修正：已有編輯器時，控制器透過 `LoadReplacementAsync` 載入並比較前後版本；新標註或尚未提交的文字仍未匯出時中止替換。原已確認捨棄且沒有再變更、復原回原版本、新內容已匯出的情況仍照常完成；失敗原因與待輸入文字均保留。
- **紅燈**：只將載入移到委派接縫、維持原判斷時，新標註與新文字兩個案例均失敗（預期取消，實際回傳可替換的圖片），結束碼 1。
- **綠燈**：新增 7 個案例，相關 `EditorWindowTests|ImageFileLoaderTests|DirtyTrackingTests` **65/65 通過**；Release `-warnaserror` 建置 **0 警告、0 錯誤**。控制器與編輯器共同改動，執行完整測試 **953/953 通過、0 跳過**（前項 946＋7）。`git diff --check` 通過。
- 命令：`dotnet test ScreenshotApp.Tests/ScreenshotApp.Tests.csproj -c Release --filter "FullyQualifiedName~EditorWindowTests|FullyQualifiedName~ImageFileLoaderTests|FullyQualifiedName~DirtyTrackingTests"`；完整建置／測試同 E01。TRX 位於 `artifacts/engineering-goal/e02-{red,green,full}/`。
- 驗證的是實際文件保護判斷與控制器接線；沒有執行檔案對話框或桌面煙霧驗證，未更改既有人工驗收狀態。
- 文件：ADR-0016、架構、已知限制、CHANGELOG。Commit 主旨：`phase-04: 保護背景開圖期間新增的編輯內容`。
