# Handoff — Phase 7：右鍵選單與裁切後填滿畫布

**日期**：2026-10-19
**狀態**：完成（2026-10-19）；v1.1.0。發佈版實機煙霧驗證全數通過（資料夾版 Phase 3、單檔版 Phase 1、2，見第 3 節）
**分支**：`feature/phase-07-context-menu-crop-fill`（自 `main` 51cd11c 建立）→ 使用者要求提交後 fast-forward 合併回 `main`，加上本機標籤 `v1.1.0`（不推送）

## 1. 完成內容

| Item | 產出 |
|------|------|
| P7-01 | 規格：修訂紀錄；FR-14（右鍵選單）、FR-16（再製、反轉箭頭方向、清除所有標註）、FR-18（重新編號所有步驟）、FR-20（還原完整圖片）、FR-21（裁切後填滿畫布）、FR-22（多個標註的操作只算一筆）、FR-25（`Ctrl+D`）；新增 FR-36（三種右鍵選單）；AT-03／04／05 |
| P7-02 | `CompositeCommand`（依序執行、以相反順序復原）；`EditorSession`：`ResetCrop`、`ClearAnnotations`、`EditSelected`、`Duplicate`、`ReverseArrow`、`RenumberSteps`，以及 `CanResetCrop`、`CanClearAnnotations`、`CanRenumberSteps` |
| P7-03 | `EditorZoom.Fit(..., enlarge)`：只顯示裁切範圍時上限 400%；`EditorWindow._shownCrop`：顯示的裁切範圍改變時回到符合視窗 |
| P7-04 | `EditorWindow.xaml` 三份選單（標註、畫布、裁切）、`PrepareContextMenu`、16 個 Click 處理、`Ctrl+D`；`Strings.resx` 新增 24 個鍵 |
| P7-05 | ADR-0010、ADR-0012 修訂；ARCHITECTURE、USER_GUIDE、KNOWN_LIMITATIONS、ACCEPTANCE、CHANGELOG `[Unreleased]` |
| P7-06 | Release 建置 0 警告；933 個測試通過；Phase 3 煙霧腳本第 9 步加入五項檢查 |
| P7-08 | 版本 1.1.0（專案檔 `Version`、`app.manifest`）；CHANGELOG `[1.1.0] - 2026-10-19`；README、USER_GUIDE（含更新方式）、KNOWN_LIMITATIONS 的版本與發佈檔名；ACCEPTANCE（v1.1.0 發佈版、使用者驗證步驟改用 1.1.0 的 ZIP）；AGENTS.md（版本範例、發佈新版本的步驟、資源中具名元素與未顯示視窗的陷阱）；ADR-0017 第 4 點修訂；規格修訂紀錄標示 v1.1.0 |
| P7-09 | commit 前試發佈：資料夾版 275 個檔案、155.9 MB（ZIP 67.3 MB），單檔版 8 個檔案、149.4 MB（ZIP 63.5 MB）；檔案版本 1.1.0.0 |
| P7-10 | `phase-07` commit、本機標籤 `v1.1.0`、以該 commit 重新發佈（產品版本內嵌 commit）、fast-forward 合併回 `main`；不推送 `upstream` |

## 2. 設計原因

- **裁切維持非破壞性**：「填滿」是畫布的符合視窗規則，不是把文件換成裁切後的圖片。規格要求非破壞性 `CropRect` 且裁切不改寫標註座標；復原裁切因此只是一筆 `SetCropCommand` 的復原。
- **400% 上限只在顯示裁切範圍時**：由「顯示範圍是否為整張圖片」推得，不另加旗標；新截圖、開啟圖片與裁切工具中仍不超過 100%，第一次開啟的小圖維持原尺寸（使用者選擇）。
- **觸發條件**：記住上次在裁切工具以外顯示的裁切範圍，不同就回到符合視窗。一個條件涵蓋套用、復原、重做與還原完整圖片；只進出裁切工具時保留使用者的縮放。檢查放在提早返回之前，涵蓋「裁切工具中復原再離開」顯示範圍不變的情況。
- **復原不還原檢視**：縮放與捲動屬於畫面，不屬於文件；放進命令會讓文件與畫面耦合。
- **複合命令**：清除所有標註與重新編號所有步驟各只算一筆復原。以相反順序復原時，每個子命令看到的文件與它執行完當下相同，刪除記住的索引仍然有效。
- **選單用 Click、開啟前同步設定狀態**：與既有標註選單一致；`RoutedCommand` 的可用狀態以背景優先權重新查詢，剛開啟時可能顯示上一刻的狀態。資源中的 `x:Name` 不產生欄位，以 `FindLogicalNode` 依名稱查找。
- **右鍵空白處取消選取**：比照左鍵，避免畫布選單開啟時仍顯示選取外框。
- **再製的偏移以裁切範圍判斷**：複本要在畫面上看得到。

