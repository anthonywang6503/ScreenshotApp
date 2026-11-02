# 建置與品質檢查

## 執行環境

- Windows x64、PowerShell 7、Git、.NET SDK **10.0.403**。
- [global.json](../global.json) 固定 SDK 並停用版本前移；缺少指定 SDK 時會直接失敗，不會悄悄切換至預覽版或不同工具鏈。
- [產品套件鎖定檔](../ScreenshotApp/packages.lock.json) 與 [測試套件鎖定檔](../ScreenshotApp.Tests/packages.lock.json) 記錄相依版本與內容雜湊。產品仍沒有直接 NuGet 套件。

## 本機與 CI 共用的入口

```powershell
pwsh -NoProfile -File tools/Invoke-Quality.ps1
```

[腳本](../tools/Invoke-Quality.ps1) 依序執行：

1. `dotnet restore ScreenshotApp.sln --locked-mode --nologo -warnaserror`。
2. `dotnet build ScreenshotApp.sln -c Release --no-restore --nologo -warnaserror`。
3. `dotnet test ScreenshotApp.sln -c Release --no-build --no-restore --nologo`，輸出 TRX。
4. 檢查本次 TRX 包含測試且全部通過；沒有測試、跳過測試或缺少報告都會失敗。

任何一步失敗就停止，不會繼續回報成功。每次報告使用獨立的 `artifacts/quality/<GUID>/tests.trx`，避免採用前一次留下的報告。此目錄不納入 Git。

這是完整整合驗證入口；一般局部修改仍依 AGENTS.md 執行足以涵蓋影響的相關測試，不要求每次小修改都跑完整套件。若使用者正在執行 `bin/Release` 下的程式，先由使用者從系統匣結束，避免鎖住建置產物。

測試保留 WPF／Win32 案例，沒有新增 Skip 或篩選來迴避平台測試。此入口不啟動實機煙霧腳本、不模擬使用者桌面輸入、不發佈、不推送，也不修改使用者的設定、剪貼簿或開機啟動登錄值。

## 驗證品質檢查本身

修改入口腳本後執行：

```powershell
pwsh -NoProfile -File tools/Test-Quality.ps1
```

[負向驗證腳本](../tools/Test-Quality.ps1) 在 `artifacts/quality-self-test/<GUID>/` 建立原始碼副本，只在副本注入故障，檢查非零結束碼與對應診斷：

| 注入項目 | 必須拒絕的原因 |
|---|---|
| 產品新增 `#warning` | `CS1030`，建置警告不能通過 |
| 新增必然失敗的測試 | `QualityProbeTests.MustFail` 失敗 |
| 新增被跳過的測試 | TRX 的通過數不等於總數 |
| 改變套件版本但不更新 lock file | `NU1004`，不得自行重新解析相依版本 |

來源程式與既有測試不會被改寫。副本、測試報告與四份紀錄留在忽略目錄，方便查核。測試套件版本變更時，須一併更新負向驗證中的版本替換條件。

## Windows CI

[quality.yml](../.github/workflows/quality.yml) 在 push、pull request 或手動觸發時，以 `windows-2025` 安裝 global.json 指定的 SDK 並呼叫相同入口；工作逾時為 15 分鐘。成功或失敗均嘗試保留已產生的 TRX 14 天，還原／建置階段尚未產生 TRX 時不因缺少附件覆蓋原錯誤。

- 權限僅 `contents: read`；checkout 不持久化憑證；沒有遠端寫入、發佈或部署步驟。
- Actions 固定至查證過的 release commit：checkout v7.0.1、setup-dotnet v6.0.0、upload-artifact v7.0.2，不使用浮動標籤。
- 本機可用 `go run github.com/rhysd/actionlint/cmd/actionlint@v1.7.12 .github/workflows/quality.yml` 檢查 workflow；Go 不是產品建置或日常品質檢查的必要工具。
- **2026-10-29 驗證範圍**：本機入口最終 957 個測試全部通過、四項負向驗證通過、PowerShell 語法與 actionlint 通過。新增長序列測試另通過三項突變辨識驗證，詳見 [工程改善紀錄](GOAL_BACKLOG.md)。遠端 GitHub runner 尚未執行，尤其不能用本機通過推論其桌面／Win32 環境已驗證。

## 維護

有意更新 SDK 時修改 global.json，安裝該版本後重跑品質檢查；不需修改 workflow 中的版本。更新測試套件時，先修改專案檔，再以 `dotnet restore ScreenshotApp.sln --use-lock-file --force-evaluate` 更新鎖定檔，檢查版本差異後一起提交。文字檔維持 UTF-8、LF；不要手動改寫套件內容雜湊。

設定依據：[.NET SDK 選擇規則](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json)、[setup-dotnet](https://github.com/actions/setup-dotnet)、[checkout](https://github.com/actions/checkout)、[upload-artifact](https://github.com/actions/upload-artifact)、[actionlint](https://github.com/rhysd/actionlint)。
