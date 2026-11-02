# Progress Log

## Session: 2026-11-02

### Current Status
- **Phase:** 1 - Requirements & Discovery
- **Started:** 2026-11-02

### Actions Taken
-

### Test Results
| Test | Expected | Actual | Status |
|------|----------|--------|--------|

### Errors
| Error | Resolution |
|-------|------------|

- Discovery complete; feature/phase-15-bilingual-github-release created. Implementation in progress.

- Implemented persisted Language, English-first startup, WPF language and Settings selector with restart hint. Revised spec/ADR before code. Translating all resources.
- One no-op spec patch did not match; appended the revision directly without changing existing content.

- 281 resources in each language; complete key/placeholder checks and language persistence/startup/window tests added.
- First complete gate: 1188 passed, one old neutral-culture expectation failed; updated it to en. Zero build warnings.
- English public docs written with preserved Chinese detail; contribution/security/issue and release guidance added. Packaging now includes bilingual docs and checksums and validates delete targets.
- Translation helper initially rejected new keys before its extra-key override; fixed and regenerated all resources.

- Complete gate PASS: 1189/1189, zero skipped, Release zero warnings/errors; TRX artifacts/quality/1ca56b52e5694764ac4d3a95b9d4fc87/tests.trx.
- Full format check found pre-existing whitespace in unrelated files; modified C# files pass focused format verification. PowerShell release script parses without errors.
- Public link check found five old pre-archive planning links; corrected their paths in four historical proposal files. Recognizable credential pattern scan found no matches (not a security audit).
- origin p76871024 resolves through SSH configuration to github.com and matches verified GitHub main 7d75d338; existing origin is the correct publication target.

- fa94d83 committed source. Post-commit packaging PASS: folder 281 files, ZIP approx 67.4 MiB; both resource cultures load from published assembly; embedded version/commit match. ZIP contents/CRC/licenses/bilingual docs/checksum PASS. Runtime resolves 10.0.14 (updated old 10.0.13 notices). Single-file local output withheld from upload.
- Evidence docs/evidence/phase-15/validation.md; final tagged rebuild will have its own digest checked against GitHub after upload. No ScreenshotApp process started or user data modified.

- Release blocker found before creating GitHub Release: single-file publish rewrites source packages.lock.json with implicit ILLink dependency/RID; it was accidentally included in 4b2b487. Subsequent locked restore fails NU1004. v1.3.0 tag already pushed; do not overwrite it. Correct source lock and isolate query/variant locks in publish/. Publish corrected 1.3.1 instead, rerunning full gate and post-package locked restore/hash check. User informed.

- Corrected 1.3.1 full gate PASS: 1189/1189, zero skipped, Release zero warnings/errors; TRX artifacts/quality/b3415effc54f4078b7f1e70fd366e889/tests.trx.
- v1.3.0 remote CI failed as expected; no Release was created. Separate packaging lock paths and source-lock hash guard implemented.

- 1.3.1 post-commit packaging PASS at fe31ce9: both variants generated, source lock unchanged, post-package locked restore PASS; ZIP and English/zh-TW resource/version probes PASS. Historical Chinese-label smoke assumptions documented as unvalidated, no desktop script run.

- 1.3.1 main CI 41404048273: 1184 pass/5 fail. All failures are LF-vs-CRLF in SelectionOverlayTests; resx checkout was converted on Windows due missing .gitattributes. Add text=auto eol=lf per repository policy; do not weaken assertions. Preserve unreleased 1.3.0/1.3.1 tags. Final release 1.3.2; await successful main CI BEFORE final tag. User informed.

- 1.3.2 local full gate PASS: 1189/1189, 0 skipped, Release 0 warnings/errors. Added .gitattributes, retained all assertions/tests.
