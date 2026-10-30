# 可自主執行的大型 Goal

> **推薦先執行 G1：讓應用程式協調流程具有可重現的失敗恢復測試。**
> 本文件是候選任務書，不代表已授權啟動任何 Goal。這次只更新本文件，沒有實作候選工作。

## 1. 盤點基準與方法

- 盤點日期：**2026-10-29**（台灣時間）。
- 基準：`77297367289b5a91be15ed42f1d505498f4622fd`，產品版本 `1.1.0`；`v1.1.0` 標籤指向 `6c7c103`，與後續文件／授權變更的 HEAD 不同。
- 檢查範圍：198 個已追蹤檔案的清單、兩個專案與建置設定、53 個產品 C# 檔案的測試涵蓋結果、38 個測試／輔助 C# 檔案的分布、7 支 PowerShell 腳本、規則與產品文件、17 份 ADR、計畫／交接／失敗紀錄、全部 17 筆 Git 歷史及可存取的遠端 Issue／Actions。
- 方法：依本專案探索規則使用既有 CodeGraph 取得相關原始碼與呼叫關係，配合文件、組態、Git 與實際建置／測試驗證。沒有建立新索引。
- 原有 `docs/AUTONOMOUS_GOAL.md` 是 10 月 24 日的未追蹤草稿。本版重新核對數據，保留有證據支持的方向，調整範圍與驗收方式；不沿用未重跑的進階分析器警告數。
- 以下區分 **已驗證**（這次命令或目前原始碼）、**歷史證據**（既有紀錄）、**推論**（工程判斷）、**尚未驗證**（不可當成通過）。

「完整檢查」在此表示涵蓋上述所有面向，不代表逐條 Windows 行為已重新實機驗收，也不是完整安全性稽核。本次沒有執行桌面煙霧腳本、發佈、登入登出、混合 DPI 切換或乾淨機器驗證。

### 1.1 架構與模組邊界

目前是單一 WPF 應用程式專案加一個 xunit 測試專案，資料夾代表職責，並非多個可獨立部署的服務。產品不引用 NuGet 套件；測試引用 Microsoft.NET.Test.Sdk 17.14.1、xunit 2.9.3、xunit.runner.visualstudio 3.1.4。

| 區域 | 目前邊界與證據 | 對 Goal 選擇的意義 |
|---|---|---|
| 啟動／協調 | [AppController.cs](../ScreenshotApp/AppController.cs) 第 38–50、234–414 行直接建立平台服務／視窗，管理開圖、截圖、關閉、結束、忙碌與提示；[App.xaml.cs](../ScreenshotApp/App.xaml.cs) 組裝生命週期 | 協調決策與副作用耦合；G1 可沿既有行為建立最小測試接縫 |
| 擷取 | [CaptureFlow.cs](../ScreenshotApp/Capture/CaptureFlow.cs) 第 7–16、49–75 行，以 `ICaptureEnvironment` 隔離快照與覆蓋層；`finally` 恢復狀態 | 已有可沿用的設計，不需要新 DI 框架 |
| 編輯模型 | [ImageDocument.cs](../ScreenshotApp/Editor/Model/ImageDocument.cs) 第 34–54 行、[UndoRedoStack.cs](../ScreenshotApp/Editor/Commands/UndoRedoStack.cs) 第 15–84 行、[EditCommands.cs](../ScreenshotApp/Editor/Commands/EditCommands.cs) | 不可變標註、命令歷史、裁切與匯出版本已有明確契約；適合 G3 的多步操作驗證 |
| 編輯視窗 | [EditorWindow.xaml.cs](../ScreenshotApp/Editor/EditorWindow.xaml.cs) 共 1,276 行；檢視決策、右鍵選單、匯出與 WPF 接線同檔 | G4 可針對既定決策分離，不必全面改寫 MVVM |
| 輸出 | [ImageRenderer.cs](../ScreenshotApp/Export/ImageRenderer.cs) 第 15–48 行，與 [AnnotationPainter.cs](../ScreenshotApp/Editor/Rendering/AnnotationPainter.cs) 共用繪製；先整張合成，再取裁切像素 | 這是已驗證的正確性限制，不把「只畫裁切範圍」當成可直接自主最佳化 |
| 平台／設定 | `Platform/` 集中 Win32、SafeHandle、快捷鍵、系統匣、登錄；`Settings/` 負責設定與視窗；[SettingsStore.cs](../ScreenshotApp/Settings/SettingsStore.cs) 有可指定的檔案位置 | 測試必須沿用隔離目錄與專用登錄子機碼，不能接觸使用者設定 |

[ARCHITECTURE.md](ARCHITECTURE.md) 第 27–61 行已明列非單向相依：`Platform` 引用 `Editor/Model` 的像素型別與 `Settings` 的快捷鍵型別，`Model`／`Commands` 互相引用，視窗回呼 `AppController`。這些關係目前有明確用途；沒有證據支持為了消除圖上的循環而拆專案。

### 1.2 AGENTS.md／CLAUDE.md／README

- **已驗證**：[AGENTS.md](../AGENTS.md) 存在，儲存庫內沒有 `CLAUDE.md`，也沒有下層另行覆寫的代理規則。沒有必要為補齊檔名而建立重複規則。
- [README.md](../README.md) 的建置與測試命令可執行，版本與專案檔一致；下載連結指向公開 GitHub 儲存庫。
- 產品行為由 [ScreenshotApp.Spec.md](ScreenshotApp.Spec.md) 與已接受的 ADR 約束；[KNOWN_LIMITATIONS.md](KNOWN_LIMITATIONS.md) 另列裁切、輸入格式、單檔啟動與驗證限制。
- [task_plan.md](../.planning/2026-10-18-screenshotapp-v1/task_plan.md) 受 attestation 保護。候選工作使用自己的進度紀錄，不修改該檔或重啟歷史 Phase。
- 本次對話明訂「要求時才 commit」，因此以下五份任務書都以**未提交的可審查變更**為交付，不自動 commit、合併、推送或建立 PR。
- 歷史計畫包含早期階段的類別／路徑名稱；不能把那些名稱直接當成現存測試。現在的檔案與可執行測試才是驗證入口。

### 1.3 Build 與 CI

**已驗證**：

- 本機為 Windows x64，.NET SDK **10.0.403**、MSBuild **18.9.15**、.NET／Windows Desktop Runtime **10.0.14**。
- [應用程式專案](../ScreenshotApp/ScreenshotApp.csproj) 啟用 WPF、有限範圍的 Windows Forms、Nullable、LibraryImport 所需的 unsafe，以及繁中附屬組件；沒有另設 `TreatWarningsAsErrors`。`WFO0003` 有具理由的既有抑制，不能為「零抑制」而破壞 WPF 的 DPI 設定方式。
- [nuget.config](../nuget.config) 只保留 nuget.org；沒有 `global.json`、`Directory.Build.props`、`.editorconfig` 或 `.gitattributes`。
- 沒有 `.github/`、`.gitea/` 或其他已追蹤 CI workflow。
- [Publish-Release.ps1](../tools/Publish-Release.ps1) 第 19、26、50–56 行固定輸出至 `publish/`，會先刪除同名舊產物。本次只讀取與剖析，沒有執行；也不把它放進一般 Goal 的預設驗證。
- `tools/` 的 7 支 PowerShell 腳本全部可通過語法剖析；語法通過不等於桌面操作正確。

### 1.4 本次測試與涵蓋率

實際執行：

~~~powershell
dotnet build ScreenshotApp.sln -c Release --nologo
dotnet test ScreenshotApp.sln -c Release --no-build --nologo --collect "Code Coverage;Format=cobertura" --logger "trx;LogFileName=baseline.trx" --results-directory $auditRoot
~~~

`$auditRoot` 是本次建立的獨立暫存目錄，不是產品資料目錄。

