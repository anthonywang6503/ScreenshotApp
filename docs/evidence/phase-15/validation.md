# 1.3.0 validation — 2026-11-02

## Source and automated checks

- Implementation commit: `fa94d83659d0447596446738028761d86823e999` (release evidence/notices are committed subsequently).
- `pwsh -NoProfile -File tools/Invoke-Quality.ps1`: locked restore PASS; Release build 0 warnings/0 errors; **1189/1189 tests passed, 0 skipped**.
- Language coverage: English/zh-TW resource loading, new-thread UI culture, XAML settings title, saving the selector, fresh/legacy defaults, invalid-language isolation, both resource key sets/nonempty values/composite-format arguments.
- `dotnet format ... --verify-no-changes --include <changed C# files>`: PASS. A repository-wide format check also found existing whitespace issues outside this change; unrelated files were not reformatted.
- PowerShell parser on `tools/Publish-Release.ps1`: PASS.
- Public Markdown: 51 files and 529 local links checked, PASS. Five old planning links were updated to archived locations.
- `git diff --check`: PASS. A bounded recognizable-credential pattern scan found no matches in current tracked/new text; this is not a security audit or a history scan.

## Post-commit package verification

`tools/Publish-Release.ps1` generated folder and single-file self-contained archives. Only the folder archive is eligible for this release's public download.

- Folder: 281 files, approximately 156.1 MiB unpacked and 67.4 MiB ZIP.
- Runtime configuration: Microsoft.NETCore.App **10.0.14**, Microsoft.WindowsDesktop.App **10.0.14**, both included frameworks.
- ZIP decompression/CRC and equality of required packaged files against output: PASS.
- Included: executable/DLL/runtime configuration, `zh-TW/ScreenshotApp.resources.dll`, Apache license, actual runtime licenses/notices, bilingual user guide/limitations/notices and a download README.
- Loaded the **published folder assembly** in an isolated .NET probe; ResourceManager returned `Exit (&X)` for English and `結束(&X)` for zh-TW.
- File version `1.3.0.0`; informational product version `1.3.0+<build commit>` matched the committed source.
- `SHA256SUMS.txt` contains only the public folder ZIP. Its final digest is compared with the GitHub asset digest after the tagged rebuild/upload.

These checks do not start ScreenshotApp, drive desktop input, change clipboard contents or write user preferences/history/startup settings. The final tagged package is rebuilt from the release commit and rechecked before upload. Exact asset sizes/digests and remote CI status are available from [GitHub Releases](https://github.com/anthonywang6503/ScreenshotApp/releases/tag/v1.3.0) and [Actions](https://github.com/anthonywang6503/ScreenshotApp/actions).

## Explicitly unverified

Interactive tray/capture/export/language restart smoke, clean Windows without an installed runtime, mixed-DPI physical input, IME interaction and external application clipboard/scrolling compatibility: **NOT_RUN** for 1.3.0. Older desktop evidence is not reused as a current pass. The single-file download is **WITHHELD** until version-specific interactive smoke passes.
