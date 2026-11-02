# Contributing

Thank you for helping improve ScreenshotApp. Open an issue for substantial changes before investing in a large implementation. Describe the user-visible problem, Windows version/scaling, app version and reproduction steps. Do not attach private captures, local history, sensitive paths or credentials.

## Development

Use Windows x64, PowerShell 7 and .NET SDK 10.0.403. Clone the repository, run `pwsh -NoProfile -File tools/Invoke-Quality.ps1`, then run the app from `ScreenshotApp/bin/Release/net10.0-windows/`. Exit from the tray before rebuilding a locked executable. See [quality checks](docs/QUALITY.md).

Read the [specification](docs/ScreenshotApp.Spec.md), [architecture](docs/ARCHITECTURE.md), [ADR index](docs/adr/README.md) and [AGENTS.md](AGENTS.md). Keep changes focused. Add a regression test for a bug and verify it fails before fixing. Use focused tests for small changes and the full gate for core changes/releases. Do not run scripts that drive the real desktop while the user is active.

## Conventions

- UI strings belong in `Strings.resx` (English) and `Strings.zh-TW.resx` (Traditional Chinese, Taiwan). Use `Strings.Get`/`Strings.Format` or `{res:Str Key}`. Maintain identical keys/format arguments and avoid hardcoded XAML text.
- Public documentation starts in English and links its `_zh_TW.md` reference. Historical evidence/decisions may retain their original language. Use relative links within the repository.
- Keep physical pixels authoritative; use CoordinateMapper for conversions, immutable annotations and undoable commands for document edits, and the shared painter for screen/export.
- Follow existing code style, UTF-8/LF text and explicit native resource ownership. Comments explain intent directly without referencing external documents or requirement IDs.
- Tests must use temporary data, test registry keys and named objects, preserving the user's settings, history, clipboard and startup registration.

Submit focused changes with a clear explanation and test results. Source contributions are provided under the project's [Apache-2.0 license](LICENSE). Release publishing is handled separately by the maintainer; see [release procedure](docs/RELEASING.md).
