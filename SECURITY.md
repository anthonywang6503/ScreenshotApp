# Security policy

Please report vulnerabilities privately using [GitHub private vulnerability reporting](https://github.com/anthonywang6503/ScreenshotApp/security/advisories/new) when enabled. If that form is unavailable, open an issue asking the maintainer for a private reporting channel **without including vulnerability details or exploit data**. Do not post private screenshots, local history, logs containing personal paths or credentials in a public issue.

Include the affected version/commit, Windows version, reproduction steps, expected/actual behavior and impact. Use a minimal synthetic image or test data. There is no guaranteed response or patch timeline; fixes are targeted at the latest release and development branch.

ScreenshotApp is offline and runs as the current user. Capture history is enabled by default and stores unencrypted original images and annotations, including content before crop/masking. Disable and clear it where appropriate. Blur/mosaic do not provide secure deletion. Error logs can include file paths and exception details. Read the [privacy and limitations](docs/KNOWN_LIMITATIONS.md) before sharing captures or diagnostics.

Release binaries are currently unsigned. Download only from the project's [GitHub Releases](https://github.com/anthonywang6503/ScreenshotApp/releases) and compare the SHA-256 checksum. A checksum detects corruption or mismatched files; it is not a digital signature.
