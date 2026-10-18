# Handoff — Phase 3：圖片編輯核心

**日期**：2026-10-18
**狀態**：complete（實機煙霧驗證部分完成，見第 6 節）
**分支**：`feature/phase-03-editor-core` → fast-forward 合併回 `main`

## 1. 完成內容

| Item | 產出 |
|------|------|
| P3-00 | `docs/adr/0010`（文件模型與命令）、`0011`（共用 AnnotationPainter）、`0012`（畫布縮放模型）、`0013`（文字輸入策略），皆 Accepted |
| P3-01 | `Editor/Model/Annotation.cs`（不可變 record：`BoxAnnotation` → 矩形／橢圓、`SegmentAnnotation` → 直線／箭頭、文字、步驟編號；幾何、命中、控制點）、`BoxHandles`、`Geometry2D`、`TextLayout`、`ImageDocument` |
| P3-02 | `Editor/Commands/UndoRedoStack.cs`（上限 100）、`EditCommands.cs`（新增、刪除、替換、裁切、層序五個命令，以 Id 找標註、設值語意） |
| P3-03 | `Editor/Rendering/AnnotationPainter.cs`（畫布與輸出共用；矩形筆劃在範圍內側、箭頭頭部、步驟編號以文字外框置中、亮色底配黑字） |
| P3-04 | `Editor/EditorWindow.xaml(.cs)`（工具列、屬性列、狀態列、畫布）、`AnnotationLayer`（縮放容器內）、`HandleLayer`（縮放容器外、固定螢幕尺寸）、`Settings/StyleChoices`（設定視窗改用共用選項） |
| P3-05 | `CoordinateMapper` 加入 `ImageViewTransform`、`ImageToViewScale`、Image ↔ ViewDip、`ViewLengthToImage`、`ImageToPixel`、`ScrollOffsetToKeep`；`Editor/EditorZoom.cs` |
| P3-06～P3-11 | `Editor/EditorSession.cs`：八種工具、拖曳預覽、裁切、文字提交規則、樣式套用與預設值、層序、復原重做、Esc 取消順序；編輯器快捷鍵與右鍵選單 |
| P3-12 | `Export/ImageRenderer.cs`、`Export/PngFileService.cs`（暫存檔後取代目標）；複製與另存為真實功能 |
| P3-13 | `AppStateMachine` 加入 Editing；`AppController` 開啟／重用唯一編輯視窗、預設樣式寫回設定、第二個執行個體在編輯中帶出編輯視窗；移除 Phase 2 暫用的剪貼簿通知 |
| P3-14 | 9 個新測試類別與 2 個既有類別的新增案例，共 460 個；全專案 774 個 |
| P3-15 | `tools/smoke/Invoke-Phase3Smoke.ps1`；證據 `docs/evidence/phase-03/`（部分完成） |
| P3-16 | `docs/ACCEPTANCE.md` 加入 MT-03（編輯器部分）、MT-06、MT-07 的步驟 |
| 文件 | `README.md`（編輯器操作與快捷鍵、專案結構）、`docs/KNOWN_LIMITATIONS.md`（暫無未匯出變更提醒、單選、空心圖形只命中外框、不自動換行、字型選項）、`CHANGELOG.md` |

## 2. 設計原因

- **不可變標註 + 五個設值命令**：移動、調整、樣式、文字、編號都是「把同一個標註換成新值」；Undo 只保存前後兩個 record，不保存 Bitmap；以 Id 找標註，層序變動不影響復原。
- **拖曳中只有預覽**：文件只經由命令修改，取消拖曳只丟棄預覽，一次拖曳只形成一筆。
- **整數圖片像素**：輸出與測試可精確比對；矩形範圍與區域選取一樣包含起點與終點像素；線段通過像素中心。
- **縮放容器 `LayoutTransform` 比例 = zoom ÷ dpiScale，換算全在 `CoordinateMapper`**：100% 為一個圖片像素對應一個實體像素（實機已逐像素確認）；控制點層在縮放容器外，容差以螢幕 DIP 定義。
- **一般狀態只顯示裁切範圍、裁切工具顯示整張原圖**：所見即所得，又能重新放大裁切範圍。
- **裁切不提供平移**：進入裁切工具時範圍是整張圖片，若範圍內拖曳是平移，就無法直接框選；改為控制點以外一律框選新範圍。
- **控制點位移以指標移動量四捨五入**：控制點畫在像素格線上；以「所在像素」相減會多算一個像素，且指標像素被限制在圖片內，右、下邊到不了圖片邊緣（由自動化測試發現）。平移仍用限制在圖片內的像素，抓住的位置不會離開圖片。
- **文字以縮放容器內的 TextBox 原地輸入**：輸入法由 WPF 處理；以第一個字元位置量測輸入框的內部偏移，讓完成後由繪製碼畫出的文字不位移；快捷鍵用 `InputBindings`，由輸入框路由來的一律不執行編輯器動作。
- **`ExportedVersion`／`IsDirty`、「開啟」按鈕延到 Phase 4**：有使用者時才加入、不放占位按鈕。

## 3. 驗證結果與證據

