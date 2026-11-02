# Changelog

[繁體中文 and detailed historical entries](CHANGELOG_zh_TW.md). Versions use semantic versioning. Unrun interactive acceptance is never inferred from automated tests.

## [Unreleased]

## [1.3.1] - 2026-11-02

### Fixed

- Isolate packaging restore lock files from the source lock file so folder/single-file publishing cannot break subsequent locked restore or Windows CI.
- Supersedes the unpublished v1.3.0 tag; the original tag is retained for traceability.

## [1.3.0] - 2026-11-02 (tag only; not released)

### Added

- English and Traditional Chinese interface selection in Settings. English is the default for fresh installs and settings without a language preference; saved changes take effect after a tray exit and restart.
- Complete English neutral/zh-TW resources and tests for resource parity, format parameters, legacy/invalid settings, culture propagation and settings-window persistence.
- English-first README, user guide, limitations, architecture/specification entry points, quality/acceptance records and third-party notices with preserved Chinese references.
- Contribution and security reporting guidance, issue templates, release procedure and SHA-256 download checksums.

### Changed

- Version/manifest updated to 1.3.0/1.3.0.0. Portable packaging includes both languages and bilingual user documentation. Folder ZIP is the public download; single-file output remains withheld until its required interactive smoke checks pass.
- Archived 1.2 planning/evidence remains available; desktop and MT-12–MT-15 checks are still unverified.

## [1.2.0] - 2026-11-02

- Added selection magnifier and color copying, mosaic/blur, pixel measurement/rulers, editor color picking, local editable history and manual/automatic vertical scrolling capture.
- Protected new edits made during asynchronous export/loading from earlier close/replace requests.
- Added pinned SDK/NuGet locks, shared local/Windows CI quality gate, isolated gate failure probes and long editing-sequence regression tests.
- Added Apache-2.0 licensing and packaged license files. Local integration passed 1176 tests; desktop smoke and new manual acceptance remained unrun. ZIPs were generated locally without a GitHub release.

## [1.1.0] - 2026-10-19

- Added contextual editor menus, duplication, arrow reversal, step renumbering, layer actions, clear annotations and restore full image.
- Cropped fit can enlarge up to 400%. Updated user guides and acceptance evidence. Version-specific release smoke results are recorded in the historical acceptance record.

## [1.0.0] - 2026-10-18

- Initial native Windows tray capture/editor, configurable global hotkey, single instance, physical-pixel region selection, basic annotations/crop, undo/redo, PNG/clipboard export, image loading, settings, per-user startup and bounded error logs.
- Self-contained folder/single-file packaging, automated tests, guarded desktop smoke scripts and initial user/developer documentation.
