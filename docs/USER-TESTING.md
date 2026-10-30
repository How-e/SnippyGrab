# Desktop testing

Use disposable captures containing synthetic text or shapes. Keep personal screenshots, OCR output, profile data and detailed test results outside Git. Record the app build, Windows version, receiver version, monitor scaling and any unavailable conditions with your local results.

## Capture and transfer

1. Focus your destination app. Press Print Screen, select a region and release. The shelf should appear without stealing focus; Ctrl+V should paste the correct image.
2. Cancel with Escape and try reversed selections, mixed-DPI monitors and each capture mode. Toggle cursor inclusion.
3. Drag one image into an unsent draft. Ctrl-select several captures and verify file count, contents and ascending shelf-number order. Check a delayed paste/read.
4. Alt-drag screenshot 1 onto position 3: `[1,2,3]` becomes `[2,3,1]`. Repeat in the opposite direction.

## Shelf and menus

1. Try 1, 5 and 20 captures. Hover and leave repeatedly: the primary card stays under the pointer, selection survives collapse, and the wheel reaches all captures.
2. Test narrow thumbnails and every screen position/orientation. The shelf stays bounded and all actions remain available through More.
3. Use tray → Focus screenshot shelf. Arrows navigate, Space selects, Enter edits, Delete dismisses. Ctrl+C copies, Ctrl+S exports, Ctrl+P pins and Escape clears selection.
4. Open screenshot, tray and pin menus. Check submenus, disabled export-folder state, check marks, keyboard navigation and dismissal with Escape.

## Settings and appearance

1. Open Settings and change values in several categories. Returning to a category preserves the draft. Cancel changes nothing; Apply persists the complete draft after restarting.
2. Use Reset defaults, custom retention, a saved disconnected display and modified hotkeys. Tab/Shift+Tab navigate shortcut fields; Escape disables a shortcut.
3. Enter invalid numeric/color/path values. Apply should show an error and take you to the relevant category without discarding other edits.
4. Try System, Light and Dark. Change Windows app appearance while System is selected and check already-open windows and menus.
5. Check minimum window sizes, Windows text sizes of 100/150/225%, high contrast and reduced motion. Categories, fields, buttons and all sixteen editor tools remain reachable. Verify focus visibility and readable disabled states.

## Editor, history and pins

1. Try every editor tool, reversed drags, contextual color/stroke/text properties, zoom, undo and redo. Text-entry shortcuts should edit text rather than invoke document commands.
2. Apply + copy updates the managed image and clipboard. Export PNG writes a separate file outside the cache. Cancel a picker and verify no success state appears.
3. Closing applies pending edits; Discard changes leaves them unapplied. Confirm that edited images update in the shelf, open History and detached pins.
4. In History, select several captures, copy files, pin, restore to the shelf and page through a large collection. Resize between the preview split and compact list layout.
5. Resize pins and test opacity, always-on-top, click-through and tray → Restore pins. Restart and check saved pin layout. Clear temporary confirmation must preserve pins, active editors and transfers.

## Setup and lifecycle

1. Use a disposable Windows profile or VM for installation/upgrade/uninstall. Verify per-user installation, startup opt-in, data preservation and OCR prerequisites.
2. Close application windows and confirm the tray remains. Exit should coordinate pending editor edits. Check Explorer restart, sleep/resume and display reconnect when available.
3. Check Welcome: shortcut guidance, export folder, startup option and Start snipping. No startup or Windows shortcut setting changes without an explicit choice.

The complete compatibility matrix is in [ACCEPTANCE.md](ACCEPTANCE.md). Automated layout and synthetic clipboard checks establish their stated scope; real receivers, hardware and assistive technology need separate observation. Reopen affected release gates when a change requires renewed acceptance.
# UI Design local alpha acceptance additions — 2026-10-29

These checks supplement the existing screenshot checklist. Q2/Q8/Q19 are reopened; automated fixtures do not establish these desktop observations.

- With WebView2 present, open Tray → UI Design. With the runtime absent on a clean profile, confirm actionable guidance and continued ordinary screenshot capture. No silent runtime installation.
- Import a local project URL and local HTML; choose visible/full/element regions and different viewport sizes. Compare original fonts, wrapping, clipping, stacking, gradients, shadows, images and vector icons; record marked fallback and page/browser versions. Try unavailable pages, oversized DOM and cancellation.
- Select, move, resize, edit text/typography/appearance, insert, group, duplicate, reparent, align, hide/lock and undo/redo through canvas and keyboard/inspector. Verify selection at zoom, DPI/text scales, high contrast and assistive technology.
- Save/reopen, open a shelf design preview, recover an unsaved snapshot, cancel close/Exit/update, simulate unwritable/stale files, and verify no discarded changes. Open the same file twice and confirm a stale save cannot overwrite the newer version.
- Review/exclude/revert changes and check the preview, JSON, brief, original/edited images and region crops agree. Mark private layers, move/delete them, and inspect ZIP contents and overlapping raster assets for private text/pixels. Clipboard contention must retain the brief and offer retry/export.
- Import a scheme-free LAN address such as `10.0.1.11:8085`. Check the HTTP Basic sign-in dialog's origin/challenge, incorrect credentials and cancellation. Check form sign-in and retained source sessions. Design/handoff data must exclude credentials and URL queries.
- Start from the actual workspace **Import page…** button. Verify source initialization completes, Load or Enter navigates, invalid addresses allow retry, and Capture layers returns editable content to the workspace. A separately opened browser-service fixture does not cover this entry path. Include lazy images below the viewport, deferred rendering, direct text in `display: contents`, and font faces inside imported/media stylesheets when checking detection.
- Reload the unpacked Design extension in Chrome and Edge after upgrading, accept its page-capture permission, and register its actual ID. Import a signed-in full page taller than the viewport, a visible area and an offscreen picked element at different display scales. Verify the bottom content, inline statistics, side borders, generated icons, font styles and nested loaded scroll panels against Original. Check source scroll/style restoration, denied/restricted pages, debugger conflicts, unregistered origin, host errors/retry, moving/updating the install, bridge removal, and pending inbox handling with an app already running. Do not include account cookies/storage in evidence.
- Give a handoff ZIP to an independent agent without chat history, implement the requests in the real project, then recapture the route/viewport/theme. Verify source drift/missing selectors and context mismatch are explicit. Check responsive behavior and accessibility separately from pixel similarity.
