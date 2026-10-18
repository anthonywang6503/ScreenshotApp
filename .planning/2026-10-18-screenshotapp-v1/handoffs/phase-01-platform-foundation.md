# Handoff — Phase 1：Windows 平台基礎

**日期**：2026-10-18
**狀態**：complete
**分支**：`feature/phase-01-platform-foundation` → fast-forward 合併回 `main`

## 1. 完成內容

| Item | 產出 |
|------|------|
| P1-00 | `docs/adr/0001`～`0006`（技術堆疊、座標系統、單一執行個體、設定持久化、文字資源、系統匣與 WinForms 邊界），皆 Accepted |
| P1-01 | `ScreenshotApp.sln`（傳統格式）、`ScreenshotApp/ScreenshotApp.csproj`、`ScreenshotApp.Tests/ScreenshotApp.Tests.csproj`、`README.md` |
| P1-02 | `app.manifest`：PerMonitorV2 + `true/pm` 後援、asInvoker、Common Controls v6；啟動時非 PMv2 才寫日誌 |
| P1-03 | `Platform/NativeMethods.cs`（`LibraryImport`：RegisterHotKey、UnregisterHotKey、DPI 感知查詢、AllowSetForegroundWindow）、`Platform/DpiAwareness.cs` |
| P1-04 | `App.xaml(.cs)`（`OnExplicitShutdown`、zh-TW 語言、啟動失敗時告知後結束）、`AppController.cs`、`GlobalExceptionHandler.cs` |
| P1-05 | `Resources/Strings.resx`、`Strings.cs`、`StrExtension.cs`（`{res:Str Key}`） |
| P1-06 | `Platform/TrayService.cs`（「設定」「結束」、雙擊開設定、工具提示顯示快捷鍵狀態、通知） |
| P1-07 | `Platform/HotkeyService.cs`（message-only `HwndSource`、`MOD_NOREPEAT`、先註冊新再解除舊）、`Settings/HotkeyGesture.cs` |
| P1-08 | `Platform/SingleInstanceGuard.cs` |
| P1-09 | `Platform/StartupService.cs` |
| P1-10 | `Settings/AppSettings.cs`、`SettingsStore.cs`、`ColorText.cs`、`CommonColors.cs` |
| P1-11 | `Settings/SettingsWindow.xaml(.cs)`、`Platform/ColorPicker.cs` |
| P1-12 | `Platform/ErrorLog.cs` |
| P1-13 | 系統匣「結束」→ `Application.Shutdown` → `AppController.Dispose`（解除快捷鍵、移除圖示） |
| P1-14 | `tools/New-AppIcon.ps1` → `Resources/app.ico`（16/20/24/32/40/48/64 DIB + 256 PNG；由 subagent 產生並驗證） |
| P1-15 | `tools/smoke/Invoke-Phase1Smoke.ps1`；證據 `docs/evidence/phase-01/` |
| P1-16 | 11 個測試類別、114 個測試案例 |
| 文件 | `docs/ACCEPTANCE.md` 初版（AT、煙霧驗證、MT 清單與 Phase 1 驗證步驟） |

## 2. 設計原因

- **計畫調整（已記錄於 Decisions）**：GDI SafeHandle 移至 P2-04、狀態機移至 P2-08，與其唯一使用者一起實作；系統匣不放尚未實作的「開始截圖」「開啟圖片」；DPI 只在異常時記錄。
- **系統匣圖示在快捷鍵註冊後才顯示**：煙霧驗證發現「圖示出現 → 快捷鍵尚未註冊」的時間窗會讓外部檢查誤判，甚至讓 App 的註冊因短暫衝突而失敗；改為圖示出現即代表常駐服務已就緒。
- **`BeginInvoke` 而非 `InvokeAsync`**：`InvokeAsync` 會把例外存進工作，繞過全域例外處理。
- **啟動失敗時結束程式**：常駐服務建立失敗代表沒有系統匣可操作，留在背景只會是看不見的處理程序。
- **無法開啟既有具名 Mutex（既有執行個體以系統管理員身分執行）**：視為已有執行個體，避免當機與重複圖示；先以測試重現 `UnauthorizedAccessException` 再修正。
- **設定讀取失敗時不寫回**：避免覆蓋只是暫時無法讀取的有效設定檔；損毀時先備份為 `settings.corrupt.json`。
- **設定檔快捷鍵註冊失敗時保留使用者偏好**：不自動改寫成其他組合，下次啟動會再試；設定視窗按「套用」時也會重試。

