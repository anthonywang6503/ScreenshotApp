# Release procedure

Requires Windows x64, PowerShell 7, the pinned .NET SDK, Git and an authenticated GitHub CLI with write access to `anthonywang6503/ScreenshotApp`. A release is an explicit maintainer operation; the CI workflow does not publish automatically.

1. Update the specification/ADR for changed behavior. Update English resources and the complete zh-TW satellite, guides, limitations, notices and changelog. Preserve unrun acceptance status.
2. Increment `Version` in `ScreenshotApp.csproj` and `assemblyIdentity` in `app.manifest` together. Use semantic versioning and date the changelog.
3. Run `pwsh -NoProfile -File tools/Invoke-Quality.ps1`, formatting/static checks appropriate to the change, and documentation link checks. Record actual results in [acceptance](ACCEPTANCE.md).
4. Commit on the feature branch and fast-forward main according to project policy. Push the candidate main commit and inspect Windows CI before creating the final tag. Do not publish from uncommitted source: the embedded informational version includes the build commit.
5. Run `pwsh -NoProfile -File tools/Publish-Release.ps1`. It creates self-contained folder and single-file ZIPs plus `SHA256SUMS.txt` for the public folder ZIP in `publish/`. It must preserve `zh-TW/ScreenshotApp.resources.dll` in the folder build and include it in the single-file bundle. Inspect file/product versions, both resource cultures, licenses and ZIP contents. Packaging is not interactive acceptance.
6. Run guarded interactive smoke on the exact built artifacts only when the desktop is idle. Adapt the historical scripts' Chinese title/text assumptions before testing an English-default build. Without version-specific single-file smoke, publish only the folder ZIP and its checksum file. Document all unrun checks.
7. After successful main CI and package checks, create annotated tag `v<version>` at the verified commit and push only that tag to the verified GitHub remote without force. Inspect the tag's Windows CI result too. Resolve failures before publishing. Do not push unrelated branches/tags.
8. Create release notes in a UTF-8 file and use `gh release create v<version> --repo anthonywang6503/ScreenshotApp --verify-tag --title "ScreenshotApp v<version>" --notes-file <file> <folder-zip> <checksums>`. Create as a draft first if additional artifact verification is needed. State platform, language/restart behavior, privacy defaults, unsigned status and unrun desktop checks.
9. Verify GitHub visibility, tag commit, downloadable assets, sizes and digest against the local files. Report the release link and remaining acceptance limits.

Use `gh release view` and `gh run view` to inspect external state. Never overwrite an existing release/tag or force-push to repair a mismatch silently. Runtime licenses are collected from the actual resolved packs; no application NuGet dependencies are added for packaging.

Packaging restore uses separate NuGet lock files under publish/ for the runtime query and each variant. Never commit these generated locks over ScreenshotApp/packages.lock.json. After packaging, confirm the source lock is unchanged and rerun locked restore. The v1.3.0 tag is retained without a Release because its packaging-generated source lock broke locked restore; v1.3.1 corrects this.

The v1.3.1 tag is also retained without a Release: Windows checkout converted multiline resources to CRLF and five overlay assertions failed. Version 1.3.2 enforces LF with .gitattributes. For future releases, push the candidate main commit and wait for successful Windows CI before creating/pushing the final version tag; never rewrite a published tag.