| 項目 | 本次結果 |
|---|---|
| Release 建置 | **0 警告、0 錯誤** |
| 測試 | **933 passed／0 failed／0 skipped**；收集器顯示測試執行約 4 秒 |
| 產品原始碼行涵蓋率 | **1,813／2,803＝64.680699…%** |
| 產品原始碼分支涵蓋率 | **766／1,422＝53.867791…%** |
| AppController | 行 **21／179（11.7%）**；分支 **2／114（1.8%）** |
| EditorWindow.xaml.cs | 行 **266／676（39.3%）**；分支 **90／402（22.4%）** |
| EditorSession | 行 **367／386（95.1%）**；分支 **259／286（90.6%）** |
| CaptureFlow／CoordinateMapper／命令與歷史 | 行與分支皆 **100%** |
| App.xaml.cs／SettingsWindow.xaml.cs／SelectionController.cs | 本次行涵蓋率 **0%**；不表示沒有歷史實機驗證 |

統計口徑：從 Cobertura 的 class-level lines 依「已追蹤的產品 C# 路徑＋行號」去重；重複紀錄取最大 hits 與分支命中／總數，排除測試與產生碼，這次納入全部 53 個產品 C# 檔案。XML 根節點的 **84.5%** 同時包含測試等內容，不能稱為產品涵蓋率。未來若收集器對同一行產生不同分支分母，必須先解釋差異，不能靜默合併。

`64.7%`／`53.9%` 是顯示用四捨五入數字，不能直接拿來當不低於基準的精確下限，否則本次基準本身就可能不通過。

測試已有良好隔離慣例：[StaRunner.cs](../ScreenshotApp.Tests/TestSupport/StaRunner.cs) 提供 STA、[HotkeyServiceTests.cs](../ScreenshotApp.Tests/HotkeyServiceTests.cs) 使用測試快捷鍵、[CaptureServiceTests.cs](../ScreenshotApp.Tests/CaptureServiceTests.cs) 不保存桌面內容。現有測試沒有 `Skip=` 或 `[Trait(...)]` 分類；因此不能假定它們都可在無互動桌面的 runner 上執行。

### 1.5 TODO／FIXME、已知 Issue 與失敗紀錄

