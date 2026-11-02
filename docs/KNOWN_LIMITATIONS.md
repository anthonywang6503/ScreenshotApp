# Known limitations

[繁體中文](KNOWN_LIMITATIONS_zh_TW.md) · [User guide](USER_GUIDE.md)

Applies to 1.3.1. Automated WPF/model tests are not a substitute for interactive desktop acceptance. Current evidence and unrun checks are listed in [acceptance](ACCEPTANCE.md); preserved detailed findings remain available in [Traditional Chinese](ACCEPTANCE_zh_TW.md).

## Distribution and languages

- Windows x64 only; Windows 11 is the target. Windows 10 is best-effort and unverified; ARM64 and 32-bit distributions are not provided.
- Unsigned portable binaries may be blocked by SmartScreen or organizational policy. No installer, automatic updater, Store package or elevated execution.
- Folder distribution is the published download. Single-file packaging extracts native WPF libraries to temporary storage and is withheld until its version passes interactive smoke checks. Clean Windows without an installed .NET runtime remains unverified.
- English (default) and Traditional Chinese are supported. Save a language selection, exit from the tray and restart to apply it. Windows/framework dialogs and unexpected exception text may follow the operating-system language. Other languages and live switching are not supported.
- Historical interactive smoke scripts assume Traditional Chinese labels and need adaptation for English-default builds. They have not been validated for this bilingual release; automated language tests and packaging checks do not replace those desktop checks.

## Capture and selection

- No mouse cursor in output; only rectangular regions within a single monitor, without automatic window/control detection.
- Protected/DRM content, the UAC/secure desktop and some fullscreen GPU/hardware-overlay surfaces cannot be captured correctly.
- The desktop is frozen at capture time. Animations or clocks do not update while selecting. Own windows are hidden first, but system dialogs may still be captured.
- Display resolution/scaling/layout changes or monitor disconnects cancel selection. Clicking without dragging leaves selection active.
- The crosshair is drawn by the app and does not use Windows accessibility pointer colors/styles. Pointer-size behavior and mixed-DPI physical interactions require desktop verification.
- Magnifier and color sampling read only the frozen snapshot; magnification is fixed and there is no arrow-key pixel stepping. Outside-display cells are gray.

## Editing and output

- One document and one selected annotation at a time. No multi-selection or public editable project-file format. Flattened PNG/clipboard output cannot restore separate annotations; editable history can.
- Undo history is capped at 100 actions. Rectangle/ellipse interiors are transparent to hit testing. Text wraps only at explicit newlines and is clipped at the crop boundary.
- Basic font choices only: Microsoft JhengHei, PMingLiU, DFKai-SB, Segoe UI and Consolas. Font choice resets when the editor closes; color, line width and font size persist.
- Downscaled thin lines/text can look faint. Cropped fit caps enlargement at 400%; undoing crop refits the full image rather than restoring the earlier view. Neither changes exported pixels.
- Duplicates offset 10 px down/right or reverse at an edge; oversized annotations may still extend outside the crop.
- Clipboard contention causes brief retries and can temporarily block the UI. Failed copy/save keeps the document. Editing during background export/loading can stop the requested close or replacement to protect new changes.
- Sign-out/shutdown may be deferred to handle unexported changes. Forcing Windows to proceed can lose them. Unmodified captures close without a prompt.

## Mosaic, blur and rulers

- Blur may be recognizable or partially recoverable; weak mosaic can retain details. Neither is secure deletion. Originals remain in the document and enabled disk history.
- Effects sample the original, cover lower annotations opaquely and leave upper annotations visible. Moving an effect resamples its new position.
- Large effects recompute on the UI thread and can pause interaction. Earlier 4K measurements were about 51 ms for 12 px mosaic and 424 ms for radius-6 blur (median of five recomputations, not end-to-end interaction). See [raw evidence](evidence/phase-10/performance.json).
- Effect caches are bounded to eight entries/64 MiB per live original. Results above 64 MiB are not cached; large images still need temporary memory.
- Rulers measure pixel-center distance, not centimeters. Inclusive bounding dimensions can differ by a pixel. Labels use black text on white, may shrink on tiny images, and can be clipped by a crop without repositioning.
- Editor color picking reads original pixels, not rendered annotations/effects. Hidden RGB in completely transparent source pixels depends on the source format.

## Image loading

- PNG/JPEG/BMP only, maximum 100,000,000 pixels. GIF, TIFF, WebP and HEIC are unsupported.
- Transparency is composed over white; EXIF orientation and ICC conversion are not applied. Some truncated image data can be decoded with missing regions filled black rather than rejected by Windows.
- Every export composes the full original before cropping to preserve identical pixels. A small crop of a huge image still requires full-image processing and temporary memory (about 400 MB for 100 million 32-bit pixels).
- Save proposes a new filename; a source is overwritten only if explicitly selected and confirmed in the save dialog.

## Local history

- Enabled by default, including older settings without history fields. Saves unencrypted originals and annotations, including content before cropping/masking. Turning it off retains existing entries. Clear all is not secure erasure and cannot remove OS/backup copies.
- Thumbnails show originals. Retention counts entries (1–500, default 50), without a disk-space quota. Large captures can use substantial storage.
- No undo stacks, drag previews or uncommitted text. External files and captures made while history is disabled are not backfilled.
- History JSON is an internal format. Unsupported/corrupt annotation data restores the original with a warning and is not automatically overwritten. A corrupt original cannot be reopened. Links/reparse points are rejected.
- Exit waits up to three seconds; forced termination or timeout can lose recent writes. Incomplete entries are cleaned during enabled startup maintenance. Disabled history performs no maintenance/eviction.
- Long-running tray use, UI interactions, disk consumption and physical restart acceptance remain distinct from isolated automated tests.

## Scrolling capture

- Vertical downward stitching on one monitor only. Backward movement is not appended. Ambiguous/repetitive/no-overlap content is not guessed; previous content is kept.
- Requires pixel-identical overlap after at least three stable frames. Smooth scrolling must settle. Animation, video, blinking cursors, changing scrollbars, floating elements or headers can prevent stitching. Large fixed bands, uniform regions and repeated content can be ambiguous.
- Auto mode uses targeted wheel messages, one notch at a time. Applications can ignore them, especially elevated or custom-input windows. No fallback to global input or pointer movement. Compatibility with Edge, Chrome, Word, VS Code and Explorer remains unverified.
- Input polling stops automatic mode after mouse/key activity, with finite detection delay. Release keys and start via mouse. Esc is not globally intercepted; it cancels only when the panel has focus. Stopping auto mode keeps the session open for manual scrolling or Finish.
- Maximum height 32,768 pixels and total 100,000,000 pixels. Only complete rows within limits are appended. Memory exhaustion cancels and restores the previous document; capture large content in sections.
- The panel may briefly hide for sampling when outside space is insufficient. Closing/covering the target region center or changing displays cancels capture. DRM/secure-desktop/GPU restrictions also apply.
- Finished results follow history settings; canceled/failed sessions are not saved. Automated stitching tests do not establish cross-application or desktop pixel accuracy.

## Not provided

Video/GIF recording, OCR, QR codes, solid redaction, pinned images, cross-monitor selection, automatic window capture, layer groups, collaboration, cloud accounts, plugins and automatic updates.
