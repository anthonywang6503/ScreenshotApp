# v1.2 計畫完成度核對（2026-11-02）

> 本報告保留完成度核對當時的狀態。後續使用者已要求封存計畫並合併回 main，授權與處理見[計畫封存紀錄](../task_plan.md)。

**結論：七項功能的產品接線、自動化測試與本機封裝均已交付；整份計畫尚未全部驗收。** P13-00 仍未完成，發佈版桌面煙霧與 MT-12～MT-15 也沒有通過證據。不能將 Phase 的 `complete` 或原生工具的 `ALL PHASES COMPLETE` 解讀為所有驗收已通過。

核對基準為 `a4f1bd1e5a1fcb1a1d541d5252ee4dfeeb7cc8e9`；本次紀錄分支為 `feature/phase-14-plan-verification`。依 planning-with-files resolver 選定[現行計畫](../task_plan.md)，以 CodeGraph 檢查產品接線，並核對本機原始 TRX、組件與封裝。機器可讀結果見 [completion-checks-2026-11-02.json](../completion-checks-2026-11-02.json)。

## 計畫標記與實際完成度

| 核對範圍 | 結果 | 判讀 |
|---|---|---|
| 原生計畫格式 | 通過，8 個 Phase | 0、8～14 的格式可追蹤 |
| 原生完成檢查 | `complete` 8／8 | 只計算 Phase 狀態，不檢查勾選項目或產品行為 |
| 勾選項目 | 已勾選 65／66，P13-00 未勾選 | 已勾選的煙霧項目允許以「尚未驗證」結案，不能計為實測通過 |
| Phase 0、8 | 規劃與需求文件已交付 | [規格追蹤矩陣](../../../../docs/ScreenshotApp.Spec.md)涵蓋 FR-37～FR-43；各實作階段的 ADR 均存在 |
| Phase 9～13 | 七項功能已有產品接線與自動化測試 | 詳見下一節；桌面操作驗收仍有缺口 |
| Phase 14 | 版本、兩種本機封裝與證據已交付 | 尚不能宣稱發佈版桌面驗收完成，單檔版仍為待驗證封裝 |

## 七項功能的實作證據

下表為目前原始碼中可追溯的產品接線；所有列出的測試類別均出現在本次核對的通過 TRX 中。

| 需求／Phase | 功能與產品接線 | 對應測試 |
|---|---|---|
| FR-37／9 | 放大鏡：[MagnifierInput](../../../../ScreenshotApp/Capture/MagnifierInput.cs)、[MagnifierGeometry](../../../../ScreenshotApp/Capture/MagnifierGeometry.cs)、[SelectionController](../../../../ScreenshotApp/Capture/SelectionController.cs)與[覆蓋層呈現](../../../../ScreenshotApp/Capture/SelectionOverlay.xaml.cs)已處理 Ctrl 啟動抑制、按下／放開、像素取樣及邊緣位置 | [MagnifierGeometryTests](../../../../ScreenshotApp.Tests/MagnifierGeometryTests.cs)、[SelectionOverlayTests](../../../../ScreenshotApp.Tests/SelectionOverlayTests.cs) |
| FR-38／9、11 | 取色器：[覆蓋層 C](../../../../ScreenshotApp/Capture/SelectionController.cs)回傳色碼，由 [AppController](../../../../ScreenshotApp/AppController.cs)複製與通知；[EditorSession.PickColor](../../../../ScreenshotApp/Editor/EditorSession.cs)讀原圖並返回先前工具 | [CaptureFlowTests](../../../../ScreenshotApp.Tests/CaptureFlowTests.cs)、[EyedropperTests](../../../../ScreenshotApp.Tests/EyedropperTests.cs) |
| FR-39／10 | 馬賽克：[效果模型](../../../../ScreenshotApp/Editor/Model/EffectAnnotation.cs)、[PixelEffects.Mosaic](../../../../ScreenshotApp/Editor/Rendering/PixelEffects.cs)、[共用繪製](../../../../ScreenshotApp/Editor/Rendering/AnnotationPainter.cs)與[編輯命令](../../../../ScreenshotApp/Editor/EditorSession.cs)已接線 | [PixelEffectsTests](../../../../ScreenshotApp.Tests/PixelEffectsTests.cs)、[EffectAnnotationTests](../../../../ScreenshotApp.Tests/EffectAnnotationTests.cs)、[EffectRenderingTests](../../../../ScreenshotApp.Tests/EffectRenderingTests.cs)、[EditorSessionTests](../../../../ScreenshotApp.Tests/EditorSessionTests.cs) |
| FR-40／10 | 模糊：[PixelEffects.Blur](../../../../ScreenshotApp/Editor/Rendering/PixelEffects.cs)採三次盒狀模糊；與馬賽克共用效果模型、強度修改及[ImageRenderer](../../../../ScreenshotApp/Export/ImageRenderer.cs)輸出路徑 | 同上；獨立參考像素與裁切／層序案例已有通過紀錄 |
| FR-41／11 | 尺規：[PixelMeasurement](../../../../ScreenshotApp/Editor/Model/PixelMeasurement.cs)、[RulerAnnotation](../../../../ScreenshotApp/Editor/Model/RulerAnnotation.cs)及[共用繪製](../../../../ScreenshotApp/Editor/Rendering/AnnotationPainter.cs)提供可輸出尺寸；[覆蓋層](../../../../ScreenshotApp/Capture/SelectionController.cs)的 R 模式拖曳後保留量測，不完成截圖 | [RulerTests](../../../../ScreenshotApp.Tests/RulerTests.cs)、[MeasurementOverlayTests](../../../../ScreenshotApp.Tests/MeasurementOverlayTests.cs) |
| FR-42／12 | 截圖歷史：[AppSettings](../../../../ScreenshotApp/Settings/AppSettings.cs)預設開啟、50 筆；[HistoryStore](../../../../ScreenshotApp/History/HistoryStore.cs)、[HistorySnapshot／JSON](../../../../ScreenshotApp/History/HistorySnapshot.cs)及[AppController 歷史整合](../../../../ScreenshotApp/AppController.History.cs)處理新增、更新、還原、關閉、淘汰與清除 | [HistoryTests](../../../../ScreenshotApp.Tests/HistoryTests.cs)、[HistoryFlowTests](../../../../ScreenshotApp.Tests/HistoryFlowTests.cs)、[SettingsStoreTests](../../../../ScreenshotApp.Tests/SettingsStoreTests.cs) |
| FR-43／13 | 長截圖：[入口](../../../../ScreenshotApp/AppController.cs)接至[工作面板與輪詢](../../../../ScreenshotApp/Capture/ScrollCaptureWindow.xaml.cs)、[穩定影格／自動停止](../../../../ScreenshotApp/Capture/ScrollCaptureSession.cs)、[拼接](../../../../ScreenshotApp/Capture/ScrollStitcher.cs)及[滾輪目標／輸入介入檢查](../../../../ScreenshotApp/Capture/ScrollInput.cs)，完成後加入編輯器與歷史 | [ScrollStitcherTests](../../../../ScreenshotApp.Tests/ScrollStitcherTests.cs)、[ScrollCaptureSessionTests](../../../../ScreenshotApp.Tests/ScrollCaptureSessionTests.cs)、[ScrollCaptureIntegrationTests](../../../../ScreenshotApp.Tests/ScrollCaptureIntegrationTests.cs)、[CaptureFlowTests](../../../../ScreenshotApp.Tests/CaptureFlowTests.cs) |

