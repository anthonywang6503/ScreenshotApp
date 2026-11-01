# Handoff — Phase 11：尺規、量測與編輯器取色

**日期**：2026-11-02

**狀態**：程式、測試、文件與煙霧腳本完成；實機依計畫允許的「尚未驗證」路徑記錄。

**基準／分支**：`352b53a` → `feature/phase-11-ruler-eyedropper`。依使用者要求在目前分支提交，不建立 PR、不推送；Phase 12 維持 pending。

## 完成內容

- [ADR-0021](../../../docs/adr/0021-ruler-and-eyedropper.md)：像素中心距離、包含端點的寬高、顯示捨入、標籤、互動與原圖取色；模型及文字輸入 ADR 已修訂。
- [PixelMeasurement](../../../ScreenshotApp/Editor/Model/PixelMeasurement.cs) 共用純計算，double 差值避免整數平方溢位。
- [RulerAnnotation](../../../ScreenshotApp/Editor/Model/RulerAnnotation.cs)：線段端點、垂直界線、白底黑字標籤、顏色與線寬；標籤限制在原圖，極小圖片縮小；所有修改沿用命令與復原。
- 覆蓋層按 R 切換量測，放開保留、下一次拖曳清除舊線，不建立文件；Ctrl 放大鏡、C 取色、Esc／右鍵取消仍有效。
- 編輯器 Alt+Q 尺規、Alt+D 取色；取色保留原工具與文字輸入，使用原圖 RGB 並沿用 SetColor 與預設設定儲存。先驗證座標、再驗證換算後像素，圖片外不夾取。

## 驗證

- `pwsh -NoProfile -File tools/Invoke-Quality.ps1`：Release 0 警告／0 錯誤，1072／1072 測試通過，無跳過；比 Phase 10 新增 36 個案例。
- [自動化摘要](../../../docs/evidence/phase-11/automated-validation.json) 記錄 TRX 與組件 SHA-256；TRX 為 `artifacts/quality/9272029a78fc4b2c8c2f554ac5795657/tests.trx`，不提交。
- 畫布與輸出逐像素一致、縮放不影響輸出、裁切不重新定位標籤、PNG 存檔與剪貼簿酬載一致；測試只使用暫存設定及未顯示視窗。
- 取色邊界回歸先重現 CopyPixels 例外後修正；文字資源掃描曾無法辨識條件式鍵，已改為明確呼叫並通過。
- [煙霧結果](../../../docs/evidence/phase-11/phase11-results.txt)：NOT_RUN，既有使用者 ScreenshotApp PID 28076 執行中；未送出輸入，沒有改變使用者設定、剪貼簿或文件。

## 實機重跑與界線

在既有程式結束後，閒置至少 90 秒再執行：

```powershell
pwsh -NoProfile -STA -File tools/smoke/Invoke-Phase11Smoke.ps1 -ExePath ScreenshotApp/bin/Release/net10.0-windows/ScreenshotApp.exe -EvidenceDirectory docs/evidence/phase-11
```

腳本使用已知圖樣，驗證覆蓋層 300／400／500 三角形、編輯器 300px 尺規輸出與復原、原圖取色後建立矩形的 RGB。每次輸入前檢查前景與使用者活動，備份／還原設定及剪貼簿，只結束腳本建立的程式。執行期間不要啟動其他主控台指令搶前景。

真實鍵鼠、PrintWindow、多螢幕混合 DPI、微軟注音組字中取色及 PowerPoint 貼上尚未驗證。標籤固定像素單位、白底黑字；裁切可能裁掉標籤，不會重新排版。

## 下一階段接點

Phase 12 尚未開始。歷史 JSON 必須包含新增尺規的 Start／End／StrokeWidth／Color；`ImageBounds` 與原圖尺寸一致，還原時以原圖重建或驗證，不信任未驗證的外部邊界。顯示文字與量測值可由端點重新計算，不必序列化。暫時取色、覆蓋層量測、拖曳預覽與文字輸入均不屬於歷史文件內容。
