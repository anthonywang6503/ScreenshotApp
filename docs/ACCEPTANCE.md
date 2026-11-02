# Acceptance record

[Traditional Chinese historical evidence and manual test procedures](ACCEPTANCE_zh_TW.md) · [Quality gate](QUALITY.md)

## 1.3.2 — 2026-11-02

This release adds English-default English/Traditional Chinese UI selection and English-first public documentation. Language changes are saved and take effect after exiting from the tray and restarting.

| Check | Result |
| --- | --- |
| Locked restore, zero-warning Release build, complete automated suite | PASS: 1189/1189 tests, 0 skipped, 0 warnings/errors |
| Both resource sets, format arguments, migration, invalid-language fallback and settings UI | PASS: language/resource tests included in the complete 1189-test run |
| Published folder package with zh-TW satellite, licenses, bilingual guides and SHA-256 | PASS: ZIP integrity/required files; 281 files; actual English/zh-TW resource loading and embedded version checked |
| Interactive tray/capture/export/language restart smoke | NOT_RUN |
| Clean Windows 11 x64 without .NET installed | NOT_RUN |
| Mixed-DPI, IME, third-party clipboard and scrolling-target compatibility | NOT_RUN for this release |
| Single-file download | Withheld pending version-specific interactive smoke |

No previous desktop result is claimed as a 1.3.2 desktop pass. Automated tests create isolated data and hidden windows; they do not show the application or drive the user's desktop.

Packaging verification resolved .NET and Windows Desktop Runtime 10.0.14. The final tagged rebuild and downloadable asset digest are verified during GitHub publication; see [release evidence](evidence/phase-15/validation.md) and [the release](https://github.com/anthonywang6503/ScreenshotApp/releases/tag/v1.3.2). Packaging checks load resources without starting the application or writing user settings.

## Packaging correction

The v1.3.0 tag was pushed but not released. Its packaging-generated source lock caused NU1004 on later locked restore. Version 1.3.2 restores the source lock and isolates runtime/variant locks under publish/. The release gate includes post-packaging locked restore and a source-lock hash check. The old tag is retained; no downloadable v1.3.0 Release is published.

## Windows checkout correction

The v1.3.1 lock fix passed local full tests and post-package restore, but GitHub Windows checkout converted multiline resx content to CRLF. Five overlay assertions expected LF and failed. Version 1.3.2 adds .gitattributes to enforce LF at checkout, retaining the real assertions and all tests. Neither earlier tag has a published Release. The final tag is created only after successful remote CI.

## Previous baseline

The local 1.2.0 integration gate passed **1176/1176** tests with zero Release warnings. Its new magnifier/effects/ruler/history/scrolling features had automated coverage; desktop smoke and MT-12–MT-15 were not completed. Its local ZIP generation was not a GitHub release.

Version 1.1.0 had witnessed folder/single-file smoke evidence, including context menus/cropped fit and platform/selection checks. These results apply to those older builds only. Detailed counts, dates, screenshots and raw files remain unchanged in the [Chinese evidence record](ACCEPTANCE_zh_TW.md) and [evidence/](evidence/).

## Manual release checks still required

On an idle desktop, verify the exact published build: tray appears, Ctrl+Shift+2 opens selection, cancel preserves work, capture opens the editor, annotations/crop/undo work, copy and PNG match, language can be saved and activated after tray exit/restart in both directions, and disabling/clearing history behaves as documented. Test automatic/manual scrolling with overlapping content, input interruption, cancel and limit handling. Verify mixed-DPI and IME on suitable machines and external application clipboard pasting. Use the guarded [smoke scripts](../tools/smoke/) where available; record version, executable path and evidence. Never replace an unrun item with a pass inferred from source or automated tests.
