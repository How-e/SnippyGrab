# Interactive desktop acceptance

Use synthetic information. Record Windows/app build, GPU/HDR, monitor geometry/scaling, app/receiver versions, timings and observed behavior. A stable tag requires completion of this matrix.

For step-by-step user checks, see [USER-TESTING.md](USER-TESTING.md). Release requirements are maintained in `scripts/release-gates.json`. This matrix defines acceptance; it is not itself a record of passed tests. [Capabilities and evidence](CAPABILITIES.md) reconciles implementation, automated evidence and the current **41 closed / 10 open** gates. Performance changes in `0.1.0-alpha.20261010.1` reopen Q1/Q12/Q20/Q24/Q29/Q30/Q32/Q34/Q35/Q41. Renew real receiver delivery, editor/export/OCR/history/pin interaction, primary receiver use, actual interaction/accessibility, end-to-end timing, prolonged resources and clean-profile package acceptance; Q1 remains open until the required milestone evidence is complete. The owner reported PASS for the full physical monitor/DPI/HDR matrix on acceptance build `0.1.0-alpha.acceptance.20261009.2`; precise per-row configurations were not supplied. That report does not accept the later performance changes.

| Area | Acceptance |
|---|---|
| Capture and paste | Focus a receiving application, Print Screen, select, release; shelf shows without stealing focus; immediately Ctrl+V receives correct pixels. Repeat 20 times. |
| OLE receivers | Single and Ctrl-selected 2–4 captures into desktop chat clients, browsers, code editors, Explorer, and web issue composers. Verify order, image contents and delayed read after 1 minute. Receiver support varies. |
| Hotkeys | Every mode, fallback, changed keys, collision, Windows capture setting on/off, pause/resume; no repeat on hold. |
| Region | Esc cancel, click without drag, backwards selection, cross-monitor spans, negative origins, vertical displays. |
| DPI | 100%, 125%, 150%, 175%, 200%, mixed scaling. Exact physical crop and sensible shelf placement in every corner. |
| Capture modes | Desktop, monitor picker/tray/optional hotkey (full physical bounds, disconnect cancellation), window picker and active window; partly offscreen/maximized windows, cursor on/off, protected/secure desktop. |
| Dock | 1/5/20 captures; bounded footprint and expansion; wheel traverses all; Ctrl-click selections visible; drag threshold avoids editor opening; Alt-drag reorder; keyboard operations. |
| Editor | All tools, reversed drags, zoom, undo/redo branches, save/copy/close/discard; shared PNG/JPEG options from shelf/history/editor, quality and white alpha background, overwrite/cancel; exported redaction pixels opaque; original transfer file unchanged after edit. |
| OCR | Error/stack trace/dialog/log, entire image and selected area. Missing model and VC++ runtime leave capture working. Review text for character errors. |
| Cache | 1h/24h/7d/never, session-only, clear during drag/editor, pins after restart, disabled history, corrupt JSON, abnormal termination between image/metadata writes. |
| Tray | Close UI keeps tray; Exit saves pending editor changes. Explorer restart, startup, sleep/resume, display disconnect/reconnect. |
| Install/remove | Clean user account without .NET; portable, install without elevation, Apps uninstall, startup removed, captures retained intentionally. |
| Performance | Fresh tray working set, 60-second idle CPU, 20 captures, 4K/8K capture-to-availability, editor memory recovery, 200 capture cycles. |
| Accessibility | Keyboard/focus, accessible names/tooltips, high contrast, text scaling, dark/light/system theme. |

`SnippyGrab.exe --self-test C:/absolute/path/checks.json` temporarily shows synthetic windows and moves the pointer on each monitor and sends a synthetic Print Screen key event. Run when the desktop is idle. It uses an isolated cache and does not overwrite the user's clipboard. Not suitable for headless CI. Automated intra-app OLE delivery does not prove compatibility with external receiving applications.
