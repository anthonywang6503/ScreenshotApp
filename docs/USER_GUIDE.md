# User guide

[繁體中文](USER_GUIDE_zh_TW.md) · [README](../README.md)

Applies to ScreenshotApp 1.3.0. All image processing is local. **Capture history is enabled by default**, including upgrades: it saves unencrypted originals before cropping or masking. Turn it off in Settings if you do not want captures saved; disabling it does not remove existing entries.

## Install, update and choose a language

Extract the release ZIP to a stable location and run `ScreenshotApp.exe`. Keep all files and subfolders. No separate .NET installation or administrator permission is needed. The unsigned application may trigger Windows SmartScreen; review the release source before allowing it. The app starts in the notification area without a main window.

The initial interface is **English**. Double-click the tray icon, choose **Interface language → 繁體中文** or **English**, then click **Apply** or **OK**. Choose **Exit** from the tray and start the app again. Closing the editor or settings alone does not exit the app. Language changes apply after restarting; existing images, annotations and history are preserved. Framework dialogs and unexpected system errors may use the Windows language.

To update, exit the old app and replace its program folder with the new extracted folder. Preferences and history remain in `%LocalAppData%\ScreenshotApp`. If an older settings file has no language preference, the new interface defaults to English.

## Tray menu

**Start capture** starts a normal region capture. **Scrolling capture** starts vertical capture. **Open image** loads a file; **Capture history** reopens stored work. **Settings** changes preferences; **Exit** stops the app and unregisters hotkeys. Double-click the icon to open settings. Starting the app again activates its existing editor or settings instead of opening a second instance.

## Capture a region

Press **Ctrl+Shift+2** (configurable), then drag a rectangle on one monitor. Releasing completes the capture and opens the editor. The desktop is a frozen snapshot; its displayed width and height are physical pixels, independent of Windows scaling. Selection cannot span monitors. Clicking without dragging does not capture. **Esc** or right-click cancels and keeps the previous document. Changing display configuration cancels selection.

Hold **Ctrl** to show the fixed 15×15 pixel magnifier. Release Ctrl, then press **C** to copy the pointer pixel's uppercase RGB hex code without replacing the current editing document. Pixels outside the display use a gray fill in the magnifier.

Press **R** to measure instead of capturing. Drag between pixel centers; the result stays visible after release. The display shows length, horizontal/vertical differences and the inclusive bounding size. For example, (10,20) to (13,24) is 5 px long with a 4×5 px bounding box. Press R again to return to capture; Esc/right-click cancels. These measurements are pixels, not physical units.

## Edit images

Select a tool, then drag or click on the image. Use selection to move or resize one annotation at a time. Annotations are separate from the original until export. Rectangle and ellipse interiors do not intercept selection; click their outlines. Double-click text or a step to edit its content/value.

| Tool | Action |
| --- | --- |
| Select (Alt+V) | Select, move and resize annotations |
| Crop (Alt+C) | Drag a crop; Enter/double-click inside applies; Esc cancels |
| Rectangle / Ellipse (Alt+R / Alt+E) | Draw outlines |
| Line / Arrow (Alt+L / Alt+A) | Drag endpoints; arrows point toward release |
| Text (Alt+T) | Click and type; Enter adds a line, Ctrl+Enter or click elsewhere commits, Esc cancels |
| Step (Alt+N) | Click to place an incrementing numbered marker; values 1–999 |
| Mosaic / Blur (Alt+J / Alt+H) | Drag a region; strength is block size 2–64 px or blur radius 1–32 px |
| Ruler (Alt+Q) | Drag to add an exportable pixel-distance annotation |
| Pick color (Alt+D) | Sample the original image, then return to the previous tool; Esc cancels |

The style bar controls color, line width, font, font size, step value and effect strength. Width and font size use output pixels, not zoomed display units. Color, width and size changes without a selected annotation become defaults. Font choice lasts for the editor session; Microsoft JhengHei is the initial font. Effect strength is not persisted to preferences.

The editor color picker reads the original, not composed annotations or effects. With an annotation selected it changes that annotation's color and can be undone; during text entry it changes text style without committing. With no selection it updates the default color. Effect annotations do not use a color.

Mosaic and blur sample only the original and cover underlying annotations opaquely; annotations above them remain visible. **Neither guarantees secrecy**. Blur may be recognizable or partially recoverable. Originals remain in the document and enabled disk history. Check the exported result before sharing.

## Keyboard, context menus and zoom