- **已驗證**：已追蹤的 `.cs／.xaml／.ps1／.csproj` 中，`TODO／FIXME／HACK／XXX` 標記為 **0**。這不代表沒有未完成事項。
- 公開 [GitHub Issues](https://github.com/anthonywang6503/ScreenshotApp/issues) 的 API 查詢 `state=all` 回傳空陣列；[Actions](https://github.com/anthonywang6503/ScreenshotApp/actions) 的 workflows 與 runs 都是 **0**。公開 main 與本機基準 SHA 相同。
- `origin` 的 SSH 別名實際指向 github.com；`upstream` 指向的 `wanghy6503/ScreenshotApp` API 回傳 **404**，只能標示「此登入狀態無法確認」，不能推論該遠端沒有 Issue、已刪除或從未失敗。
- **歷史證據**：計畫的 `Errors Encountered` 有 **45 筆**，混合已修正問題、工具問題與當時的診斷過程，不能當成 45 個未修缺陷。

| 紀錄 | 現況與工程意義 |
|---|---|
| Delete 誤判失敗 | [ACCEPTANCE.md](ACCEPTANCE.md) 第 170–172 行與 [findings.md](../.planning/2026-10-18-screenshotapp-v1/findings.md) 第 157 行已結案：圖樣本來就有 560 個符合「偏紅」條件的像素，產品刪除有執行；不是現在仍待修的 Delete bug |
| 自動化誤報通過／干擾桌面 | findings 第 125、131–157 行及計畫錯誤表記錄輸入未生效仍通過、前景被終端機搶走、剪貼簿序號漏更新；說明測試工具也需要負向測試 |
| GDI 趨勢誤判 | [ACCEPTANCE.md](ACCEPTANCE.md) 第 242 行：單次尖峰不等於持續洩漏；改比較前後中位數後通過，原始 CSV 仍保留 |
| 真實產品修正 | DIB 取代會累積 GDI 物件的圖片寫法、整張合成後裁切以維持像素一致；不能為了簡化而復原舊做法 |
| 未決定的產品行為 | findings 第 168 行：裁切外灰色區域是否禁止命中與繪圖，仍待使用者決定；所有 Goal 均排除 |
| 尚未完成的人工驗收 | [ACCEPTANCE.md](ACCEPTANCE.md) 第 305–320 行：不同 DPI、實體多螢幕、拔除顯示器、乾淨 Windows 等；MT-08 的新剪貼簿格式需再確認，MT-10 是 Agent 量測完成但使用者尚未確認 |

四支煙霧腳本合計 **4,135 行**；以 PowerShell AST 計算有 **155 個函式定義**（包含巢狀函式），37 個名稱重複，其中 27 組的函式本文不同。本文差異未必是缺陷，須依各 Phase 契約判斷，不能直接選一份覆蓋全部。

歷史最近結果須分版本：v1.1.0 的 Phase 1／2／3 分別 31／31、38／38、43／43；Phase 4 的 25／25 沿用 v1.0.0，沒有隨 v1.1.0 重跑。本次沒有重新執行這些腳本。

### 1.6 最近 Git 歷史

| 日期／commit | 主要內容 | 與候選工作的關聯 |
|---|---|---|
| 10-24 `7729736` | README 公開下載位置與 GitHub Release 紀錄 | 公開入口已具備，CI 尚未建立 |
| 10-24 `1d8a0c8` | Apache 2.0、授權檔與 README | 品質檢查須尊重既有授權與發佈規則 |
| 10-24 `c3293cf` | 規格移入 docs | 引用以目前路徑為準 |
| 10-19 `ad3563f` | v1.1.0 發佈版煙霧證據 | 不把舊產物結果冒充新變更已驗證 |
| 10-19 `6c7c103` | 右鍵選單、再製／重新編號、裁切填滿；新增 39 測試 | 編輯狀態組合增加，支持 G3／G4 |
| 10-18 `e608611` | v1.0.0、錯誤說明、發佈與診斷 | 支持故障路徑與測試工具驗證 |
| 10-18 `6bfffbd`、`0a765b4` | 圖片 I/O、未匯出保護、編輯核心 | G1／G3 的既定行為基礎 |

## 2. 選擇與排序

評分是工程判斷，並非效益的精確量測。三個因子各 1–5 分：

- **價值**：1 為局部整理，3 為持續維護效率，5 為直接保護重要使用者工作或關鍵正確性。
- **可驗證性**：1 主要靠主觀評估，3 仍受環境限制，5 可在本機以固定輸入、命令與失敗注入判定。
- **自主工作深度**：1 為單次小修，3 為數個相依步驟，5 為可持續經過基準、設計、測試、重構、反向驗證與收斂的多輪工程工作。

| 排名 | Goal | 價值 | 可驗證性 | 深度 | 乘積 | 評分依據 |
|---|---|---:|---:|---:|---:|---|
| 1 | **G1 協調流程的失敗恢復與可測試邊界** | 5 | 5 | 5 | **125** | 未匯出資料的保護決策集中於低涵蓋區，規格明確，可用可等待的流程與受控副作用驗證 |
| 2 | **G2 可重現的本機品質閘門與 CI 設定** | 4 | 5 | 5 | **100** | 尚無 CI；建置、測試分類、涵蓋率口徑、負向驗證可共用同一本機入口 |
| 3 | **G3 編輯核心的模型式回歸測試** | 4 | 5 | 4 | **80** | 既有高涵蓋率不能證明長操作序列正確；命令、裁切與匯出版本有可獨立計算的契約 |
| 4 | **G4 編輯視窗決策分離與離線接線驗證** | 4 | 4 | 4 | **64** | 1,276 行視窗承擔多種決策；可分離計算，但 WPF 焦點／版面接線仍使驗證較難 |
| 5 | **G5 煙霧判定器的離線回放與負向測試** | 3 | 5 | 4 | **60** | 有明確的誤判歷史；本 Goal 限於測量與安全判定，不要求等待桌面閒置 |

G1 的結論是「測試保護不足且影響重要」，**不是已證明目前會遺失資料**。G1／G2／G3／G5 的方向信心高；G4 的效益信心中高、重構風險較高。

未入選：OCR／馬賽克／釘選等新功能、裁切外命中行為改變、安裝程式／自動更新、真實混合 DPI 與乾淨機器驗收、遠端發佈／簽章、為追逐最新版本而升級套件、全面改寫 MVVM、以減少行數為唯一目標的重構。這些工作涉及未定需求、額外環境或缺乏足夠收益證據。

## 3. 五份任務書共同的交付邊界

以下每段 `/goal` 都可單獨複製執行；一次只選一個。新腳本、新測試分類與新的檔名是**該 Goal 要建立的產物**，目前不存在，不能把下方規劃命令當成已跑過。

- 任務 0 固定實際起始 HEAD、工作區狀態、相關檔案雜湊與測試清單。若已執行其他 Goal，重新量測並解釋基準差異，不回退他人的改動。
- 正確性與使用者資料隔離優先；以各 Goal 的情境表、既有規格及 ADR 驗收，不以行數、測試數或涵蓋率單獨代替行為。
- 不刪除既有測試、不放寬斷言、不新增 Skip、不改測試篩選讓失敗消失，不以替身取代被測的決策邏輯。只替換外部副作用。
- 檔案邊界同時檢查追蹤檔案差異與新增路徑；`git diff` 看不到未追蹤檔案，須搭配 `git status --short` 與任務 0 清單。
- 用 `docs/goals/gN/PROGRESS.md` 記錄迭代與續跑位置，`BLOCKED.md` 記錄未解事項，`VALIDATION.md` 保存命令、結束代碼、結果與限制；N 是所選 Goal 的編號。這些檔案在執行該 Goal 時才建立。
- 不允許未完成的必要驗收以「尚未驗證」換成完成。未能通過時繼續可獨立工作並回報未完成；同一失敗連續三次應重新診斷或切換可做項目，不能無限重試。
- 真實桌面操作、遠端 CI 執行、發佈與人工 MT 不屬於這五項本機工程交付；原有驗收狀態保留。離線替身測試不得冒充這些項目通過。
- 完成後只交付變更、證據與尚未解決的事項；不 commit、合併 main、推送、PR、標籤、發佈、修改全域工具設定或使用者記憶。

## 4. G1：協調流程的失敗恢復與可測試邊界

### 完整 /goal 指令

~~~text
/goal 讓 ScreenshotApp 的應用程式協調決策能在不開啟真實視窗、不操作使用者剪貼簿或設定的測試中，驗證未匯出保護、忙碌拒絕、失敗恢復與設定套用；維持既有產品行為。

1. 執行預設與目的
以 docs/ScreenshotApp.Spec.md、docs/adr/0009-app-state-machine.md、0016-unexported-changes-protection.md、0004-settings-persistence.md 及 docs/ARCHITECTURE.md 為契約。目標是保護既有資料與工作流程，不增加功能。單代理執行；正確性與隔離優先於涵蓋率。接手時先讀 docs/goals/g1/PROGRESS.md，完成每輪即更新；不確定事項寫 BLOCKED.md。

2. 界限
只允許修改 ScreenshotApp/AppController.cs、App.xaml.cs，新增 ScreenshotApp/Coordination/ 下的最小 internal 邊界與正式轉接器；Editor/EditorWindow.xaml.cs 只允許拆出 ConfirmDiscardAsync 的確認選項決策及必要接線；既有 CopyImageAsync/SaveImageAsync、焦點與輸出繪製保持不變。測試限新增 ScreenshotApp.Tests/AppController*Tests.cs、TestSupport/Controller* 與必要的 StaRunner 非同步支援。文件限 docs/goals/g1/、docs/ARCHITECTURE.md、docs/adr/ 下相關新 ADR 及 CHANGELOG.md。其餘唯讀；既有測試因接線改型別可做必要機械修改，必須保留原斷言與案例。

3. 任務 0
讀 AGENTS.md，記錄 HEAD、git status、既有測試名稱與相關雜湊。2026-10-29 在 7729736 的基準是 SDK 10.0.403、Release 0 警告、933 通過且無失敗／跳過；AppController 行 21/179、分支 2/114。執行 dotnet build ScreenshotApp.sln -c Release 與 dotnet test ScreenshotApp.sln -c Release --no-build；若基準變了先記錄並排除環境問題。開 feature/phase-08-controller-workflow 分支；若同名存在先確認是否為此 Goal 的續作。寫不超過 10 行的目標、順序與風險回執。

4. 工作
先建立情境與斷言對照表，再沿 CaptureFlow 的最小注入方式分離外部副作用；非同步決策提供可等待的 Task，async void 只留 UI 事件轉接。先鎖目前行為，每輪只搬一個流程。
測試全部以下情境：Resident/Editing 起始；新截圖成功、取消、失敗；詢問先於隱藏視窗；複製成功/失敗、儲存成功/取消/失敗、捨棄、取消的結果如何控制後續操作；開檔對話框取消與解碼失敗不取代原文件；提示/匯出/模態對話框忙碌時新的截圖、開圖、關閉、結束皆被忽略；例外後解除忙碌；關閉編輯器回 Resident、結束才要求 Shutdown；登出有未匯出內容才取消並排入詢問，不真的登出；第二個執行個體在既定狀態下喚醒；快捷鍵/啟動設定失敗保留對應舊值，其他設定依現況套用，設定讀取失敗不覆寫有效檔。
確認選項到繼續/中止的對應必須是正式共用的決策，不能只 mock 一個 bool。用可控制完成時間的 Task 與事件紀錄驗證順序和重入，不用 Sleep 猜時序。測試使用真實 AppStateMachine、文件與協調決策，僅替換視窗、對話框、排程、平台註冊與 I/O 副作用。新測試加 Trait("Goal","G1")。正式啟動必須接回真實轉接器，新增接線驗證。
在自有隔離副本分別暫時破壞忙碌拒絕與確認失敗的中止分支，證明相關測試失敗；恢復後通過。更新文件與 CHANGELOG，保存原始 TRX/Cobertura 及完整命令。

5. 規則
不引入套件、DI 容器或通用框架；不改 XAML、產品規格、裁切外命中、執行緒契約、既有測試斷言、task_plan.md 或歷史證據。不操作真實桌面或執行發佈。保留既有未提交工作；不 commit、合併、推送、PR 或打標籤。同一驗收連敗三次就重新診斷並記錄，不能刪測試或降低要求。

6. 完成條件
情境表每列都對應已通過的實際測試；負向注入兩項皆有紅到綠證據；正常執行不顯示視窗、不觸碰使用者資料。Release 0 警告；完整測試保留任務 0 的所有案例，failed=0、skipped=0；Goal=G1 篩選結果 total>0，不能空跑。回報實際命令、數量與副作用檢查，BLOCKED.md 無未解必要項目才可完成。真實 UI 與登出仍依既有驗收狀態，不宣稱已由替身驗證。
~~~

### Acceptance Criteria

| 驗收面向 | 客觀條件 |
|---|---|
| 取代與取消 | 確認失敗、取消開檔／截圖、解碼失敗均不取代原文件；先前成功匯出造成的版本變化仍遵守既有規則 |
| 重入與恢復 | 三種忙碌來源 × 四種操作逐一驗證；可控制的非同步中途再送要求，副作用呼叫次數不增加；例外後可接收下一次要求 |
| 生命週期 | 關閉、結束、第二執行個體與登出取消／排程各有狀態與呼叫順序斷言 |
| 設定 | 快捷鍵、啟動登錄與設定存檔各自失敗時的欄位保留、訊息與繼續套用行為符合現況 |
| 測試可信度 | 真實協調邏輯被執行；平台才用替身；兩項受控破壞確實使測試失敗 |
| 交付 | 既有測試全集保留、0 失敗／跳過、Release 0 警告；所有必要情境通過，沒有待補的必要驗收 |

涵蓋率是輔助證據：列出 `AppController` 與抽出類別的原始分子／分母，確認測到正式使用的決策，不能靠把未測程式搬到別檔讓數字改善。

### Validation Commands

下列建置與完整測試已在本次基準驗證；`Goal=G1` 是執行本 Goal 時新增的分類，必須在新增測試後執行：

~~~powershell
dotnet build ScreenshotApp.sln -c Release
dotnet test ScreenshotApp.sln -c Release --no-build
dotnet test ScreenshotApp.sln -c Release --no-build --filter "Goal=G1" --logger "trx;LogFileName=g1.trx" --results-directory artifacts/goals/g1
dotnet test ScreenshotApp.sln -c Release --no-build --collect "Code Coverage;Format=cobertura" --results-directory artifacts/goals/g1/coverage
git diff --check
~~~

每個命令必須檢查結束代碼；TRX 還需檢查 total > 0、failed = 0、notExecuted = 0。兩項受控破壞時，對應測試命令必須非 0，還原後為 0。

### 限制條件／Anti-goals

不全面重寫 App、不改未匯出判斷或選項、不順便修正未證實的行為、不改善設定視窗外觀；`EditorWindow` 在本 Goal 只移出確認選項判斷並提供必要接線。不能把真實協調器整個 mock 掉，也不能拿低涵蓋率當作現存缺陷證明。

### 預期 Failure Modes

| 失敗模式 | 處理與驗證 |
|---|---|
| 測試無法等待 async void，例外漏到 Dispatcher | 保留薄 UI 轉接、決策改為 Task；驗證例外與 finally 復原 |
| STA 等待造成死鎖 | 使用能處理 Dispatcher 的非同步測試支援；不要直接在同一 UI 執行緒阻塞等待 |
| 測試替身掩蓋正式接線錯誤 | 把正式組裝與控制器行為分開測；檢查真實轉接器確實被使用 |
| 提示結束與載入開始之間的 busy 空隙 | 以可控制 Task 分段插入重入要求，驗證每一段 |
| 範圍擴張成大型抽象框架 | 只為現有副作用建立邊界；沒有測試／正式呼叫需求的介面不新增 |

### 工作範圍與複雜度

**高複雜度，估計 5–7 輪**：契約表 → 最小接縫 → 取代／取消 → 重入／生命週期 → 設定失敗 → 正式接線與負向驗證。主要風險是非同步順序，並非產品需求。可單獨執行，不依賴 G2；測試收集器已存在。

## 5. G2：可重現的本機品質閘門與 CI 設定

### 完整 /goal 指令

~~~text
/goal 為 ScreenshotApp 建立可重現且失敗不會被隱藏的本機品質閘門，以及呼叫同一入口的 GitHub Actions 設定；完成範圍是本機驗證與可審查設定，不包含推送、啟用遠端 CI 或發佈。

1. 執行預設與目的
遵守 AGENTS.md、現有 csproj、nuget.config 與 docs/adr/0017-release-packaging.md。維持既有分析器規則與產品行為，只把已要求的 Release 零警告、測試、版本一致性變成可執行檢查。採用本機已驗證的 .NET 10 SDK，不追逐最新套件。續跑先讀 docs/goals/g2/PROGRESS.md，每輪更新；阻礙寫 BLOCKED.md。

2. 界限
只允許新增/修改 global.json、Directory.Build.props、.github/workflows/ci.yml、tools/quality/、tools/Invoke-QualityGate.ps1，以及 ScreenshotApp.Tests/ 中分類所必需的 Trait；既有測試本文、名稱與斷言不改。文件限 README.md、docs/ARCHITECTURE.md、docs/goals/g2/、相關新 ADR、CHANGELOG.md。產品原始碼、兩個 csproj 的套件與版本、nuget.config、Publish-Release.ps1、publish/、歷史證據及 task_plan.md 唯讀。工具不用全域安裝。

3. 任務 0
記錄 HEAD、工作區與既有檔案雜湊，開 feature/phase-08-quality-gate 分支或確認同名續作。基準 7729736 在 2026-10-29 用 SDK 10.0.403 建置為 0 警告、933 passed/0 failed/0 skipped；產品行 1813/2803、分支 766/1422。執行 dotnet build ScreenshotApp.sln -c Release 與帶 Code Coverage;Format=cobertura 的 dotnet test，保存測試全集、原始 XML 及 SDK 資訊。不同 HEAD 或 SDK 先量測與說明差異，不照抄數字。回執不超過 10 行。

4. 工作
建立 tools/Invoke-QualityGate.ps1 -Mode Local|CI -ResultsDirectory；預設 Local。明確檢查每個 native 命令的結束代碼。步驟為 SDK 前提、restore、Release warnings-as-errors build、測試、產品涵蓋率、來源版本/manifest 一致、所有工具腳本語法及 workflow 靜態契約。任一錯誤、空測試或缺報告須非 0。
逐項辨識依賴實際桌面/全域快捷鍵的測試，才加 Category=Desktop，記錄原因；Local 跑全集，CI 排除明列 Desktop。CI 執行集合與排除集合無交集，聯集等於 Local 全集；不把純 WPF STA 測試一律排除，不用 Skip。
以現有收集器實作 tools/quality/ 下的產品涵蓋率讀取器，排除測試/產生碼而保留未執行產品檔，新增產品檔不得消失。用合成 XML 測多 class 重複行、零分支、全未命中、缺失檔案/報告及不同分支分母。任務 0 在同一 SDK 分別量測 Local/CI，將精確分子分母與模式、來源清單記入 tools/quality/coverage-baseline.json；不得用四捨五入顯示值、降低下限或排除未測產品程式碼達標。
workflow 僅 push/pull_request、Windows runner、contents: read，設定經核對的 SDK，呼叫上述入口的 CI 模式；不含 secrets、部署、發布套件、發佈資產或寫入遠端。靜態契約測試檢查這些條件，明確保留「GitHub 解析與 runner 執行尚未驗證」。
建立 tools/quality/Test-QualityGate.ps1，將單一失敗注入隔離副本：編譯警告、失敗測試、空選取、版本不一致、PowerShell 語法錯、缺失/低於門檻的涵蓋率。每項必須令正式閘門失敗，恢復後正常成功；模擬資料只能用於讀取器單元測試，不能替代正常通過的真實建置/測試證據。

5. 規則
不全面啟用 latest-all、不大量抑制警告、不改執行期或套件、不重寫測試、不執行 Publish-Release、不碰使用者資料、不改遠端設定。保留其他工作；不 commit、合併、推送、PR、標籤或部署。連敗三次重新診斷，不以 continue-on-error 或忽略 exit code 變綠。

6. 完成條件
Local 與 CI 模式均在本機執行成功，Local 保留全部原測試且 0 failed/0 skipped，CI 的排除項有完整帳目；所有負向案例確實非 0，正常恢復為 0。涵蓋率口徑與精確門檻可由 XML 重算；版本與語法檢查有效；workflow 靜態契約通過。VALIDATION.md 附原始命令、數量、SDK、失敗注入結果與遠端未驗證範圍；無未解必要項目才完成。
~~~

### Acceptance Criteria

1. **單一入口真實執行**：Local／CI 都呼叫同一支正式腳本；restore、build、test、coverage、版本與語法任一步失敗都使最外層命令非 0。
2. **測試沒有消失**：Local 的測試識別集合包含任務 0 全集；CI 執行集合與明列排除集合互斥、合計等於全集。排除不等於通過，CI 報告同時列出兩者。
3. **涵蓋率沒有美化**：產品分母包含未命中與新增來源；錯誤／空報告不可成功；去重與零分支案例通過讀取器測試；下限按相同 SDK、模式的精確基準保存。
4. **至少七類負向檢查**：警告、測試失敗、零案例、版本差異、腳本語法、缺報告、涵蓋率不足各自能使正式入口失敗；不得只測一個永遠回傳失敗的假入口。
5. **CI 設定邊界**：唯讀權限、無 secrets／發佈／部署、指向同一本機入口；只宣告靜態契約已驗證，遠端解析與執行不在完成判定內。
6. **產品不變**：產品來源、套件、版本與現存發佈產物雜湊不變。若發現需要修產品才能通過的既有缺陷，記錄阻礙，不擴張此 Goal。

### Validation Commands

以下是**本 Goal 要建立的腳本介面**；本次未建立或執行。任務 0 使用第 1.4 節的現存命令取得基準，之後才能執行：

~~~powershell
pwsh -NoProfile -File tools/Invoke-QualityGate.ps1 -Mode Local -ResultsDirectory artifacts/goals/g2/local
pwsh -NoProfile -File tools/Invoke-QualityGate.ps1 -Mode CI -ResultsDirectory artifacts/goals/g2/ci
pwsh -NoProfile -File tools/quality/Test-QualityGate.ps1
git diff --exit-code -- ScreenshotApp ScreenshotApp.Tests/ScreenshotApp.Tests.csproj nuget.config tools/Publish-Release.ps1
git diff --check
~~~

前兩項正常結束代碼為 0；自我測試需確認每一負向子程序非 0 才能回傳 0。最後的唯讀檔差異檢查以任務 0 的基準為準；有原有 dirty work 時用當時保存的雜湊，不把使用者的差異當成此 Goal 造成。

### 限制條件／Anti-goals

不在這個 Goal 改善所有靜態分析警告、不升級測試框架、不簽章、不做 release readiness 宣告。CI 本機模式的成功不能證明 GitHub runner 環境相同；此 Goal 交付的是已在本機驗證的閘門與靜態檢查過的設定。

### 預期 Failure Modes

| 失敗模式 | 處理與驗證 |
|---|---|
| PowerShell 沒因 native 命令失敗而停止 | 每步檢查 `$LASTEXITCODE`，以失敗測試驗證最外層非 0 |
| 篩選錯誤造成零測試仍為 0 | 檢查 TRX 與探索清單，不僅相信 dotnet 的結束代碼 |
| 全部測試被誤標 Desktop | 以明列清單、呼叫副作用證據與全集集合核對擋下 |
| XML 含測試組件或產生碼而高估 | 以產品來源路徑、檔案清單與合成 XML 驗證口徑 |
| SDK 更新使精確涵蓋率不同 | 先固定與記錄 SDK；有證據才能重建基準，不自動降門檻 |
| 套件來源暫時無法連線 | 明列為還原環境失敗，不用忽略錯誤假裝品質通過 |

### 工作範圍與複雜度

**高複雜度，估計 5–6 輪**：環境與名單 → 測試分類 → 閘門 → 涵蓋率讀取器 → workflow 靜態契約 → 隔離失敗注入。產品風險低，主要難度是讓「檢查本身出錯」也能被察覺。可單獨執行，沒有 G1 前置依賴。

## 6. G3：編輯核心的模型式回歸測試

### 完整 /goal 指令

~~~text
/goal 為 ScreenshotApp 的編輯命令、復原歷史、裁切與匯出版本建立可重現的模型式測試，找出單一步驟測試難以涵蓋的序列問題；只修正有既定契約與失敗案例證明的缺陷，不增加編輯功能。

1. 執行預設與目的
依 docs/ScreenshotApp.Spec.md、docs/adr/0010-document-model-and-commands.md、0011-shared-annotation-painter.md、0012-canvas-zoom-model.md、0013-text-input-strategy.md、0016-unexported-changes-protection.md。獨立參考模型比對結果，不複製被測實作。先讀 docs/goals/g3/PROGRESS.md 續跑，每輪更新，疑義寫 BLOCKED.md。預設固定種子與小圖，目的為探索操作組合，不做無意義壓力測試。

2. 界限
主要新增 ScreenshotApp.Tests/ModelBased*Tests.cs 與 TestSupport/ModelBased*。只在先有失敗回歸測試且契約明確時，允許最小修正 ScreenshotApp/Editor/EditorSession.cs、Editor/Commands/、Editor/Model/、Capture/CoordinateMapper.cs、Export/ImageRenderer.cs；其餘產品唯讀。文件限 docs/goals/g3/、docs/ARCHITECTURE.md 的測試說明及 CHANGELOG.md。不改規格、XAML、平台服務、套件、既有測試斷言、task_plan.md 或歷史證據。

3. 任務 0
記錄 HEAD、dirty work、基準測試清單與產品雜湊，建立 feature/phase-08-model-regression 分支或確認續作。2026-10-29 的 7729736 是 933 passed/0 failed/0 skipped、Release 0 警告；EditorSession 行 367/386、分支 259/286。先跑 build/test 核對，有差異先解釋。寫不超過 10 行的工作順序與風險。

4. 工作
先用明確的小案例建立獨立參考模型：標註內容/層序、裁切、歷史容量、復原/重做與已匯出狀態；模型不得呼叫產品命令或 mapper 來計算期望值。再加入實際 EditorSession 操作產生器：新增/移動/調整、刪除、層序、文字提交/取消、裁切/還原、清除、再製、反轉箭頭、重新編號、Undo/Redo 與 MarkExported。
逐步檢查來源像素不變、Id 唯一、裁切非空且在原圖內、拖曳預覽/取消不產生文件命令、完成拖曳最多一筆、歷史上限 100、分岔後清除 redo、版本與 dirty 由參考模型算出。超過容量後只能復原到保留歷史的底層，不要求回到已被捨棄的初始狀態。
另外測裁切前全圖輸出的對應像素等於裁切輸出、Undo/Redo 後文件與輸出恢復、不同檢視縮放不改變輸出、舊版本匯出成功不能清除其後修改的 dirty。圖片合成在 STA、背景資料先 Freeze；使用小型合成圖片，不讀取使用者圖片。
加 Trait("Goal","G3")。預設 20 個固定種子，每種子 200 個有效/邊界操作；Extended 模式 100 種子、每種子 1000 步，以測試專用環境變數 SCREENSHOTAPP_MODEL_PROFILE=Extended 選擇。報告每類操作成功、拒絕、取消次數，不能把十萬次 no-op 當涵蓋；所有指定類別與歷史溢出/分岔都要確實到達。預設新增部分以 30 秒為上限，不用減少操作種類來達標。
失敗輸出種子、步序與最小可重放序列；以固定種子重現，再縮減案例。Guid 以建立順序對應邏輯識別，避免比較兩次執行隨機產生的 Guid。修缺陷要先紅後綠；契約不明則記錄阻礙，不替產品做決定。即使未發現缺陷，也要以暫時破壞 redo 清除或 dirty 判斷的受控修改證明新測試能失敗，之後完全還原。

5. 規則
不新增 NuGet 套件，不複製產品演算法充當參考模型，不吞例外，不因不通過而縮窄種子/邊界或放寬斷言。裁切外命中、OCR、格式支援、匯出合成策略均不變。不操作桌面、剪貼簿、使用者目錄或登錄。不 commit、合併、推送、PR、發佈或打標籤。連敗三次重新分析；只復原自己做的暫時破壞，保留其他工作。

6. 完成條件
Default 與 Extended 模式全部通過，報告實際 4000/100000 步與每類操作計數；同種子可重放，所有不變式均有有效斷言。至少一項受控破壞有紅到綠證據；發現的明確缺陷有最小回歸案例，沒有缺陷也如實回報。Release 0 警告，保留原測試全集且 failed=0、skipped=0，Goal=G3 total>0。VALIDATION.md 保存命令、種子、步數、時間與原始結果，必要項無未解阻礙才完成。
~~~

### Acceptance Criteria

| 驗收面向 | 客觀條件 |
|---|---|
| 參考模型獨立 | 不呼叫 `UndoRedoStack`、產品命令或 `CoordinateMapper` 計算期望值；期望狀態有固定手算案例驗證 |
| 歷史與版本 | 確實到達容量 100 以上、Undo 後分岔、複合命令、匯出後修改與復原；每步比對模型及產品 |
| 圖片與裁切 | 來源像素雜湊不變，裁切只移除範圍外像素，檢視倍率不改變匯出，重做恢復文件與輸出 |
| 操作覆蓋 | Default 4,000 步、Extended 100,000 步，所有指定操作類別實際到達；成功與 no-op／拒絕分開計數 |
| 重現與成本 | 固定種子可重放；預設新增測試 ≤30 秒；失敗報告含種子與縮減序列 |
| 防止空轉 | 新測試能抓到至少一項暫時植入的錯誤；正常還原後無失敗／跳過，原有測試全保留 |

### Validation Commands

`Goal=G3` 與 `SCREENSHOTAPP_MODEL_PROFILE` 是本 Goal 要新增的**測試介面**；現在尚不存在。

~~~powershell
dotnet build ScreenshotApp.sln -c Release
if ($LASTEXITCODE -ne 0) { throw "Release 建置失敗" }
$previousModelProfile = $env:SCREENSHOTAPP_MODEL_PROFILE
try {
    $env:SCREENSHOTAPP_MODEL_PROFILE = "Default"
    dotnet test ScreenshotApp.sln -c Release --no-build --filter "Goal=G3" --logger "trx;LogFileName=default.trx" --results-directory artifacts/goals/g3/default
    if ($LASTEXITCODE -ne 0) { throw "Default 模型測試失敗" }
    $env:SCREENSHOTAPP_MODEL_PROFILE = "Extended"
    dotnet test ScreenshotApp.sln -c Release --no-build --filter "Goal=G3" --logger "trx;LogFileName=extended.trx" --results-directory artifacts/goals/g3/extended
    if ($LASTEXITCODE -ne 0) { throw "Extended 模型測試失敗" }
    $env:SCREENSHOTAPP_MODEL_PROFILE = "Default"
    dotnet test ScreenshotApp.sln -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw "完整測試失敗" }
}
finally {
    $env:SCREENSHOTAPP_MODEL_PROFILE = $previousModelProfile
}
git diff --check
~~~

