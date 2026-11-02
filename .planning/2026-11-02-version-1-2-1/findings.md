# Findings & Decisions

## Requirements
-

## Research Findings
-

## Technical Decisions
| Decision | Rationale |
|----------|-----------|

## Issues Encountered
| Issue | Resolution |
|-------|------------|

## Resources
-

## 2026-11-02 GitHub release discovery
- Clean main at 01cd4f2; CodeGraph exists and is the active discovery tool.
- Existing Apache-2.0 license. Authenticated gh account anthonywang6503; its ScreenshotApp repository is already PUBLIC and matches origin/README. upstream wanghy6503/ScreenshotApp cannot be resolved; do not push there.
- Current app 1.2.0; compatible language feature warrants 1.3.0.
- Strings.resx currently zh-TW, static XAML markup; preserve zh-TW as satellite, make neutral resources English. Persist Language en/zh-TW and apply before AppController creation; restart avoids mixed old/new window strings and static style caches.
- User authorizes commit/tag/upload via requested release; no PR. Preserve historical evidence and planning docs; make public maintained docs English-first with zh_TW references.
- gh repository lookup for old upstream failed; use verified origin target.

- Complete gate PASS: 1189/1189, zero skipped, Release zero warnings/errors; TRX artifacts/quality/1ca56b52e5694764ac4d3a95b9d4fc87/tests.trx.
- Full format check found pre-existing whitespace in unrelated files; modified C# files pass focused format verification. PowerShell release script parses without errors.
- Public link check found five old pre-archive planning links; corrected their paths in four historical proposal files. Recognizable credential pattern scan found no matches (not a security audit).
- origin p76871024 resolves through SSH configuration to github.com and matches verified GitHub main 7d75d338; existing origin is the correct publication target.
