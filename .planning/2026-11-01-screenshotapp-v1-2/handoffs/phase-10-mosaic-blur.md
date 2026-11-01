# Handoff — Phase 10：馬賽克與模糊

**日期**：2026-11-01

**狀態**：程式、測試、文件、量測及煙霧腳本完成；實機依計畫允許的「尚未驗證」路徑記錄。

**基準／分支**：`e57ef62` → `feature/phase-10-mosaic-blur`。使用者明確授權本階段完成後提交；不合併、不推送、不建立 PR。

## 完成內容

- [ADR-0020](../../../docs/adr/0020-effect-annotations.md) 固定強度、捨入、邊界、取樣、快取及隱私限制；ADR-0010／0011 已補修訂。
- [EffectAnnotation](../../../ScreenshotApp/Editor/Model/EffectAnnotation.cs)：不可變矩形效果、整塊命中、八個控制點、翻轉與至少 1×1。
- [PixelEffects](../../../ScreenshotApp/Editor/Rendering/PixelEffects.cs)：區塊平均、三輪可分離盒狀模糊；滑動總和與逐列記憶體存取，輸入不變、輸出不透明。
- [EffectBitmapCache](../../../ScreenshotApp/Editor/Rendering/EffectBitmapCache.cs)：凍結原圖弱參照分組，完整 Rect／種類／Strength 鍵，每張原圖 8 筆／64 MiB 上限，超大單筆不保存。
- 畫布與匯出共用 AnnotationPainter 並傳入原圖；只取樣原圖，蓋住下層標註，保留上層，先整張合成再裁切。
- 編輯器新增 Alt+J 馬賽克、Alt+H 模糊、Alt+U 強度；強度只在工作階段內，選取效果時不適用的顏色／線寬／字型欄位停用。移動／調整／強度／再製／層序／刪除都沿用命令及復原。

## 驗證

- `pwsh -NoProfile -File tools/Invoke-Quality.ps1`：Release 0 警告／0 錯誤，1036／1036 測試通過，無跳過；新增 45 個案例。
- [自動化摘要](../../../docs/evidence/phase-10/automated-validation.json)：TRX 路徑、受測組件 SHA-256、測試數及未驗證範圍。原始 TRX 在 artifacts，不提交。
- 獨立參考演算法、原圖不變、不透明、層序、負座標／圖片邊界、裁切、PNG／DIB／存檔一致、命令粒度與快取筆數淘汰皆通過。
- [4K 量測](../../../docs/evidence/phase-10/performance.json)：單一 STA、5 次移動左邊界、含取樣及凍結點陣圖建立；馬賽克 12px 中位數 51.1ms，模糊 6px 424.3ms。大範圍同步模糊仍可能阻塞 UI，不宣稱流暢。
- [煙霧結果](../../../docs/evidence/phase-10/phase10-results.txt)：NOT_RUN，既有使用者 ScreenshotApp PID 28076 執行中，未送出模擬輸入。腳本語法及獨立參考演算法已成功編譯。

## 尚未驗證與重跑

實機拖曳／移動／調整的手感、PrintWindow、剪貼簿／另存對話框、GDI／USER 循環與 MT-14 PowerPoint 貼上尚未驗證。使用者結束既有程式後，在閒置時執行：

```powershell
pwsh -NoProfile -STA -File tools/smoke/Invoke-Phase10Smoke.ps1 -ExePath ScreenshotApp/bin/Release/net10.0-windows/ScreenshotApp.exe -EvidenceDirectory docs/evidence/phase-10
dotnet run --project tools/benchmarks/Effects.csproj -c Release -- docs/evidence/phase-10/performance.json
```

煙霧先等至少 90 秒閒置；既有程式、登入啟動登錄值、快捷鍵占用或閒置逾時均停止。備份／還原設定與剪貼簿，偵測到使用者操作即停止，只結束腳本建立的處理程序。執行期間不要發出其他主控台指令搶走前景。量測工具不顯示視窗、不操作桌面或剪貼簿。

效果不保證保密；原圖仍在編輯文件，未來啟用歷史後也會留存。PNG／剪貼簿只輸出合成像素。巨大範圍重算的暫時記憶體不包含在 64 MiB 結果快取上限內。

Phase 11 維持 pending。下一階段加入尺規、覆蓋層量測與編輯器取色，先載入本計畫及 ADR，再延伸工具／繪製流程；本次不開始。