| 驗證 | 結果 |
|------|------|
| `dotnet build ScreenshotApp.sln -c Release` | 0 error、0 warning |
| `dotnet test ScreenshotApp.sln -c Release` | 774 passed、0 failed（連續 5 次） |
| 煙霧驗證（Release exe，Windows 11 Pro 10.0.26200，單一顯示器 3456×2160 @175%） | 前 12 項通過：編輯器開啟、100% 畫布與已知圖樣逐像素一致、未加標註的複製結果與截圖一致、3px 矩形筆劃 1764/1764 純紅且在範圍內側、箭頭與編號、`Ctrl+Z`／`Ctrl+Y`；其餘未完成（見第 6 節） |
| 程式碼註解掃描（不得引用編號或文件路徑） | 無違規 |

自動化測試由兩個 subagent 各自在獨立 worktree 撰寫（避免單一專案並行建置衝突），主 agent 合併並修正它們發現的問題：

1. 裁切右、下控制點拖到圖片邊緣差一個像素 → 控制點位移改以指標移動量四捨五入。
2. `MoveDrag`／`ResizeDrag` 的 CS9124 警告。
3. 橙色步驟編號配白字對比偏低 → 亮色門檻 160 → 150。
4. 完整執行時 `SafeHandleTests` 固定失敗：WPF 繪製資源在終結器後才由繪製執行緒刪除，干擾以整個處理程序計數的 GDI 量測 → 量測輔助改為等連續兩次讀數相同。

## 4. 風險

- **暫時沒有未匯出變更保護**：新截圖直接取代目前文件、關閉編輯器或結束程式直接捨棄標註，已寫入已知限制；Phase 4 的 P4-05 加入。
- **文字輸入框與繪製結果的對齊只做了設計，沒有實機確認**：以 `GetRectFromCharacterIndex(0)` 量測偏移；若有位移需調整量測方式（MT-06 與煙霧腳本的對應檢查會發現）。
- **煙霧腳本會操作真實桌面**：已加入多重保護（偵測使用者輸入即停止、目標視窗檢查、PrintWindow、剪貼簿序號檢查）。第一次試跑時保護不足，滑鼠點擊、拖曳與 Ctrl+滾輪可能落在使用者的 QuiteRSS 與瀏覽器視窗（已告知使用者），螢幕擷取的證據截到其他程式（已刪除）。另發現：煙霧驗證進行中執行其他指令，會讓終端機視窗來到前景；腳本已加入以點擊編輯視窗標題列或狀態列恢復前景，但最好在驗證期間不執行其他指令。
- **編輯器開啟位置**：WPF `CenterScreen` 置中於游標所在的顯示器；多螢幕與混合 DPI 下的視窗大小與位置需 MT-05 一併確認。

## 5. 後續工作（Phase 4）

1. 從 `main` 建立 `feature/phase-04-image-io`，Phase 4 → `in_progress`，先寫 ADR-0014～0016。
2. `ImageRenderer` 已是唯一合成器（P4-01 完整化：AT-07 完整測試、執行緒親和）；`ClipboardService` 加入 PNG 格式與重試策略（注意 .NET 10 剪貼簿內建的重試）；`PngFileService` 改背景編碼、初始資料夾為上次成功位置並更新 `LastSaveDirectory`（目前已是暫存檔後取代）。
3. `ImageDocument` 加入版本追蹤（`ExportedVersion`／`IsDirty`）與 `UnsavedChangesDialog`，套用於新截圖、開啟圖片、關閉編輯器與系統匣結束。
4. 編輯器「開啟」按鈕與系統匣「開啟圖片」（`ImageFileLoader`）。
5. 補完 Phase 3 煙霧驗證的後半段（電腦閒置時執行 `Invoke-Phase3Smoke.ps1`）。
6. 更新 `tools/smoke/Invoke-Phase2Smoke.ps1`：它依 Phase 2 行為（選取後直接複製到剪貼簿）比對結果，Phase 3 之後選取完成會開啟編輯器，剪貼簿比對全部會失敗，編輯視窗也會蓋住置頂圖樣。改為由編輯器 `Ctrl+C` 取得結果並在比對後關閉編輯器（或併入 Phase 3 腳本）；P5-07 以發佈版重跑各階段腳本前必須完成。

## 6. 未驗證項目

- **Phase 3 煙霧驗證後半段**：文字輸入與完成後位置一致、選取刪除、拖曳移動一筆復原、縮放 400%／25% 後複製結果相同、`Ctrl+0`、以介面裁切與復原重做、預設樣式寫回設定、編輯中取消新截圖恢復原文件、新截圖取代文件、第二個執行個體帶出編輯視窗、關閉編輯器回到常駐、10 次編輯循環的資源量測。腳本已就緒，多次執行都因使用者正在使用電腦（或其他視窗來到前景）而依保護規則停止。邏輯由自動化測試涵蓋。
- **MT-03（編輯器部分）、MT-06（微軟注音）、MT-07（多縮放一致性）**：需使用者實機驗證（步驟見 `docs/ACCEPTANCE.md`）。
- **Phase 1、2 的 MT（MT-01、MT-02、MT-03 取消部分、MT-04、MT-05）**：使用者尚未回報。