任務 0 與 Default 測試時須確認環境變數未設成 Extended；若使用者原有此變數，保存後暫設 Default，結束還原。TRX 檢查不能只看命令成功，還須確認匹配案例非 0、實際模式、操作計數與 0 跳過。

### 限制條件／Anti-goals

不把 `IsDirty == (History.Version != ExportedVersion)` 這種重複產品公式的斷言當成獨立證明。不做無界圖片解碼 fuzz、不故意耗盡記憶體、不用隨機時鐘種子；不把既有已記載限制「修正」成新需求。

### 預期 Failure Modes

| 失敗模式 | 處理與驗證 |
|---|---|
| 大量操作因 busy 被拒絕，實際沒有探索 | 分開記錄 attempted／executed／rejected，固定前置序列到達每個重要狀態 |
| 超過歷史容量後期待錯誤的初始狀態 | 參考模型保留底層狀態與 100 筆視窗，驗證捨棄語意 |
| Guid 或字型造成跨執行不穩定 | Id 建立對應表；像素比較使用同環境、同次執行的關係，不拿不同機器字型結果硬比 |
| 模型與產品一起寫錯 | 先用手算小案例驗證模型，再做受控錯誤注入 |
| 隨機序列太長而無法診斷 | 保存種子、完整 trace 與縮減版本；縮減器不能漏掉造成問題的前置狀態 |
| 找到規格未定義的差異 | 保留重現證據並記 BLOCKED，不把任一邊擅自指定為正解 |

