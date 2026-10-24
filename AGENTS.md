# AGENTS.md

ScreenshotApp 是 Windows 11 常駐系統匣的截圖與圖片標註工具：.NET 10 WPF＋Win32，應用程式專案 `ScreenshotApp/`、xunit 測試專案 `ScreenshotApp.Tests/`。介面文字、文件與回覆一律使用台灣繁體中文。

## 依據

- 產品行為以規格 `docs/ScreenshotApp.Spec.md` 為準；需求變更先修改規格。
- 修改某個領域前，先讀 `docs/adr/` 中對應的 ADR；決策改變時新增 ADR，或在原 ADR 加上「修訂」段落。
- 模組關係、資料流、座標系統與執行緒模型：`docs/ARCHITECTURE.md`。
- 實測得到的 WPF／Win32 行為與踩過的陷阱：`.planning/2026-10-18-screenshotapp-v1/findings.md`。

## 建置、測試與發佈

```powershell
dotnet build ScreenshotApp.sln -c Release     # 0 警告
dotnet test ScreenshotApp.sln -c Release
pwsh -NoProfile -File tools/Publish-Release.ps1
```

- 發佈在 commit 之後執行：產品版本內嵌建置當時的 commit（`<版本>+<commit>`，版本取自專案檔的 `Version`）。輸出在 `publish/`，不納入版本控制。
- 新版本：專案檔的 `Version` 與 `app.manifest` 的 `assemblyIdentity` 版本一起更新（語意化版本），CHANGELOG 的 `[Unreleased]` 改為版本段落；commit 後加上本機標籤 `v<版本>`，再以該 commit 發佈。
- 使用者從 `bin\Release` 執行 ScreenshotApp 時執行檔被鎖住，Release 建置會失敗：請使用者從系統匣結束，期間可先以 Debug 組態建置與測試。
- 已有 ScreenshotApp 在執行時再啟動一個，既有的執行個體會在使用者桌面開啟設定視窗或編輯器。

## 程式碼慣例

- 介面文字只放在 `ScreenshotApp/Resources/Strings.resx`：程式碼用 `Strings.Get`／`Strings.Format`，XAML 用 `{res:Str Key}`。`StringsResourceTests` 檢查鍵都存在、沒有閒置的鍵、XAML 沒有寫死的文字。
- 註解用自己的話寫出設計意圖與限制；FR／AT／MT／ADR 編號與文件路徑只出現在 `docs/` 與測試名稱中。
- 座標：實體像素是唯一真值，座標空間之間的換算只經過 `CoordinateMapper`；畫布 100% 代表一個圖片像素對應一個實體像素。
- 文件模型：標註是不可變的 record，文件只經由 `IEditCommand` 進入 `UndoRedoStack` 修改；拖曳期間只更新預覽，放開時才產生一筆命令。
- 輸出：`AnnotationPainter` 是畫面與輸出共用的唯一繪製碼；`ImageRenderer` 先合成整張原圖再取出裁切範圍（平移到裁切原點再繪製，平滑像素會改變）。
- 執行緒：合成在 UI 執行緒；交給背景執行緒的點陣圖先 `Freeze()`；PNG 編碼與寫檔在背景執行緒。
- GDI 與 USER 物件以 SafeHandle 持有；剪貼簿以 DIB 與 PNG 位元組提供（`Clipboard.SetImage` 每次會在處理程序留下一個 GDI 物件）。
- 檔案與登錄錯誤顯示給使用者時，原因經 `ErrorText.Describe` 轉成繁體中文。
- XAML 資源（例如 `Window.Resources` 中的右鍵選單）裡的 `x:Name` 不會產生欄位；以 `LogicalTreeHelper.FindLogicalNode` 依名稱查找，並以測試確認每個名稱在執行期都找得到。
- 文字檔為 UTF-8、LF；以 Python 修改檔案時用位元組讀寫（Windows 的文字模式會寫成 CRLF）。

## 測試

- 修正錯誤時先寫能重現的測試，確認失敗後再修正。
- WPF 物件在 `StaRunner` 中建立；量測 GDI／USER 物件數的測試加入 `GdiObjectCountCollection`（不平行執行）。
- 編輯視窗可以在測試中以未啟動的 `AppController` 建立並載入文件（不顯示）；沒有顯示的視窗不會計算版面，符合視窗等依賴版面的行為以煙霧腳本驗證。
- 測試只使用 `TempDirectory`、測試專用的登錄子機碼與具名物件；使用者的剪貼簿、`%LocalAppData%\ScreenshotApp` 與 `HKCU\...\Run` 保持原樣。

## 實機煙霧驗證（`tools/smoke/`）

腳本會實際啟動程式，並以模擬的滑鼠鍵盤操作使用者的桌面。

- 只在使用者閒置時執行：先在背景偵測閒置（約 90 秒），一閒置就在前景依序跑完所有腳本，期間不執行其他指令（執行中的主控台會搶走前景）。使用者回來操作時 Phase 3、4 腳本會立即停止，之後再補跑。
- 以 `-ExePath` 指定要驗證的建置（例如 `publish/` 下的發佈版）。證據放在 `docs/evidence/phase-<NN>/`：結果文字檔、CSV，以及只含 ScreenshotApp 視窗的 PrintWindow 截圖。
- 撰寫或修改檢查時：
  - 判斷標註是否出現或被刪除，比較「與已知圖樣不同的像素」（`CountPatternMismatches`）；圖樣本身在部分區域就有符合顏色條件的像素。檢查失敗時先檢查量測方式。
  - 每次模擬輸入前確認使用者沒有操作、前景與目標視窗屬於 ScreenshotApp（比照 Phase 3、4 腳本）；覆蓋層出現後等它成為前景再操作。
  - 腳本自己寫入剪貼簿後立即更新記錄的剪貼簿序號，結束時才能正確還原使用者的剪貼簿。
- 結果記錄在 `docs/ACCEPTANCE.md`；無法實機確認的項目標示「尚未驗證」。

## Git

- 每項工作開 `feature/phase-<NN>-<slug>` 或 `fix/<slug>` 分支，完成後以 fast-forward 合併回 `main`，不建立 PR。
- commit 訊息為 `phase-<NN>: <摘要>`（修正既有功能時用該功能所屬的 Phase），內文條列變更與驗證結果。
- commit 前：Release 建置 0 警告、相關測試通過；在 `CHANGELOG.md` 的 `[Unreleased]` 記錄變更；行為限制改變時更新 `docs/KNOWN_LIMITATIONS.md`。
- 推送到 `upstream` 只在使用者明確要求時進行。
- `.planning/2026-10-18-screenshotapp-v1/task_plan.md` 受 attestation 保護，修改後執行 planning-with-files 的 `attest-plan`。