## 3. 驗證結果與證據

| 驗證 | 結果 |
|------|------|
| `dotnet build ScreenshotApp.sln -c Release` | 0 error、0 warning |
| `dotnet test ScreenshotApp.sln -c Release` | 114 passed、0 failed |
| 煙霧驗證（Release exe，Windows 11 Pro 10.0.26200，單一顯示器 3456×2160 @175%） | 31 項全數通過（含自訂顏色流程），連續 4 次無失敗 |
| 程式碼註解掃描（不得引用編號或文件路徑） | 無違規 |

證據：`docs/evidence/phase-01/`（結果清單與 5 張截圖）；細節見 `docs/ACCEPTANCE.md`。

煙霧驗證發現並修正的產品問題：系統匣圖示先於快捷鍵註冊出現（改為註冊後顯示）；設定視窗色彩清單 4 欄時「自訂 #12ABEF」被截斷（改為 3 欄，8 個常用色加自訂色排成 3×3）。

煙霧驗證過程中遇到的測試工具問題（皆已修正，非產品缺陷）：從桌面根節點搜尋 UI Automation 太慢、設定視窗可見後內容尚未就緒、`SetFocus` 偶爾未生效（已加重試，且未取得焦點時不送出按鍵）、截圖遇到開窗動畫或視窗被遮擋（改用 `PrintWindow`）、WinForms 下拉選單不支援 `PrintWindow`（選單改由螢幕擷取）、受控 UI Automation 用戶端沒有 Win32 控制項代理（Windows 色彩對話框改以視窗訊息操作）。

## 4. 風險

- **混合 DPI 無法在本機驗證**：本機只有一顆 175% 顯示器；Phase 2 的覆蓋層定位需靠單螢幕多種縮放與使用者的 MT-04／MT-05。
- **系統通知可能被「專注」模式抑制**：快捷鍵失敗原因同時顯示在工具提示與設定視窗，不只依賴通知。
- **WPF Label 的 UI Automation 樹含原始存取鍵文字**：UI Automation 讀到同時有「快捷鍵(H)：」與「快捷鍵(_H)：」兩個文字元素，螢幕閱讀器可能多唸一次；列入 P5-01 操作性總檢。
- **煙霧腳本會操作真實桌面**：執行期間設定視窗與色彩對話框會短暫出現，並暫時寫入／移除 `HKCU\...\Run`（結束時還原）。
- **設定檔讀取失敗後再套用會覆寫原檔**：啟動時若設定檔暫時無法讀取（例如被鎖定），本次使用預設值並提示；之後使用者在設定視窗按「套用」，寫入的是畫面上的值，原檔中其他欄位會被取代。情境罕見且有提示，暫不處理。

## 5. 後續工作（Phase 2）

1. 從 `main` 建立 `feature/phase-02-capture-selection`（`feature/phase-01-platform-foundation` 已合併，保留但不要從它分出），Phase 2 → `in_progress`，先寫 ADR-0007～0009。
2. P2-04 需一併實作 `SafeHdc`／`SafeHBitmap`／`SafeGdiObject` 與 `SafeHandleTests`。
3. P2-08 需實作 `AppStateMachine`（含 `AppStateMachineTests`），並把 `HotkeyService.Pressed` 與新增的系統匣「開始截圖」接到同一個截圖入口（目前 `AppController` 尚未訂閱 `Pressed`）。
4. 可重用：`TestSupport/StaRunner`、`TempDirectory`、`RepositoryPaths`；煙霧腳本的 Win32 + UI Automation 寫法（含等待內容就緒、只在取得焦點時送鍵、`PrintWindow` 截圖）。
5. 設定中的 `DefaultColor`／`DefaultStrokeWidth`／`DefaultFontSize` 在 Phase 3 由編輯器讀取；`LastSaveDirectory` 在 Phase 4 由另存流程更新。

## 6. 未驗證項目

- MT-01（首次啟動只進匣、重複啟動、退出重啟）：Agent 端已自動化驗證行為；圖示外觀、從檔案總管雙擊時設定視窗是否來到前景，需使用者目視。
- MT-02 占用提示：Agent 端已驗證設定視窗文字；系統通知外觀需使用者目視。
- MT-02 在各應用程式中觸發截圖：Phase 2 才有截圖動作。
