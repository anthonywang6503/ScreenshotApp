# Release procedure

Requires Windows x64, PowerShell 7, the pinned .NET SDK, Git and an authenticated GitHub CLI with write access to `anthonywang6503/ScreenshotApp`. A release is an explicit maintainer operation; the CI workflow does not publish automatically.

1. Update the specification/ADR for changed behavior. Update English resources and the complete zh-TW satellite, guides, limitations, notices and changelog. Preserve unrun acceptance status.
2. Increment `Version` in `ScreenshotApp.csproj` and `assemblyIdentity` in `app.manifest` together. Use semantic versioning and date the changelog.
3. Run `pwsh -NoProfile -File tools/Invoke-Quality.ps1`, formatting/static checks appropriate to the change, and documentation link checks. Record actual results in [acceptance](ACCEPTANCE.md).
4. Commit on the feature branch and fast-forward main according to project policy. Create the local annotated tag `v<version>` on that commit. Do not publish from uncommitted source: the embedded informational version includes the build commit.
5. Run `pwsh -NoProfile -File tools/Publish-Release.ps1`. It creates self-contained folder and single-file ZIPs plus `SHA256SUMS.txt` for the public folder ZIP in `publish/`. It must preserve `zh-TW/ScreenshotApp.resources.dll` in the folder build and include it in the single-file bundle. Inspect file/product versions, both resource cultures, licenses and ZIP contents. Packaging is not interactive acceptance.
6. Run guarded interactive smoke on the exact built artifacts only when the desktop is idle. Without version-specific single-file smoke, publish only the folder ZIP and its checksum file. Document all unrun checks.
7. Push main and the intended tag to the verified GitHub remote without force, wait for Windows CI and inspect its actual result. Resolve failures before publishing. Do not push unrelated branches/tags.
8. Create release notes in a UTF-8 file and use `gh release create v<version> --repo anthonywang6503/ScreenshotApp --verify-tag --title "ScreenshotApp v<version>" --notes-file <file> <folder-zip> <checksums>`. Create as a draft first if additional artifact verification is needed. State platform, language/restart behavior, privacy defaults, unsigned status and unrun desktop checks.
9. Verify GitHub visibility, tag commit, downloadable assets, sizes and digest against the local files. Report the release link and remaining acceptance limits.

Use `gh release view` and `gh run view` to inspect external state. Never overwrite an existing release/tag or force-push to repair a mismatch silently. Runtime licenses are collected from the actual resolved packs; no application NuGet dependencies are added for packaging.
