# Architecture decision records

This English index summarizes accepted decisions. Original records preserve their detailed rationale in Traditional Chinese. Revisions explicitly override earlier assumptions; the v1.3 language revision changes Chinese-only resources to English-default English/zh-TW selection with restart.

| ADR | Decision |
| --- | --- |
| [0001](0001-tech-stack-and-project-layout.md) | One .NET 10 WPF executable and one xunit project; direct composition and feature folders; no application NuGet packages |
| [0002](0002-coordinate-system.md) | Physical pixels as truth, conversions through CoordinateMapper |
| [0003](0003-single-instance.md) | Session-local single instance with activation signaling |
| [0004](0004-settings-persistence.md) | Atomic JSON settings, corruption backup, per-field fallback and per-user startup |
| [0005](0005-string-resources.md) | resx, static access and XAML markup extension; v1.3 English neutral/zh-TW satellite, saved selection applied before services after restart |
| [0006](0006-tray-and-winforms-boundary.md) | Windows Forms only for tray/menu/color; WPF dispatcher owns message processing |
| [0007](0007-capture-strategy.md) | Per-monitor GDI desktop snapshots |
| [0008](0008-overlay-placement-and-input.md) | Physical placement and controlled overlay input |
| [0009](0009-app-state-machine.md) | Explicit capture/editor lifecycle state machine |
| [0010](0010-document-model-and-commands.md) | Immutable annotation records, undoable commands and composites |
| [0011](0011-shared-annotation-painter.md) | Shared screen/output annotation painter |
| [0012](0012-canvas-zoom-model.md) | Pixel-correct zoom; cropped fit up to 400% |
| [0013](0013-text-input-strategy.md) | WPF text editing and Traditional Chinese IME support |
| [0014](0014-clipboard-formats-and-retry.md) | DIB/PNG clipboard payloads and bounded retry |
| [0015](0015-file-writing-and-background-encoding.md) | UI composition, frozen bitmaps and background encoding/writing |
| [0016](0016-unexported-changes-protection.md) | Prompt before losing unexported changes, including concurrent edits |
| [0017](0017-release-packaging.md) | Unsigned self-contained folder as primary download; single-file requires version-specific smoke; Apache-2.0 and runtime notices |
| [0018](0018-reproducible-quality-checks.md) | Pinned SDK/locks and common local/Windows CI gate |
| [0019](0019-overlay-magnifier-and-color-pick.md) | Frozen-snapshot magnifier/color picking with guarded input |
| [0020](0020-effect-annotations.md) | Original-sampled mosaic/blur and bounded cache; no secrecy guarantee |
| [0021](0021-ruler-and-eyedropper.md) | Pixel-center ruler/overlay measurement and original-image color picking |
| [0022](0022-capture-history.md) | Default-enabled unencrypted local history with serialized writes, retention and deletion protection |
| [0023](0023-scrolling-capture.md) | Stable exact overlap, targeted scrolling, user-input interruption, limits and cancellation |

See [architecture](../ARCHITECTURE.md), [specification](../ScreenshotApp.Spec.md), [limitations](../KNOWN_LIMITATIONS.md) and [acceptance](../ACCEPTANCE.md). Historical ADRs are design records, not runtime acceptance evidence.
