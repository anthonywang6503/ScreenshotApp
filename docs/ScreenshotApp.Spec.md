# ScreenshotApp specification

[Traditional Chinese detailed baseline](ScreenshotApp.Spec_zh_TW.md) · [Architecture](ARCHITECTURE.md) · [User guide](USER_GUIDE.md)

This is the English entry point for the maintained product contract. The preserved detailed baseline contains the original FR-01–FR-43 requirements, AT/MT acceptance matrices, performance targets and phase history. Those requirements continue to apply, except that **FR-44 below supersedes the Chinese-only UI/default and documentation assumptions**. Historical test plans do not establish completed acceptance; see [acceptance](ACCEPTANCE.md).

## Product contract

ScreenshotApp runs locally in the Windows 11 x64 notification area using C#, .NET 10, WPF and Win32. It must not require administrator rights, accounts or network access. One WPF executable and one test project remain sufficient; Windows Forms is limited to tray/color UI. The app has no direct NuGet dependencies.

The core workflow is configurable hotkey → frozen region selection on one monitor → editable annotations/crop → composed clipboard image or PNG. Start capture, open image, settings, history and exit are available from the tray. The default capture shortcut is Ctrl+Shift+2. Only one instance and one editor document run in the same login session. Conflicting/busy operations are guarded; hotkey failures retain the previous registration and remain visible to the user.

Annotations include rectangle, ellipse, line, arrow, text, numbered steps, mosaic, blur and ruler. Selection, movement, resizing, deletion, duplication, layering, renumbering, crop and undo/redo must preserve physical-pixel coordinates. Drag previews commit one command; undo retains up to 100 actions. Text supports Traditional Chinese IME input and explicit newlines. Zoom/pan changes the view without changing exported pixels; cropped fit can enlarge up to 400%.

Screen and output share one annotation painter. Export composes the full original before cropping. PNG and clipboard DIB/PNG carry identical composed pixels. Save/copy failure keeps the document. Open supports PNG/JPEG/BMP, checks content and limits total pixels to 100,000,000. Transparent sources flatten over white. Changes made during background export/loading must survive an earlier request to close/replace the document.

FR-37–FR-43 cover selection magnifier, snapshot color copying, mosaic/blur, pixel measurement/ruler, original-image color picking, disk history and manual/automatic vertical scrolling capture. History defaults on, retains originals/annotations unencrypted and supports retention 1–500 (default 50), reopen/copy/delete/confirmed clear. Disabling does not delete existing entries. Scrolling capture requires stable exact overlap, targeted auto-scroll with input interruption, and limits of 32,768 pixels high and 100,000,000 pixels total. Cancel/failure must preserve the prior editing document.

Preferences persist atomically to local JSON, corrupt files are backed up and individual invalid fields reset independently. Start with Windows is per-user and off by default. GDI/USER ownership is explicit; background bitmap use requires frozen data; encoding/writes occur off the UI thread. Unexported changes are handled before replacement, close, exit or sign-out.

## FR-44 — Interface language and public documentation (1.3.0)

1. Settings offers **English** (`en`) and **繁體中文** (`zh-TW`). English is the default regardless of Windows UI language, including existing settings without `Language`.
2. Language is persisted in the same atomic settings write. Missing values use English without a warning; invalid values reset only Language and are reported alongside other invalid fields.
3. Initialize the selected UI culture before creating services, menus, cached style display names or windows. Set the WPF default language consistently; leave system numeric formatting intact.
4. Save a change with Apply/OK, then exit from the tray and restart to apply it. State this beside the selector. Do not discard or replace active work to switch languages.
5. Use complete English neutral resources and a complete zh-TW satellite. User-facing keys, formatting parameters and XAML resource references must remain valid in both languages. Windows/framework dialogs and unexpected diagnostic details may follow OS language.
6. English is the main public documentation language, with linked Traditional Chinese references. Preserve detailed historical requirements, ADR rationale and acceptance evidence without claiming unrun checks passed.

## Verification and distribution

Require a zero-warning Release build and relevant automated tests, with the complete gate for releases. Verify both languages, legacy/malformed settings, resource parity and saved selection. Use isolated data in tests. Desktop/mixed-DPI/IME/third-party clipboard and scrolling-target behavior needs separate witnessed acceptance.

Provide the source, license, third-party notices, user/developer guides, known limitations and a self-contained win-x64 ZIP. Increment project/manifest versions together, commit, tag `v<version>`, and publish from that commit so embedded product metadata matches. Single-file downloads require version-specific interactive smoke acceptance; otherwise withhold them. Do not add installer, signing, cloud services or automatic updating as part of this release.