[ADR-0019](../../../../docs/adr/0019-overlay-magnifier-and-color-pick.md)、[0020](../../../../docs/adr/0020-effect-annotations.md)、[0021](../../../../docs/adr/0021-ruler-and-eyedropper.md)、[0022](../../../../docs/adr/0022-capture-history.md)、[0023](../../../../docs/adr/0023-scrolling-capture.md)均為 Accepted；0023 明確保留跨應用程式相容性尚未驗證的界線。

## 自動化與封裝證據

- [既有品質摘要](../../../../docs/evidence/phase-14/automated-validation.json)所指的[原始 TRX](../../../../artifacts/quality/e9d5a11736ab4f499239d5624ab9f7c3/tests.trx)確實存在：1176／1176 通過，0 失敗、0 未執行；與摘要計數一致。Release 0 警告／0 錯誤沿用該次建置紀錄。
- 應用程式及測試 DLL 的實際 SHA-256 均與品質摘要一致。HEAD 與 `v1.2.0` 之間的 `ScreenshotApp/`、`ScreenshotApp.Tests/`、`tools/` 沒有差異，因此本次核對沿用既有測試，未重新執行建置或測試套件。自動化組件建置於版本提交前，與發佈組件分開核對。
- `v1.2.0` 解參照後指向 `1e443dcfb596c8de0a29476951d9c2a0c2f76020`，與[封裝證據](../../../../docs/evidence/phase-14/release-validation.json)一致；專案版本為 `1.2.0`，manifest 為 `1.2.0.0`，CHANGELOG 有本版段落。
- 重新計算兩份 ZIP 及其 EXE 的 SHA-256，均與封裝證據一致；資料夾內容分別為 276 檔與 9 檔。本次沒有重建封裝、重跑逐檔 ZIP 比對或執行封裝內程式。

## 尚未完成的驗收

| 待辦 | 目前證據 | 完成條件 |
|---|---|---|
| P13-00 滾輪方式實測 | [12 格矩陣](../../../../docs/evidence/phase-14/wheel-probe-matrix.csv)全部 `NOT_RUN` | Edge、Chrome、Word、VS Code、檔案總管、已知圖樣，各比較 SendInput／PostMessage 並記錄穩定時間；依結果確認目前 PostMessage 方案 |
| 發佈版桌面煙霧 | [18 項總表](../../../../docs/evidence/phase-14/smoke-results.csv)全部 `NOT_RUN` | 兩種封裝的 Phase 1～4、9～13 按[交接步驟](phase-14-release-1-2.md)在合適的測試桌面執行，取得實際結果 |
| MT-12～MT-15 | [驗收紀錄](../../../../docs/ACCEPTANCE.md)均為「尚未驗證」 | 完成 DPI／多螢幕、長截圖應用程式相容性、PowerPoint 輸出、歷史重啟與長時間常駐驗收 |
| 單檔提供資格及其他環境驗證 | [發佈交接](phase-14-release-1-2.md)保留未驗證 | 單檔需通過該版系統匣、快捷鍵、截圖與複製；乾淨 Windows 11 無 .NET Runtime、長時間資源趨勢仍待實測 |

上次桌面測試未執行的原因是既有 ScreenshotApp 程序守衛；本次程序清單未列出 ScreenshotApp，不能將舊 PID 視為目前阻擋。本次僅確認完成度，沒有啟動受測程式或送出桌面輸入。實機補測的完整步驟已存在，不需要重做功能實作。

## 本次文件修正

- 在計畫前段補上「實作交付完成、整體驗收未完成」的明確結論，保留 P13-00 未勾選及各 Phase 原有的交付結案語意。
- 修正 progress 開頭仍寫 Phase 12 complete／Phase 13 pending 的過時摘要，並同步 findings 與本次證據。
- 未修改產品程式、測試、版本、標籤或封裝；本次未提交、合併、推送或建立 PR。
