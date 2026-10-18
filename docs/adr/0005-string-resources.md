# ADR-0005：文字資源策略——resx + 靜態存取 + XAML 標記延伸

- **狀態**：Accepted
- **日期**：2026-10-18

## 背景

v1.0 介面使用台灣繁體中文，規格要求文字資源集中管理，保留日後增加英文的可能性。WPF 在未指定語言時，中文字元可能由複合字型的後援機制選到簡體字形變體，使「台灣繁體中文」顯示不正確。

## 決策

1. 所有使用者可見文字集中於 `ScreenshotApp/Resources/Strings.resx`；其內容為 zh-TW，專案設定 `NeutralLanguage=zh-TW`，執行階段不需要附屬組件。
2. 存取方式：
   - 程式碼：`Strings.Get("Key")`、`Strings.Format("Key", args)`。
   - XAML：標記延伸 `{res:Str Key}`。
   - 找不到鍵時回傳 `!Key!`，讓缺漏在畫面上明顯可見而不是讓程式當掉。
3. 鍵名格式為 `區域_名稱`，例如 `Tray_Settings`、`Settings_Title`、`Hotkey_ErrorInUse`。
4. 自動化測試保證：程式碼與 XAML 引用的每個鍵都存在於 `Strings.resx`；XAML 的 `Content`、`Text`、`Title`、`ToolTip`、`Header` 等屬性不得寫死文字。
5. 啟動時將所有 WPF 元素的預設 `Language` 設為 `zh-TW`，讓 WPF 為中文字元選用繁體中文字型。
6. 日後加入英文時新增 `Strings.en.resx` 附屬資源即可；介面語言切換不在 v1.0 範圍。

## 考慮過的替代方案

- **Visual Studio 產生的 `Strings.Designer.cs` 強型別類別**：命令列建置不會重新產生，手動維護容易與 resx 脫節。
- **`x:Static` 引用強型別屬性**：同樣依賴產生的類別。
- **WPF `ResourceDictionary` 字串**：程式碼端取用不便，且無法沿用 resx 的在地化工具鏈。

## 影響

- 鍵以字串表示，拼字錯誤無法在編譯期發現，由自動化掃描測試補足。
- 文字與程式碼分離，日後翻譯不需修改程式。
