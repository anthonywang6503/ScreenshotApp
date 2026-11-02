# Handoff — Phase 12：截圖歷史

**日期**：2026-11-02

**狀態**：程式、測試、文件及煙霧腳本完成；實機依計畫允許的「尚未驗證」路徑記錄。Phase 13 維持 pending。

**基準／分支**：`71fb4ff` → `feature/phase-12-capture-history`；依使用者要求在目前分支提交，不合併、不推送、不建立 PR。

## 完成內容

- [ADR-0022](../../../../docs/adr/0022-capture-history.md)：預設開啟、固定磁碟位置、版本 1 JSON、序列背景寫入、同目錄暫存後取代、損毀回退、隱私及結束等待；修訂設定與未匯出保護 ADR。
- [HistoryJson／HistorySnapshot](../../../../ScreenshotApp/History/HistorySnapshot.cs)：九種標註與裁切、建立時間、識別碼、顏色及樣式；全部驗證成功才採用。尺規邊界由原圖重建，不保存復原／預覽／輸入狀態。
- [HistoryStore](../../../../ScreenshotApp/History/HistoryStore.cs)：原圖與原圖縮圖、JSON 最後提交；新增、更新、列出、讀取、刪除、清除及保留淘汰共用背景佇列。停用使寫入及維護世代失效；已刪除條目不重建。拒絕路徑跳脫及重新解析點。
- [控制器整合](../../../../ScreenshotApp/AppController.History.cs)：新截圖建立條目；文件取代、關閉、結束及成功匯出後保存。弱參照關聯不留住原圖；停用解除關聯、重新啟用不補存。原圖回退不建立可寫關聯。保存不修改匯出版本。
- 系統匣與編輯器歷史入口（Alt+X）；歷史視窗提供原圖縮圖、時間、尺寸、開啟、合成複製、刪除及確認後全部清除。設定預設啟用、50 筆，範圍 1～500；固定位置、隱私說明與捲動版面。

## 驗證

- 最終 `tools/Invoke-Quality.ps1`：Release 0 警告／0 錯誤，1127／1127 測試通過，無跳過，新增 55 案例。
- TRX：`artifacts/quality/61601d1580894aecbde52d9414d26cc7/tests.trx`；[摘要及受測組件 SHA-256](../../../../docs/evidence/phase-12/automated-validation.json)。
- 全部標註往返及還原合成 PNG 相同；磁碟順序、損毀、淘汰、關閉、清除、失敗與生命週期測試只用暫存目錄。歷史複製以委派替身取代真實剪貼簿，控制器未啟動、不寫登入登錄值。
- 收尾先重現存取鍵衝突及停用後待執行維護仍會清理的錯誤，再修正並通過回歸；另涵蓋登出被其他程式取消後仍可更新歷史。第一次測試替身出現可為 null 的警告已消除。
- 未顯示 WPF 內容的 RenderTargetBitmap 預覽確認清單與設定欄位可見；這不是桌面操作證據。
- [煙霧結果](../../../../docs/evidence/phase-12/phase12-results.txt)：NOT_RUN，既有使用者 ScreenshotApp PID 27708。未送出輸入或變更使用者設定／剪貼簿／歷史。

## 實機重跑與界線

在既有程式結束、歷史資料夾為空且閒置至少 90 秒時：

```powershell
pwsh -NoProfile -STA -File tools/smoke/Invoke-Phase12Smoke.ps1 -ExePath ScreenshotApp/bin/Release/net10.0-windows/ScreenshotApp.exe -EvidenceDirectory docs/evidence/phase-12
```

建議在 Windows 沙箱或測試帳號執行。腳本拒絕非空歷史，不會為測試搬移或清除使用者資料；備份並還原設定及剪貼簿，只結束自己的程序。驗證三次截圖、標註更新、正常結束與重啟、列表、還原合成像素、保留兩筆淘汰、刪除及確認後清除。

Phase 2～4 啟動前寫入 `HistoryEnabled=false`；9～11 已有此設定。歷史停用時不執行清理／淘汰。但本次因既有程式而未重跑桌面驗證，不宣稱已實測檔案雜湊不變。

首次通知、真實鍵鼠、長時間常駐、作業系統結束工作階段、磁碟／記憶體占用及 MT-15 尚未驗證。歷史未加密，縮圖代表原圖，復原堆疊不保存；退出逾時或強制終止可能遺失最新更新。

## Phase 13 接點

Phase 13 尚未開始。長截圖成功得到凍結圖片後建立 `ImageDocument`，呼叫 `RecordCapture(document)` 再走 `OpenEditor`；取消、失敗或只量測不記錄。忙碌守衛需擴充至長截圖整段工作階段；文件來源與座標仍遵循現行規格。不可透過重新啟用歷史補存停用期間的文件。
