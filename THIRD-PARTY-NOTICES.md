# 第三方聲明

ScreenshotApp 的原始碼不包含第三方程式碼，也沒有複製 ShareX、Greenshot 或其他開源專案的程式碼（只參考其操作行為）。

## 隨發佈版本散布的元件

自包含發佈版本（`win-x64`）內含下列 Microsoft 元件，依 MIT 授權散布。授權檔由 `tools/Publish-Release.ps1` 從實際使用的 Runtime 套件複製到發佈目錄的 `licenses/`：

| 元件 | 版本 | 授權 | 發佈目錄中的授權檔 |
|------|------|------|------------------|
| .NET Runtime（Microsoft.NETCore.App） | 10.0.13 | MIT | `licenses/Microsoft.NETCore.App.LICENSE.txt`、`licenses/Microsoft.NETCore.App.THIRD-PARTY-NOTICES.txt` |
| Windows Desktop Runtime（Microsoft.WindowsDesktop.App：WPF、Windows Forms） | 10.0.13 | MIT | `licenses/Microsoft.WindowsDesktop.App.LICENSE.txt` |

.NET Runtime 內含的其他第三方元件及其授權列於 `Microsoft.NETCore.App.THIRD-PARTY-NOTICES.txt`。

## 只用於開發與測試（不隨發佈版本散布）

| 套件 | 版本 | 授權 |
|------|------|------|
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT |
| xunit | 2.9.3 | Apache-2.0 |
| xunit.runner.visualstudio | 3.1.4 | Apache-2.0 |