## 3. 驗證結果與證據

| 驗證 | 結果 | 證據 |
|------|------|------|
| `dotnet build ScreenshotApp.sln -c Release` | 0 警告、0 錯誤 | 本機輸出 |
| `dotnet test ScreenshotApp.sln -c Release --no-build` | 933 passed／0 failed（Phase 5 為 894，新增 39） | `docs/ACCEPTANCE.md` 第 1 節 |
| 新測試先紅後綠 | 實作前建置失敗（CS0246／CS1061），實作後通過 | `progress.md` Phase 7 |
| Phase 3 煙霧腳本 | 語法剖析與內嵌 C# 編譯通過；以 v1.1.0 資料夾版實機執行 43／43 通過，第 9 步的五項新檢查：套用裁切後 400%、Ctrl+Z 後 100%、Ctrl+Y 後 400%、右鍵畫布選單「還原完整圖片」後輸出為整張圖片且 100%、Ctrl+Z 恢復裁切 | `docs/ACCEPTANCE.md` 第 2 節 Phase 7；`docs/evidence/phase-07/release-phase3-*` |
| v1.1.0 發佈（`tools/Publish-Release.ps1`） | commit `6c7c103` 之後以該 commit 發佈，產品版本 `1.1.0+6c7c103…`；資料夾版 275 檔、155.9 MB（ZIP 67.3 MB），單檔版 8 檔、149.4 MB（ZIP 63.5 MB） | `docs/ACCEPTANCE.md` 第 2 節「v1.1.0 發佈版」；`publish/`（不納入版本控制） |
| 發佈版煙霧驗證（單檔版） | Phase 1 31／31、Phase 2 38／38（30 次截圖循環 GDI 恆為 39）；依 ADR-0017 提供單檔版 | `docs/evidence/phase-07/singlefile-*` |

## 4. 風險與已知限制

- 放大比例通常不是整數，與既有的 150% 一樣以最近鄰取樣，部分像素寬一個螢幕像素；很小的裁切範圍在 400% 時仍不會填滿（已列入 KNOWN_LIMITATIONS）。
- 版面需要顯示視窗才會計算，填滿的縮放值沒有單元測試，靠煙霧腳本的狀態列縮放確認。
- 既有行為（未修改）：命中判定以整張原圖為範圍，裁切後在畫布邊緣的灰色區域可能選到看不見的標註，右鍵選單同樣會對它開啟標註選單。是否限制在裁切範圍內留待使用者決定。

## 5. 後續工作

- 使用者依 ACCEPTANCE 第 3 節「Phase 7 驗證步驟」確認標註選單、裁切選單、編輯文字與修改編號的焦點、`Ctrl+D`。
- 使用者決定命中判定是否限制在裁切範圍內（findings「既有行為」），以及是否推送到 `upstream`。

## 6. 未驗證項目

- 標註選單與裁切選單的實際操作、編輯文字與修改編號在選單關閉後取得焦點、實體鍵盤的 `Ctrl+D`：尚未驗證（選單選擇與狀態、項目與快捷鍵的接線由 `EditorWindowTests` 涵蓋）。
- v1.1.0 沒有重跑 Phase 4 煙霧腳本（剪貼簿格式、另存、開啟圖片、未匯出變更、資源與延遲量測）：Phase 7 沒有修改這些功能的程式碼，沿用 v1.0.0 發佈版的結果。