### 工作範圍與複雜度

**中高複雜度，估計 4–6 輪**：參考模型 → 操作產生器 → 版本／歷史 → 像素關係 → 重放／縮減 → 已證實缺陷收斂。主要產物是測試能力；可能完全不需要修改產品程式碼。工程價值不以「一定找出幾個 bug」衡量。

## 7. G4：編輯視窗決策分離與離線接線驗證

### 完整 /goal 指令

~~~text
/goal 將 ScreenshotApp 編輯視窗中的檢視狀態、選單決策與匯出流程分離為可直接測試的 internal 元件，保留 WPF 事件、焦點、XAML 與可見行為；以特徵測試及未顯示視窗的接線測試證明重構範圍。

1. 執行預設與目的
依 docs/adr/0002-coordinate-system.md、0010-document-model-and-commands.md、0011-shared-annotation-painter.md、0012-canvas-zoom-model.md、0013-text-input-strategy.md、0014-clipboard-formats-and-retry.md、0015-file-writing-and-background-encoding.md 與 docs/ScreenshotApp.Spec.md。保留現行 code-behind，不引入 MVVM/DI 框架。先讀 docs/goals/g4/PROGRESS.md 續跑，每輪更新，疑義寫 BLOCKED.md。自動化重構驗收與真實 UI 驗收分開。

