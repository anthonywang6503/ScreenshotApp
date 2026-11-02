# Handoff — Phase 14：v1.2.0 整合驗證與本機封裝

**日期**：2026-11-02

**目前狀態**：品質檢查及文件總檢完成，準備提交版本、建立本機標籤後封裝；本檔將於封裝及煙霧前置檢查後補齊最終結果。桌面驗收未完成，不視為 MT 通過。

**分支／基準**：`feature/phase-14-release-1-2`，接續 `51011c2`。依使用者指令完成後在目前分支提交，不合併、不建立 PR、不推送、不上傳 Releases。

## 已完成

- 專案 `Version` 為 1.2.0，manifest `assemblyIdentity` 為 1.2.0.0；CHANGELOG 建立本版段落並保留空的 Unreleased。
- README、USER_GUIDE、KNOWN_LIMITATIONS 同步七項功能、入口與快捷鍵；明示舊版升級後截圖歷史預設開啟，原圖包含裁切／遮蔽之前內容且未加密。
- ARCHITECTURE 補齊歷史模組、相依圖、測試對應與 ADR 0018～0023；更正 PNG 不可拆回標註與歷史可重新編輯的界線。
- ACCEPTANCE 新增 Phase 14、MT-12～MT-15 狀態與實機操作步驟；舊版延遲、畫面與煙霧結果明確保留為歷史證據。
- 完整品質入口通過：1176／1176、無跳過、Release 0 警告／0 錯誤，使用 `BaseOutputPath=bin/phase14/` 避開使用者執行檔鎖定。11 支煙霧腳本 PowerShell AST 通過。

## 證據

- [自動化摘要、TRX 路徑與受測組件 SHA-256](../../../docs/evidence/phase-14/automated-validation.json)。自動化組件建置於版本提交前；發佈組件來源另行記錄，不混用雜湊。
- [P13-00 的 12 格滾輪矩陣](../../../docs/evidence/phase-14/wheel-probe-matrix.csv)：既有使用者 ScreenshotApp PID 35760 執行中，守衛回傳 NOT_RUN，未操作桌面。
- 發佈版封裝與煙霧的最終證據待提交版本後補入本目錄。

## 續作及驗證界線

- 兩種封裝均須從本機 `v1.2.0` 所指的版本提交建立，產品版本包含該 commit；補證據的文件提交不移動版本標籤。
- 既有程式執行時，不關閉它、不啟動第二份 ScreenshotApp，保留使用者文件與設定；桌面煙霧記錄尚未驗證。
- 單檔尚未通過此版系統匣、快捷鍵、截圖與複製煙霧，依 [ADR-0017](../../../docs/adr/0017-release-packaging.md) 僅產生待驗證封裝，不提供為已驗收版本。
- MT-12～MT-15、長截圖的 P13-00、乾淨 Windows 11 無 .NET Runtime 執行、混合 DPI、PowerPoint 貼上與長時間歷史／資源趨勢均待實測；步驟見 [驗收紀錄](../../../docs/ACCEPTANCE.md#v120-使用者實機驗證步驟mt-12mt-15)。
- 重跑煙霧必須使用無敏感內容、使用者閒置至少 90 秒的測試桌面；Phase 12 需空白歷史目錄，禁止為通過守衛而刪除使用者條目。
