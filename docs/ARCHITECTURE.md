# Architecture and interaction contract

Decision: .NET 10 LTS, WPF, Windows 10 22H2 / Windows 11, x64. WPF provides native OLE file drop, per-monitor DPI, transparent small windows, system clipboard, and no browser runtime. Win32 GDI capture uses physical desktop pixels. A per-monitor-v2 manifest prevents DPI virtualization. No screen polling.

Core owns versioned settings, history metadata, cache retention/leases, coordinate geometry and editor command history. App owns capture, monitor/hotkey integration, clipboard, OLE, tray/startup, OCR and WPF views. Services have explicit boundaries; orchestration lives in AppController, not a window.

## Dock
One primary image with two offset previews. Hover expands a bounded vertical/horizontal strip (default three items); wheel changes the primary index. A small count button opens history. Ctrl-click toggles multiple selections; dragging exports all selected PNG files. Ordinary click opens the editor after the drag threshold has not been crossed. Alt-drag inside the shelf reorders. Unselected items never grow the shelf beyond its configured bound. Capture shows without activation. Hover controls copy/edit/pin/save/OCR/dismiss. Keyboard commands activate only when the user deliberately focuses the dock.

## Capture and coordinates
RegisterHotKey with MOD_NOREPEAT; default PrintScreen region, Ctrl+PrintScreen desktop, Alt+PrintScreen active window, Ctrl+Alt+PrintScreen window picker; Ctrl+Shift+S fallback. Never change Windows keyboard settings silently. Report failed registrations and the Windows PrintScreen setting; offer settings guidance. Region overlay freezes the physical virtual desktop once, draws selection in physical-pixel space, then converts only window UI placement to device-independent units. Negative desktop origins are supported. Window picking hides the overlay before identifying/capturing the target. Capture restores foreground application before clipboard and dock delivery.

## File lifecycle
LocalAppData/SnippyGrab/cache, UUID-based PNG names, atomic writes, versioned JSON metadata. Full images are loaded only for copy/edit/OCR; dock decodes small thumbnails. Pin state persists independently of optional history. Saving exports a copy and never changes the managed cache path. Editors and drags acquire leases. Completed drags and clipboard-file copies protect files for at least 24 hours to allow asynchronous receivers. Startup and coarse cleanup remove only recognized owned files; unknown files, reparse points, pins and leases are protected. Session-only mode removes unpinned files on a clean exit; crashes are bounded by retention on restart. Dismiss hides an item without deleting an active transfer. Pinned images are never expired.

## Local OCR
Tesseract wrapper 5.2.0 with native engine 5.0.0 and Leptonica 1.82.0 (exact inventory in DEPENDENCY-REVIEW.md), lazy initialized for each OCR job, bundled hash-verified English LSTM model. Production OCR decodes admitted images with Windows WIC and passes raw RGBA pixels to native OCR; compressed native image readers are bypassed. Model provisioning requires network only at build time. Portable OCR requires Microsoft VC++ 2015–2022 x64 runtime; failures leave capture working. No cloud API. OCR text is never logged or persisted.

## Failure boundaries
Clipboard uses bounded asynchronous retries. Capture/save/OCR/cache failures show concise status without terminating the app. A per-user mutex prevents duplicate hotkey/tray ownership. No IPC server. Timers run only for coarse cleanup/dock expiry or pending hide, not continual capture/provider polling.

## Release
Self-contained portable executable plus OCR resources; per-user executable setup plus script installer, no elevation. Stable packaging enforces the recorded P0/P1 queue gates; optional external Authenticode stays disabled by default. GitHub tag workflow builds/tests, bundles license notices and generates SHA-256. Native OCR sidecars remain beside the executable. Unsigned alpha until external desktop acceptance passes; no stable claim from unit tests alone.
