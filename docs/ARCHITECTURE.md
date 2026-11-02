# Architecture

[Traditional Chinese detailed reference](ARCHITECTURE_zh_TW.md) · [Specification](ScreenshotApp.Spec.md)

ScreenshotApp is a Windows tray application built with .NET 10 WPF and Win32. One executable project and one xunit project are composed directly by `AppController`; there is no DI container, plugin framework or network service. Windows Forms is restricted to the tray menu and color dialog.

## Modules

| Directory | Responsibility |
| --- | --- |
| [Platform](../ScreenshotApp/Platform/) | Win32 interop, SafeHandles, GDI capture, displays, DPI, hotkeys, tray, startup, single instance and logs |
| [Capture](../ScreenshotApp/Capture/) | Capture flow, physical coordinates, selection overlays, magnifier/measurement, scrolling sessions/stitching/panel |
| [Editor](../ScreenshotApp/Editor/) | Window, editing session, annotation/handle layers, zoom, unexported-change handling |
| [Editor/Model](../ScreenshotApp/Editor/Model/) | Immutable annotation records and pixel geometry, document and text layout |
| [Editor/Commands](../ScreenshotApp/Editor/Commands/) | Undoable document changes and composite commands |
| [Editor/Rendering](../ScreenshotApp/Editor/Rendering/) | Shared annotation painter and bounded source-effect cache |
| [Export](../ScreenshotApp/Export/) | Full-image composition, clipboard formats, PNG writing and image loading |
| [History](../ScreenshotApp/History/) | Serialized disk writes, retention, original/thumbnail/annotation format and history window |
| [Settings](../ScreenshotApp/Settings/) | Validated preferences, JSON persistence, style/hotkey choices and settings UI |
| [Resources](../ScreenshotApp/Resources/) | English neutral strings, zh-TW satellite, accessors, markup extension, error descriptions and icon |

## Lifecycle and language

`App` uses explicit shutdown so closing a window does not stop the tray app. It acquires the single-instance guard first, initializes English, loads preferences, then initializes the saved UI culture **before** constructing services, static style choices, tray menus or windows. WPF's default `Language` follows that culture for font fallback. The UI culture propagates to new threads; system numeric formatting is retained. English is the neutral resource; `Strings.zh-TW.resx` provides the complete Chinese satellite. Framework Chinese resources use zh-Hant. The selector persists en/zh-TW and explains that a normal tray exit/restart is required; existing windows are not rebuilt while editing.

`AppController` connects hotkey, tray, capture, editor and history services. Loaded settings are repaired field-by-field and persisted when missing/corrupt/invalid, but not overwritten after a read failure. Hotkey registration and Start with Windows synchronization report warnings. A second instance signals the first to activate its editor/settings and exits. Shutdown releases hotkeys, tray and native handles, with bounded waiting for history writes.

The state machine coordinates idle/capture/selection/editing. Busy capture and modal/export operations suppress conflicting entry points. Tray capture/open/exit callbacks are dispatched after the menu closes to avoid capturing or disposing the menu mid-event. Only one editor document is active.

## Physical pixels and capture

Physical pixels are the source of truth. `CoordinateMapper` performs coordinate-space conversions; 100% canvas zoom means one image pixel per physical display pixel. Win32 captures per-monitor snapshots; overlays are positioned with physical screen bounds and Per-Monitor V2 awareness. Region drag is constrained to its starting monitor. Changing displays cancels the capture. The magnifier/color picker/measurement use frozen snapshot pixels, not live desktop data.

Scrolling capture samples a selected target region, accepts stable frames and stitches only uniquely aligned exact overlap. It tracks fixed header/footer bands, ignores backward/ambiguous movement, uses targeted wheel messages for auto mode and stops it when user input is detected. Sessions enforce height/pixel limits and produce one normal editable image only when finished. Target loss, covering or display changes cancel; no global input fallback is used.

## Documents, drawing and exports

Annotations are immutable records. Only `IEditCommand` operations mutate a document via `UndoRedoStack` (100 actions). Dragging updates a preview and commits one command on release. Composite commands group related edits. Crop is metadata over the original; annotation coordinates remain in the original image space. Rulers use pixel-center distance and the same measurement model as the overlay.

`AnnotationPainter` is the single drawing path for screen and output. Mosaic/blur sample the original and render opaque results with a bounded per-original cache (eight entries/64 MiB). `ImageRenderer` composes the full image first, then crops; translating drawing to a crop origin would change antialiased pixels. PNG and clipboard outputs therefore share composed pixels. Clipboard uses DIB and PNG bytes rather than `Clipboard.SetImage`, avoiding retained GDI objects.

Composition runs on the UI thread. Bitmap data passed to workers is frozen first; PNG encoding, disk writes and image decoding can run in the background. Native resources have explicit SafeHandle ownership. Unexported-change tracking identifies the exported history position; a prompt does not authorize discarding edits made while asynchronous export or loading is running.

## Preferences, history and privacy

Settings uses UTF-8 JSON and a same-directory temporary file followed by replacement. Unknown fields are ignored; invalid individual fields reset only themselves; corrupt JSON is backed up. Preferences are stored in `%LocalAppData%\ScreenshotApp\settings.json`, including Language (`en` default, `zh-TW` alternative).

History is enabled by default and serializes originals, thumbnails and annotation/crop metadata to local unencrypted storage. Writes are queued in order with deletion protection and retention cleanup. Unsupported annotation data restores only the original without silently overwriting the entry. Disabling does not delete old entries. No screenshot or log data is uploaded by the application.

See the [detailed Chinese reference](ARCHITECTURE_zh_TW.md) for prior call flows, state diagrams and test mappings. [ADR index](adr/README.md) records design decisions and subsequent revisions. The v1.3 language rules above supersede earlier Chinese-only assumptions.