2. 界限
只允許修改 ScreenshotApp/Editor/EditorWindow.xaml.cs、新增 Editor/Presentation/ 下的決策元件/薄轉接器，新增 ScreenshotApp.Tests/EditorPresentation*Tests.cs、EditorExportFlow*Tests.cs、TestSupport/Presentation*，以及 EditorWindowTests.cs 必要的機械接線修改與新增斷言。文件限 docs/goals/g4/、docs/ARCHITECTURE.md、相關新 ADR、CHANGELOG.md。XAML、AppController、EditorSession、Model、Commands、CoordinateMapper、AnnotationPainter、Export 服務與其既有測試均唯讀。

3. 任務 0
記錄 HEAD、工作區、受保護檔案雜湊與原測試清單，建立 feature/phase-08-editor-decisions 分支或確認續作。7729736 的 EditorWindow.xaml.cs 為 1276 行、行涵蓋 266/676、分支 90/402；Release 0 警告、933 測試通過。先執行 build/test，基準改變須記錄原因。以現有行為建立可重放的輸入/輸出/事件順序案例，再開始搬移，回執不超過 10 行。

4. 工作
分三輪，每輪先特徵測試、再抽出、再驗證正式視窗呼叫新元件：
A 檢視狀態：一般/裁切工具顯示範圍、進出裁切工具、套用/復原/重做/還原後的 fitMode、完整圖片符合視窗最多100%、裁切最多400%、尺寸未就緒、DPI與縮放錨點。視窗傳入尺寸/原點，換算仍經 CoordinateMapper；不要在新類別再複製公式。
B 右鍵選單：裁切/畫布/標註三種選單、文字/編號/箭頭項目顯示、Undo/Redo/匯出/還原/清空可用狀態；同步更新時點與選取副作用不變。XAML資源查找與事件接線留在視窗。
C 匯出：提交文字→記錄文件版本→UI合成並Freeze→背景編碼/寫檔→UI剪貼簿/成功標記/提示。取消存檔、剪貼簿拒絕、寫檔失敗、設定目錄記憶失敗、重入與舊版本匯出後的新修改，均有測試。僅替換對話框/外部寫入等副作用，使用真實 ImageDocument、ImageRenderer 與編碼器；不得真的寫剪貼簿。
文字焦點、IME、鍵盤路由、滑鼠捕捉、Dispatcher優先權與版面取值仍在原視窗，不搬入新決策類別。測試可建立不顯示的視窗，必須沿用 StaRunner；非同步測試要能處理 Dispatcher，不以 Sleep 猜測。
新測試加 Trait("Goal","G4")；保留原 EditorWindowTests 的資源名稱、選單、快捷鍵斷言。加接線測試確認視窗使用新決策、Load 換文件後解除舊訂閱、不重複執行。分別暫時破壞裁切fit重設與匯出成功標記，相關測試須失敗，恢復後通過。

5. 規則
不以分割partial檔案或縮短行數充當完成；每個新元件都要有獨立決策與正式呼叫端。不改裁切外命中、不調整快顯內容/快捷鍵/字串，不用 ConfigureAwait(false) 改掉UI接續，不變更輸出合成策略。不執行桌面腳本或發佈，不改人工MT狀態。不 commit、合併、推送、PR或打標籤。保留其他變更；連敗三次重新診斷，不能放寬斷言。

6. 完成條件
A/B/C 的特徵輸出與事件順序在重構前後一致；新決策元件不讀真實 Window/控制項狀態，只接收必要資料與副作用邊界；正式視窗接線測試通過。XAML與受保護核心檔雜湊不變；兩項負向注入皆有紅到綠證據；Release 0 警告、原測試全集保留、0 failed/0 skipped，Goal=G4 total>0。VALIDATION.md 列出原始命令與所有情境，必要項無阻礙才完成。報告限本機重構驗收，焦點/IME/實際版面仍不宣稱已重新實機通過。
~~~

