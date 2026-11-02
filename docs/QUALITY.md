# Build and quality checks

[繁體中文](QUALITY_zh_TW.md) · [Contributing](../CONTRIBUTING.md)

Use Windows x64, PowerShell 7, Git and .NET SDK **10.0.403**. [global.json](../global.json) disables SDK roll-forward and previews. The application's and test project's NuGet lock files pin resolved versions and hashes. The application has no direct NuGet dependencies.

```powershell
pwsh -NoProfile -File tools/Invoke-Quality.ps1
```

The shared local/Windows CI gate performs locked restore, Release build with warnings treated as errors, and all tests without rebuilding. Each run writes a fresh `artifacts/quality/<GUID>/tests.trx` and requires a nonzero test count with every test passing. Missing/skipped tests or native-command failures fail the gate. Output is ignored by Git. Do not build over an application running from `bin/Release`; exit it from the tray first.

For small changes, run relevant tests and necessary static checks; use the complete gate for shared core changes, changes spanning modules or releases. WPF tests run on dedicated STA threads. Tests use temporary storage and test-specific registry/named objects; preserve the user's clipboard, preferences and startup registration. The gate does not run interactive smoke tests, publish binaries or push to GitHub.

## Verify the gate itself

After changing the quality script, run `pwsh -NoProfile -File tools/Test-Quality.ps1`. It injects four failures into isolated source copies: build warning, failing test, skipped test and dependency/lock mismatch. Every case must be rejected. It does not rewrite the original source. Evidence goes under `artifacts/quality-self-test/`.

## Windows CI

[quality.yml](../.github/workflows/quality.yml) runs the same gate on `windows-2025` for pushes, pull requests and manual dispatch. It installs the SDK selected by global.json, has a 15-minute timeout, read-only contents permission, no persisted checkout credentials, and uploads available TRX for 14 days even on failure. Actions are pinned to commit hashes. CI does not publish a release. A runner's WPF/Win32 behavior must be checked from its actual run; local success is not proof of remote success.

## Localization checks

The resource tests require every referenced key to exist, every key to be used, nonempty values, no hardcoded XAML text, identical English/zh-TW key sets and matching composite-format arguments. Language tests exercise migration from old settings, invalid-field fallback, preference round-trips, culture propagation and both settings-window languages. Language changes are persisted but applied after a normal restart.

## Toolchain updates

Update global.json deliberately, install that SDK and rerun the gate. For dependency changes, update the project, run `dotnet restore ScreenshotApp.sln --use-lock-file --force-evaluate`, review and commit the lock diff. Do not hand-edit hashes. Keep text UTF-8 with LF. Formatting checks can use `dotnet format ScreenshotApp.sln --verify-no-changes --no-restore`.

Interactive scripts in [tools/smoke/](../tools/smoke/) drive the real desktop. Run only when the user is idle, with the correct `-ExePath`, input interruption guards and clipboard restoration. Record unrun desktop checks as **NOT_RUN** in [acceptance](ACCEPTANCE.md).
