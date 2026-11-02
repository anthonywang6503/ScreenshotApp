# Handoff — Phase 14：v1.2.0 整合驗證與本機封裝

**日期**：2026-11-02

**交付狀態**：品質檢查、文件總檢、版本提交、本機標籤、兩種 ZIP 封裝與前置守衛證據完成。Phase 14 complete 表示本機交付完成；桌面煙霧、P13-00 與 MT 仍未驗證。

**分支／基準**：`feature/phase-14-release-1-2`，接續 `51011c2`。版本提交 `1e443dcfb596c8de0a29476951d9c2a0c2f76020`，本機標籤 `v1.2.0` 指向該提交；後續證據提交不移動標籤。依使用者指令完成後在目前分支提交，不合併、不建立 PR、不推送、不上傳 Releases。

## 已完成

- 專案 `Version` 為 1.2.0，manifest `assemblyIdentity` 為 1.2.0.0；CHANGELOG 建立本版段落並保留空的 Unreleased。
- README、USER_GUIDE、KNOWN_LIMITATIONS 同步七項功能、入口與快捷鍵；明示舊版升級後截圖歷史預設開啟，原圖包含裁切／遮蔽之前內容且未加密。
- ARCHITECTURE 補齊歷史模組、相依圖、測試對應與 ADR 0018～0023；更正 PNG 不可拆回標註與歷史可重新編輯的界線。
- ACCEPTANCE 新增 Phase 14、MT-12～MT-15 狀態與實機操作步驟；舊版延遲、畫面與煙霧結果明確保留為歷史證據。
- 完整品質入口通過：1176／1176、無跳過、Release 0 警告／0 錯誤，使用 `BaseOutputPath=bin/phase14/` 避開使用者執行檔鎖定。11 支煙霧腳本 PowerShell AST 通過。

## 證據

- [自動化摘要、TRX 路徑與受測組件 SHA-256](../../../docs/evidence/phase-14/automated-validation.json)。自動化組件建置於版本提交前；發佈組件來源另行記錄，不混用雜湊。
- [P13-00 的 12 格滾輪矩陣](../../../docs/evidence/phase-14/wheel-probe-matrix.csv)：既有使用者 ScreenshotApp PID 35760 執行中，守衛回傳 NOT_RUN，未操作桌面。
- [封裝驗證與 SHA-256](../../../docs/evidence/phase-14/release-validation.json)：資料夾版 276 檔／67.4 MiB ZIP，單檔版 9 檔／63.6 MiB ZIP。兩者產品版本皆 `1.2.0+1e443dcfb596c8de0a29476951d9c2a0c2f76020`，內嵌 manifest 1.2.0.0、PerMonitorV2、asInvoker；ZIP 每個檔案的 CRC／SHA-256 與輸出目錄相同，隨附文件與標籤相同，兩個 Runtime 的授權齊全。
- [煙霧總表](../../../docs/evidence/phase-14/smoke-results.csv)／[文字結果](../../../docs/evidence/phase-14/smoke-results.txt)：兩種封裝各 9 項共 18 項 NOT_RUN。Phase 9～13 共 10 次入口於腳本守衛停止，Phase 1～4 共 8 項由外層守衛擋住、未啟動腳本；未啟動受測程式或注入桌面輸入。各入口的結果另保存在 folder／singlefile 子目錄。
- 封裝輸出（不納入 Git）：`publish/ScreenshotApp-1.2.0-win-x64.zip` 與 `publish/ScreenshotApp-1.2.0-win-x64-singlefile.zip`。主封裝 SHA-256：`913644588f03a27097e4e218dcdd9ff7dddf1bc06d290521e56d73ca178caf1f`；單檔：`260dca5188a8c67bf116e95dc33f8f5260b82688492397de98034627d1bfc216`。
- 發佈工具的單檔還原會改寫應用程式 NuGet lock，加入 SDK 隱含 ILLink／RID；已只還原這次工具產生的 lock 變更，封裝後 locked restore 通過。沒有產品相依升級或發佈工具程式變更。

## 續作及驗證界線

- 兩種封裝已從本機 `v1.2.0` 所指版本建立；補證據的文件提交不移動標籤或重建封裝。重建指令：

```powershell
$env:BaseOutputPath = 'bin/phase14/'
pwsh -NoProfile -File tools/Publish-Release.ps1
```

  若要重現本次內嵌 commit，必須先在乾淨 checkout 選擇 `v1.2.0`，不可直接以後續證據提交重建並聲稱雜湊相同。
- 既有程式執行時，不關閉它、不啟動第二份 ScreenshotApp，保留使用者文件與設定；桌面煙霧記錄尚未驗證。
- 單檔尚未通過此版系統匣、快捷鍵、截圖與複製煙霧，依 [ADR-0017](../../../docs/adr/0017-release-packaging.md) 僅產生待驗證封裝，不提供為已驗收版本。
- MT-12～MT-15、長截圖的 P13-00、乾淨 Windows 11 無 .NET Runtime 執行、混合 DPI、PowerPoint 貼上與長時間歷史／資源趨勢均待實測；步驟見 [驗收紀錄](../../../docs/ACCEPTANCE.md)。
- 重跑煙霧必須使用無敏感內容、使用者閒置至少 90 秒的測試桌面；Phase 12 需空白歷史目錄，禁止為通過守衛而刪除使用者條目。