### Acceptance Criteria

| 區域 | 必須驗證的結果 |
|---|---|
| 檢視狀態 | 各裁切／工具／Undo／Redo／fit 組合輸出不變；包含尚未版面配置與不同 DPI 的輸入資料 |
| 選單 | 三種選單、各標註種類、可用與隱藏狀態、選取副作用一致；保留資源名稱測試 |
| 匯出 | 取消／失敗不標記成功；成功標記合成時的版本；正在匯出時拒絕重入；記住目錄失敗不否定已成功存檔 |
| 執行緒 | 測試記錄合成／剪貼簿回呼在 STA／UI 接續，背景只收到凍結圖片；不能只驗證函式回傳值 |
| 正式接線 | 未顯示視窗仍能呼叫新元件；換文件不重複訂閱；不是測了一份正式程式未使用的複本 |
| 範圍保護 | XAML 與列為唯讀的核心、繪製、輸出服務不變；既有測試與兩項受控破壞驗證通過 |

不設定「視窗剩多少行」作為完成條件；把邏輯搬家而沒有更好的可測試邊界不算完成。

### Validation Commands

`Goal=G4` 在本 Goal 新增；其他測試名稱已存在。受保護檔案以任務 0 雜湊比對，以下 diff 命令適用於沒有原有差異的基準：

~~~powershell
dotnet build ScreenshotApp.sln -c Release
dotnet test ScreenshotApp.sln -c Release --no-build --filter "Goal=G4|FullyQualifiedName~EditorWindowTests|FullyQualifiedName~EditorZoomTests|FullyQualifiedName~ImageRendererTests|FullyQualifiedName~DirtyTrackingTests"
dotnet test ScreenshotApp.sln -c Release --no-build
git diff --exit-code -- ScreenshotApp/Editor/EditorWindow.xaml ScreenshotApp/AppController.cs ScreenshotApp/Editor/EditorSession.cs ScreenshotApp/Editor/Model ScreenshotApp/Editor/Commands ScreenshotApp/Editor/Rendering ScreenshotApp/Capture/CoordinateMapper.cs ScreenshotApp/Export
git diff --check
~~~

新測試須另以 `--filter "Goal=G4"` 輸出 TRX 並確認 total > 0，避免其他既有測試通過卻掩蓋新分類空跑。

### 限制條件／Anti-goals

不改 WPF 的焦點與鍵盤事件策略、不宣告混合 DPI／IME／實際版面已驗證、不把視窗拆成數個巨大服務、不改內部類別為公共 API。此 Goal 的完成是可驗證的內部重構，並不代替後續版本的實機或發佈驗收。

### 預期 Failure Modes

| 失敗模式 | 處理與驗證 |
|---|---|
| 未顯示視窗尺寸為 0，測試只走 early return | 決策測試顯式傳入尺寸；另測未就緒狀態，不用它代表正常版面 |
| 抽出方法改變提交文字或標記匯出的時機 | 使用事件序列與不同版本的文件驗證，不僅比最終 bool |
| await 後跑到錯誤執行緒 | 驗證執行緒與 SynchronizationContext；不改既定 UI 接續 |
| 選單剛開啟時可用狀態落後 | 保留同步更新與具名資源查找，不改成背景命令重新查詢 |
| Load 後舊 Session 仍觸發更新 | 以兩份文件輪替，驗證舊來源不再影響新視窗狀態 |
| 為了讓重構漂亮而改到 G1 邊界 | 保留既有 AppController 介面；若 G1 已完成，沿其現存邊界接線，不再設計第二套 |

### 工作範圍與複雜度

**高複雜度，估計 5–7 輪**：特徵資料 → 檢視狀態 → 選單 → 匯出 → 接線／訂閱 → 執行緒與負向驗證。風險主要在 WPF 時序，因此可驗證性給 4 分。與 G1 都可能碰到 `EditorWindow.xaml.cs`，應依序執行；G1 不是必需前置條件。

## 8. G5：煙霧判定器的離線回放與負向測試

### 完整 /goal 指令

~~~text
/goal 為 ScreenshotApp 的實機煙霧腳本建立共用且可離線驗證的測量與安全判定，讓圖樣誤判、資源趨勢誤判、剪貼簿還原決策與結果假成功可在不操作桌面的情況下被抓到；不全面合併四支腳本，也不執行實機煙霧驗證。

1. 執行預設與目的
先讀 AGENTS.md 的煙霧規則、docs/ACCEPTANCE.md、.planning/2026-10-18-screenshotapp-v1/findings.md，特別是 P5-11 圖樣560像素誤判、剪貼簿序號與GDI尖峰紀錄。測試判定器本身，而不是用替身宣布產品實機通過。先讀 docs/goals/g5/PROGRESS.md 續作，每輪更新；疑義寫 BLOCKED.md。

2. 界限
只允許 tools/smoke/ 下新增 SmokeMeasurements.psm1、必要的純計算 C#、Test-SmokeMeasurements.ps1 與 fixtures/；四支 Invoke-Phase*Smoke.ps1 只替換已驗證的純測量/安全決策呼叫。文件限 docs/goals/g5/、docs/ARCHITECTURE.md 的測試段與 CHANGELOG.md。產品、產品測試、Show-TestPattern.ps1、歷史 evidence、ACCEPTANCE.md 的舊結果、task_plan.md 均唯讀。保留每支腳本的參數、檢查名稱、門檻、順序、原生API與輸入/清理動作。

3. 任務 0
固定 HEAD、工作區、四支腳本與原始證據雜湊，建立 feature/phase-08-smoke-oracles 分支或確認續作。基準7729736四支腳本4135行；PowerShell AST有155個函式定義、37個重複名稱、27組本文不同；7支工具腳本語法無錯。這些是背景，不是必須減少的績效指標。先剖析全部腳本，列出各Phase純判定的輸入、輸出與差異；不要dot-source會執行桌面動作的整支腳本。回執不超過10行。

4. 工作
先以固定案例鎖定原判定，再只抽出有相同契約的純計算/決策到模組；有不同語意保留明確差異，不選一份覆蓋全部。
離線案例必須包含：已知圖樣與自身比較差異0；相同偏紅底圖不能判成存在標註；人工改動N個像素回傳精確N與首個位置；非零圖樣原點/裁切偏移/stride填補；圖樣原點搜尋；GDI單次尖峰與持續成長；新離線報告在不足樣本時不得聲稱已證明穩定，既有實機門檻不變；有失敗或未完成檢查不能回報全通過；timeout資料不能算成功。
安全判定用傳入的快照測試：使用者有新輸入、前景或滑鼠目標不在允許清單時不授權送出操作；腳本自己寫入剪貼簿後更新已知序號、外部後寫時不還原、沒有外部改動才可還原。原生查詢/SendInput/實際Clipboard/程序終止仍留在原位置，不改順序或新增還原權限。
fixtures只含合成像素/假視窗識別/時間/序號與既有公開於本倉庫的資源CSV，禁止真實桌面截圖或使用者剪貼簿。用原始資料與手算結果建立oracle，不能直接保存新實作輸出作為期望。
Test-SmokeMeasurements.ps1使用現有pwsh與.NET，不安裝Pester或其他模組；以全新程序測匯入/重複匯入，沒有視窗、輸入、系統設定或網路副作用。原腳本必須呼叫被測的共享判定，不能留未使用的測試複本。新增靜態接線檢查保護每次輸入前的使用者/前景/目標檢查及清理順序。
提供 -VerifyFailureDetection：在隔離副本暫時破壞像素判定、結果彙總及剪貼簿還原判定，各自執行同一測試入口必須非0；恢復後0。保存原始輸出及套用到每個Phase的差異說明。

