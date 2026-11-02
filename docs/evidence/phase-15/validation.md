# 1.3.2 validation — 2026-11-02

## Source and automated checks

- Language implementation: `fa94d83659d0447596446738028761d86823e999`; packaging-lock correction and 1.3.1 version: `fe31ce9e2c4118483f827d86f2319c38910c0a80` (evidence-only updates follow).
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
- File version `1.3.2.0`; informational product version `1.3.2+<build commit>` matched the committed source.
- `SHA256SUMS.txt` contains only the public folder ZIP. Its final digest is compared with the GitHub asset digest after the tagged rebuild/upload.

These checks do not start ScreenshotApp, drive desktop input, change clipboard contents or write user preferences/history/startup settings. The final tagged package is rebuilt from the release commit and rechecked before upload. Exact asset sizes/digests and remote CI status are available from [GitHub Releases](https://github.com/anthonywang6503/ScreenshotApp/releases/tag/v1.3.2) and [Actions](https://github.com/anthonywang6503/ScreenshotApp/actions).

## Explicitly unverified

Interactive tray/capture/export/language restart smoke, clean Windows without an installed runtime, mixed-DPI physical input, IME interaction and external application clipboard/scrolling compatibility: **NOT_RUN** for 1.3.2. Older desktop evidence is not reused as a current pass. The single-file download is **WITHHELD** until version-specific interactive smoke passes.

## Packaging lock regression

The earlier v1.3.0 tag was not released: single-file publish rewrote the source lock with a RID and implicit ILLink task dependency, causing NU1004 in later locked restore and both GitHub CI runs. Version 1.3.1 uses isolated query/variant locks under publish/ and checks the source-lock SHA-256 before/after packaging. After generating both variants, the source lock remained byte-for-byte unchanged and `dotnet restore ScreenshotApp.sln --locked-mode --nologo -warnaserror` passed. The complete corrected source gate again passed 1189/1189 with zero warnings/skips. Historical smoke scripts still contain Chinese label assumptions and are explicitly unvalidated for this bilingual release.

## Windows checkout regression

GitHub run 41404048273 at the earlier v1.3.1 candidate passed locked restore/build but failed five of 1189 tests: multiline Selection_Pixel resources contained CRLF after Windows checkout while overlay assertions expected LF. The final 1.3.2 candidate adds .gitattributes (`* text=auto eol=lf`), preserving assertions and enforcing the project text policy. No Release was created for v1.3.0/v1.3.1. The final commit must pass main CI before tagging.

## Published result

Published [v1.3.2](https://github.com/anthonywang6503/ScreenshotApp/releases/tag/v1.3.2) as the latest public release, not a draft or prerelease. The tag resolves to `dc985d8d24f1373aef8ca091d79766f192a57e1a`; the executable embeds that commit. Both [main CI](https://github.com/anthonywang6503/ScreenshotApp/actions/runs/41404417197) and [tag CI](https://github.com/anthonywang6503/ScreenshotApp/actions/runs/41404623295) passed the complete 1189-test gate with zero skipped tests and zero build warnings/errors.

The download ZIP is **70700702 bytes**, SHA-256 `20aa947a16fa346cf770fcc63abef43db4550e0ee817d52f994ce34411d924de`. Both remote asset sizes/digests match local files, and their state is uploaded. Only the folder ZIP and SHA256SUMS.txt are release assets. Source/tag/package/CI details are retained in [release.json](release.json). The package was built from the exact commit now identified by the final tag; subsequent commits only record release evidence. Desktop acceptance remains NOT_RUN.