| Shortcut | Action |
| --- | --- |
| Ctrl+Z / Ctrl+Y | Undo / redo (up to 100 actions) |
| Delete | Delete the selected annotation |
| Ctrl+D | Duplicate the selected annotation with a small offset |
| Ctrl+] / Ctrl+[ | Move one layer forward / backward |
| Ctrl+Shift+] / Ctrl+Shift+[ | Move to front / back |
| Ctrl+C / Ctrl+S / Ctrl+O | Copy image / save PNG / open image |
| Ctrl+wheel | Zoom around the pointer |
| Ctrl+0 | Fit to window |
| Space+drag / middle-button drag | Pan |

Text-entry controls retain their normal text-editing shortcuts. Right-click an annotation for edit, duplicate, reverse arrow, renumber steps, layering or delete. Right-click empty canvas for undo/redo, export, zoom, restore full image or clear annotations. During crop, the menu offers apply/cancel/restore. Commands are enabled only where applicable.

100% maps one image pixel to one physical display pixel. Full-image fit is capped at 100%; after applying a crop, fit can enlarge the crop up to 400%. Undoing the crop or restoring the full image fits the full image again. Entering/leaving crop mode without applying keeps the current view. Zoom does not change output pixels. The status bar reports image/crop dimensions, pointer coordinates, zoom and operation results.

## Copy, save and open

**Copy** supplies both PNG and bitmap formats to the clipboard. **Save PNG** exports the same composed pixels with annotations and crop. A busy clipboard is retried briefly; if copying or saving fails, the document stays open. PNG files flatten annotations; use history to reopen editable captures.

Open PNG, JPEG or BMP via the tray or Ctrl+O. Format is checked by content. Images are limited to 100,000,000 pixels. Transparent pixels are composed over white; EXIF orientation and ICC color conversions are not applied. Opening a file does not overwrite it; Save PNG proposes a new filename. Editing during background loading cancels replacement if new unexported changes appeared.

## Scrolling capture

Choose **Scrolling capture** from the tray or configure its separate optional hotkey. Select a region on one monitor, excluding scrollbars, animation and floating UI where possible. Scroll down slowly inside the blue frame, leaving some overlap, and wait for the preview to update.

Click **Auto scroll** to try targeted scrolling. Release all keys before starting and avoid input while it runs. Moving the mouse or pressing a key stops automatic mode and keeps the captured content; manual scrolling can continue. Unsupported applications may ignore scrolling messages; use manual mode. There is no fallback to global input or moving the pointer.

Click **Finish** to edit the stitched result (or initial frame if nothing scrolled). **Cancel**, closing the panel, or Esc while it has focus discards the scrolling session and restores previous windows. Finishing adds the result to history if enabled; cancel/failure does not.

After successfully capturing new content, three unchanged observations can finish automatic capture. No initial movement prompts you to use manual mode or finish. Unstable content, ambiguous overlap or backward scrolling stops automatic mode without replacing already stitched content. Scroll back to reliable overlap before continuing. The limit is 32,768 pixels high or 100,000,000 pixels total. At the limit, click Finish to retain the result. Target-window closure, covering the region center or display changes cancel capture. The control panel may briefly hide during sampling. Cross-application compatibility remains unverified; see [acceptance](ACCEPTANCE.md).

## Capture history

Open history from the tray or editor. Reopen to continue editing, copy the composed result, delete selected entries, refresh, or confirm **Clear all**. The default retention is 50 entries, configurable from 1 to 500. Thumbnails show original captures; reopen/copy applies saved annotations and crop. History does not preserve the undo stack, pending drags or uncommitted text. Opened external files are not automatically added. Reopening malformed or unsupported annotation data restores only the original and warns without automatically overwriting the entry.

Disabling history stops automatic writes; existing entries remain until deleted. Clear all deletes originals and annotations in the history folder but keeps the document currently being edited. It is not secure erasure. Closing waits at most three seconds for pending history writes; forced termination or timeout may lose the latest updates.

## Unexported changes

Before replacing/closing a modified document or exiting, choose Save PNG and continue, Copy and continue, Discard changes, or Cancel. Unmodified images close without prompting. If you keep editing during background export, new changes are retained and the original close/replace action stops. On sign-out/shutdown, handle the prompt and request sign-out again; forcing Windows to continue can lose changes.

## Settings, local data and removal

Settings includes language, normal/scrolling hotkeys, Start with Windows (off by default), style defaults, the last save folder, history retention and clearing. Hotkeys require Ctrl, Alt or Win and cannot use Windows-reserved capture combinations. Occupied hotkeys retain the previous working registration. The two capture hotkeys must differ. Corrupt settings are backed up; invalid individual fields fall back without resetting other preferences.

Local data is under `%LocalAppData%\ScreenshotApp`: `settings.json`, optional `settings.corrupt.json`, error logs and `history/`. Error logs contain messages/stack traces and can include file paths. Start with Windows uses the current user's `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` entry. No data is uploaded.

To remove the app, disable Start with Windows, exit from the tray, delete the program folder and optionally delete its local data folder. Deleting the app folder alone does not remove saved captures.

## Troubleshooting

If no window appears, check hidden tray icons. If the hotkey is occupied, open Settings and change it. If capture is black, check protected-content or GPU restrictions. If copying fails, retry after another application releases the clipboard. For unexpected errors, review the indicated log and remove personal data before attaching it to a report. See [known limitations](KNOWN_LIMITATIONS.md) and [security reporting](../SECURITY.md).