5. 規則
不改原有通過門檻或圖樣、不刪檢查、不以行數或零重複名稱為目標、不共用原生API型別與桌面動作、不存整個桌面。不得啟動ScreenshotApp、Show-TestPattern或四支實機腳本；不讀寫使用者剪貼簿/設定/登錄，不終止使用者程序。不commit、合併、推送、PR、發佈或打標籤。連敗三次重新診斷，不降低oracle要求。

6. 完成條件
所有上述正向/負向固定案例、原始CSV回放與每個Phase的靜態接線檢查通過；三種受控破壞均能被正式自我測試抓到。所有ps1/psm1語法無錯，純C#可實際載入，匯入沒有外部副作用。產品與受保護檔雜湊不變；VALIDATION.md有命令、案例數與紅到綠證據，必要項無阻礙才完成。成果只表示判定器已離線驗證，四支實機腳本與產品操作沒有因此取得新的PASS。
~~~

### Acceptance Criteria

1. **像素判定有獨立正解**：圖樣自身、已知 N 個變動、偏紅底圖、裁切偏移與 stride 等案例均可精確比對；P5-11 的錯誤判定能被負向案例抓到。
2. **統計與結果誠實**：使用既有資源 CSV 回放加上合成趨勢；不把單次尖峰視為持續洩漏；新的離線報告不能將不足樣本說成穩定，timeout 或未完成檢查不能算通過。既有有效資料的門檻與結果保持一致，不藉離線測試修改實機門檻。
3. **安全決策可測**：前景／目標／使用者輸入與剪貼簿序號分支各有正反例；測試完全不呼叫真實輸入、剪貼簿或程序終止。
4. **正式腳本確實使用**：只替換純判定呼叫；原生查詢、輸入、備份／還原動作仍按原順序；差異檢查保護四支腳本，而非只測共用模組。
5. **三種負向注入有效**：錯誤像素結果、把 FAIL 算成功、錯誤還原外部剪貼簿，皆使同一自我測試入口非 0。
6. **不挪用驗收**：不更新舊 evidence、不把離線案例數混成 Phase 1–4 的實機 PASS 數；未執行實機驗證的事實明確保留。

### Validation Commands

以下模組與自我測試是**待建立產物**，本次沒有執行。兩個入口都必須在不操作桌面的新程序完成：

~~~powershell
pwsh -NoProfile -File tools/smoke/Test-SmokeMeasurements.ps1
pwsh -NoProfile -File tools/smoke/Test-SmokeMeasurements.ps1 -VerifyFailureDetection
git diff --exit-code -- ScreenshotApp ScreenshotApp.Tests tools/smoke/Show-TestPattern.ps1 docs/evidence docs/ACCEPTANCE.md
git diff --check
~~~

自我測試須包含全部 `.ps1／.psm1` 語法剖析、純 C# 載入、每個 Phase 接線與非零案例檢查；`-VerifyFailureDetection` 只有在三種受控破壞都被抓到、且正常版本恢復成功時才能回傳 0。不需要重新跑完整 dotnet 測試，因為產品與其測試必須保持原樣。

### 限制條件／Anti-goals

不全面共用 `Start-App`、`Stop-AppViaTrayMenu`、`SendInput` 或所有內嵌 C#；重複名稱本身不是要消滅的缺陷。不補跑需要 30–40 分鐘桌面閒置的完整套件、不調整 Windows 全域設定、不保存使用者螢幕或剪貼簿資料。

### 預期 Failure Modes

| 失敗模式 | 處理與驗證 |
|---|---|
| 不同 Phase 的同名函式實際不同契約 | 先比輸入／輸出／副作用；只抽共同部分，不以名字相同直接合併 |
| 模組載入執行了桌面動作 | 模組只定義純計算；全新程序測載入，禁止 dot-source 完整實機腳本 |
| PowerShell scope 讓剪貼簿序號失同步 | 明確傳入／回傳判定資料；測試自己寫入與外部寫入的時序 |
| Add-Type 重複型別或參考錯誤 | 純 C# 載入與重複匯入須實測，不能只通過 PowerShell Parser |
| 測試回放重複舊的誤判公式 | 像素預期由手工變動數決定，統計資料同時含單次尖峰與持續上升 |
| 靜態接線通過被誤說成桌面安全保證 | 報告限定為離線判定與接線檢查，真正 GUI 互動保留尚未重驗 |

### 工作範圍與複雜度

**中高複雜度，估計 4–6 輪**：盤點差異 → 像素 oracle → 統計回放 → 安全決策 → 接線 → 負向驗證。產品程式碼不變，風險集中於腳本作用域與判定差異。範圍刻意不包含桌面動作重構，使完成不依賴使用者閒置或正式環境權限。

## 9. 推薦與執行順序

**目前最值得執行的是 G1（125 分）**。原因是未匯出變更、取消與重入已有完整決策依據，但實際協調檔案的分支涵蓋率只有 1.8%。補強這一層能讓後續錯誤恢復與重構不再主要依賴實機操作，成果可由情境測試、呼叫順序與受控破壞證明。

建議依序 **G1 → G2 → G3 → G4 → G5**；這是優先順序，不是必要依賴。若當前更重視每次變更都能自動把關，可先選 G2。每項都能從目前儲存庫單獨開始；不需要先執行另外四項。

本文件不授權多代理或平行修改。G1 與 G4 共享編輯視窗接線；G2 可能替測試加分類；所有 Goal 都會更新 CHANGELOG／架構說明，因此即使日後另行允許並行，仍須指定共享檔案的單一負責者並重新整合驗證。

**這一輪到文件交付為止。五個 Goal 都尚未開始實作。**

## 附錄：驗證證據與命令使用規則

### 本次實際取得的證據

- `dotnet build`：Release 0 Warning(s)、0 Error(s)。
- `dotnet test` 加內建 Code Coverage：933 passed、0 failed、0 skipped，已產生 TRX 與 Cobertura。
- `git ls-files`／`git log`／`git status`：198 個已追蹤檔案、17 筆歷史、起始無已追蹤檔案差異；`.codegraph/` 與本文件原先即未追蹤。
- 7 支 PowerShell 腳本 AST：0 語法錯誤；4 支實機腳本函式統計為 155／37／27。
- GitHub API：公開儲存庫 Issue 空陣列、workflow／run 數 0、main SHA 與本機一致；upstream API 404。
- 本次原始 TRX、Cobertura、舊稿備份及原有追蹤檔案雜湊，保存在儲存庫外的獨立暫存目錄：`C:\Users\wangh\AppData\Local\Temp\screenshotapp-goal-audit-20261029-30bb9c972114481d9daa35d8f5441e67`。這是可清理的本機證據，不是可攜交付或永久驗收紀錄；本文件已保留主要數據與重跑方式。

### 執行任一 Goal 時的命令要求

命令一律由儲存庫根目錄執行，使用 Windows 的 .NET 10 SDK 與 PowerShell 7；產品需要 WPF／Win32，不能用 Linux 建置成功代替 Windows 驗證。

驗證腳本需檢查 native 命令結束代碼；篩選測試還需檢查 TRX，至少：

~~~powershell
# $trxPath 指向該次執行剛產生的 TRX；不要重用舊檔。
[xml]$result = Get-Content -LiteralPath $trxPath -Raw
$counters = $result.TestRun.ResultSummary.Counters
if (-not $counters -or [int]$counters.total -eq 0 -or
    [int]$counters.failed -ne 0 -or [int]$counters.notExecuted -ne 0 -or
    [int]$counters.passed -ne [int]$counters.total) {
    throw "測試缺少結果、有失敗或有未執行案例"
}
~~~

這段是驗收規則示意，`$trxPath` 必須由對應命令的輸出路徑提供；五份 `/goal` 都要求執行者保存與檢查實際結果，不能只貼命令或宣稱完成。

Release 執行檔若被鎖住，不終止使用者的程式；先以 Debug 處理獨立工作，記錄 Release 驗收尚未完成。所有暫時的錯誤注入只在自有隔離副本進行，復原自己的改動，不使用會清除他人工作的 reset／clean。新增的進度文件與測試資料只能寫入該 Goal 的允許範圍及隔離暫存／忽略產物目錄。
