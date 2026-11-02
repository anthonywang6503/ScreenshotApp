# Third-party notices

[繁體中文](THIRD-PARTY-NOTICES_zh_TW.md)

ScreenshotApp is licensed under the Apache License 2.0; see [LICENSE](LICENSE) (`LICENSE.txt` in downloads). The application has no direct NuGet dependencies. Its source does not include copied ShareX, Greenshot or other third-party project code.

Self-contained Windows x64 distributions include Microsoft .NET Runtime and Windows Desktop Runtime (WPF/Windows Forms) under their distributed license terms. The 1.3.2 package resolves runtime **10.0.14**, confirmed from its runtime configuration. [Publish-Release.ps1](tools/Publish-Release.ps1) copies license files from the actual resolved runtime packs into `licenses/`:

| Component | License files in the download |
| --- | --- |
| Microsoft.NETCore.App | `Microsoft.NETCore.App.LICENSE.txt`, `Microsoft.NETCore.App.THIRD-PARTY-NOTICES.txt` |
| Microsoft.WindowsDesktop.App | `Microsoft.WindowsDesktop.App.LICENSE.txt` |

The .NET third-party notices describe additional bundled components. Use the included files as the authoritative distribution notices.

Development/test-only packages are not included in application downloads: Microsoft.NET.Test.Sdk 17.14.1 (MIT), xunit 2.9.3 (Apache-2.0), and xunit.runner.visualstudio 3.1.4 (Apache-2.0). Exact resolved dependencies and hashes are recorded in the committed lock files.
