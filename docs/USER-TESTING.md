# Desktop testing requested from you

Start with ****, about 20–30 minutes. These validate the actual AI workflow that automated checks cannot prove. Remaining tests depend on available hardware/apps and can be scheduled later. Mark unavailable environments **NOT AVAILABLE**; you do not need to acquire hardware or install every listed app.

Use the current **0.1.0 alpha** and record whether it is the portable or installed build. Capture only synthetic content: a Notepad window with `CAPTURE A`, `CAPTURE B`, `CAPTURE C` and `error CS1002: ; expected`, for example. Test attachment/paste in an unsent draft; there is no need to send a message or publish an issue. Keep personal screenshots, OCR output and private logs out of the repository.

## Recorded user results — 2026-10-06

| Test | Reported result | Findings | Queue follow-up |
|---|---|---|---|
| Dock first-hover /  /  | FAIL | With multiple captures, first hover causes visible glitching/movement before settling. When expanded, moving to the top image makes the dock lose hover and collapse, preventing access. | //: first-hover stability and reachable expansion; highest P1 work. |
|  | PASS — reported cases | Capture preserved Codex composer focus; Ctrl+V uploaded the correct latest image. Esc and click without selection created no capture and left no dimming. | //: retain success; exact repeat count/timing and other capture cases remain unreported. |
|  | PARTIAL — correct single drag; order observed | One dragged image was correct despite multiple captures. Selecting 3, 2, 1 displayed 1, 2, 3 in Codex, matching PiP order rather than selection-click order. Latest capture is 1; previous captures move down. No explicit exact-count/nonadjacent/delayed-read result supplied. | : define ordering contract and verify payload versus receiver order; / remain open for remaining cases. |
|  | FAIL / PARTIAL | Without selection, pointer leave collapses as intended. With selections, leaving keeps up to three captures open; re-hover instantly collapses without a click, preserving selections. Alt-reorder works but moving 1 to 2 requires dropping on 3; upward moves accept the adjacent item. Delete did not work on a selected capture; Enter did not work on hover. Dismiss works; clicking a capture opens the editor. | : collapse/hover/reorder; /: explicit keyboard focus/actions; : retest after fixes. Keyboard focus during failed shortcuts was not confirmed. |
|  | PARTIAL — editor reported successful; Save unclear | Editor behavior was reported working well. Save destination could not be verified: no images found in the configured directory. Save As opened the correct directory. | : distinguish apply/copy from permanent export and show destination. Current source labels apply/copy as Save. // retain detailed/unreported and fault/large-image checks. |
|  | PASS — reported behavior | OCR was reported working as intended; no errors reported. Separate full-image/area results, input cases and timing were not supplied. | /: preserve successful observation; fault/cancellation/package and unreported cases remain open. |
|  | PARTIAL — prerequisite confirmed | Windows “Use the Print Screen key to open screen capture” had to be off to prevent Snipping Tool taking control of SnippyGrab. Other modes/fallback/pause/repeat cases were not reported. | : actionable setup/help and fallback; : remaining capture-mode checks. |

Next targeted retest order, after corresponding fixes and agent verification: dock first-hover/top-card reachability and selection leave/re-enter; adjacent Alt-reorder both ways; dock focus/Delete/Enter; Save/Save As destination; selected transfer order after reordering. Check Codex focus/latest paste again after dock changes.  have no separate completed report; the initial dock performance observation is relevant to .

Overlay follow-up: user reported that the corrected overlay now covers the monitors properly after the overlay-bounds fix (`c4b78ab`). This establishes reported coverage only; bottom-edge/cross-monitor crop accuracy, Esc and paste were not separately reported. Exact running build/version was not supplied. Keep broader / and hardware gates open.

## First batch: the primary workflow

### P0 implementation retest — /

After installing the updated P0 build, use disposable synthetic captures for : edit then close and paste, Copy then immediately close, Exit with two dirty editors, and Discard an unsaved edit. Confirm applied edits survive restart in Recent captures. Record the build and any premature close, wrong clipboard content or lost edit. Failure messages should keep the editor open and explain retry/Discard.

History recovery is tested automatically in isolated directories. Do not corrupt your real cache. If recovery is already offered naturally, Recent captures explains that unknown old images are pinned and cleanup is disabled. Cancel confirmation first, restart and verify new pins remain. Confirm only after reviewing the explanation; unknown images should remain pinned afterward. Recovery UI interaction remains unverified if the banner is never encountered. Do not run the pointer-moving self-test during normal desktop use.

### Print Screen → immediate Codex paste

1. Focus an unsent Codex composer with synthetic content visible nearby.
2. Press Print Screen, select a region and release. Immediately press Ctrl+V without clicking the dock.
3. Confirm Codex receives the correct image, the dock appears quietly, and keyboard focus remains usable in Codex.
4. Repeat with different regions, including several rapid captures, up to 20 times. Check that the pasted capture is always the latest one.
5. Try Esc cancellation and a click without a meaningful selection. Neither should add an unwanted capture or leave the desktop dimmed.

Report incorrect/stale images, duplicate captures, visible delay, focus loss or any need to click Codex again. Approximate timing is useful; you do not need specialized measurement tools. Supports ///.

### Single and multiple screenshots dragged into Codex

1. Capture labeled A, B, C and D regions.
2. Drag one thumbnail into the Codex composer. Verify one correct image arrives, rather than a text path or internal object, and the editor does not open accidentally.
3. Ctrl-click 2–4 captures, including a nonadjacent pair; drag one of the selected images into the composer.
4. Verify exactly the selected images arrive, without duplicates or omitted files. Record their displayed order.
5. If possible, leave the draft for a minute and confirm the attachments remain readable. Cancel/discard the draft when done.

If a browser/editor rejects the drop, report what happened and its version; receiver support must be established per application. Supports //.

### Compact shelf, scrolling and selection

1. With 10–20 disposable captures, move away from the dock. It should occupy a small footprint rather than cover the desktop.
2. Hover, wheel through captures, Ctrl-click several, and move away again. Check visible selection feedback and sensible expansion/collapse.
3. Alt-drag one thumbnail onto another to reorder; verify scrolling and later dragging use the expected order.
4. Select a capture other than the main thumbnail. With focus on the dock, test Ctrl+C and paste into a suitable draft; then test Delete on disposable captures. Check that these actions affect the selected items. This is a known code-review risk, so report mismatches.
5. Try Enter to edit, Ctrl+S to export, and Escape to close/deselect where supported. Check hover buttons/tooltips and accidental clicks while beginning a drag.
6. Change thumbnail size, orientation, opacity, topmost, auto-collapse and auto-hide in Settings. Check that the dock stays on-screen and usable.

For the reported regressions, also check first hover with 3/5/20 captures, move from the collapsed dock to the top expanded card, leave/re-enter with selections, and Alt-move adjacent items both upward and downward. Confirm keyboard focus is visibly on the dock before testing Delete/Enter; hovering alone may leave keys with the previous application.

Dock reliability retest: the primary card should stay at the chosen corner while other cards grow inward. Move through padding/gaps and reach every displayed card/control; leaving should collapse after a short delay, with the same selection restored on re-entry. Try cold and repeated hover with 1/3/5/20 captures, scrolling, adjacent/nonadjacent Alt-moves in both directions, starting a drag then canceling, and releasing a simple press outside its card. Repeat your normal Codex capture/paste and selected-file drop checks.

Keyboard implementation retest: click a numbered badge (it should focus without editing), or select Focus screenshot shelf (keyboard) from the tray. Check the visible Focus/Selected text. Navigate with arrows/Home/End and toggle Space on a nonprimary capture, then Ctrl+C and Delete on disposable content. Select two nonadjacent captures, scroll to focus an unselected third capture, and verify Copy/Delete still use exactly the selected pair while Enter/Ctrl+S/Ctrl+P use the focused third capture. Tab/Shift+Tab should reach card controls; Enter/Space on a button should activate that button. Ctrl+H/Ctrl+, should open history/settings. Escape should clear selection and collapse. Confirm a fresh capture still preserves composer focus. Actual native focus, clipboard output and accessibility acceptance remain pending until this retest.

Report screen coverage, hidden selections, premature hiding, controls that cannot be reached and any action using the wrong capture. Supports ///////; transfer ordering is .

### Editor and updated screenshot

1. Click a synthetic capture. Add an arrow, rectangle, ellipse, line, freehand stroke, text, highlighter and numbered markers. Try crop, zoom and undo/redo, including undo followed by a different edit.
2. On synthetic text, separately try blur, pixelate and solid redaction. Confirm the exported redaction covers the text completely. Blur/pixelation should not be treated as guaranteed secrecy.
3. Use Copy; paste into Codex and verify edits. Use Save and Save As; note whether their behavior matches their labels and whether the exported PNG opens correctly.
4. Edit again and close the editor. Paste and inspect the dock thumbnail; they should show the updated image. Test Discard on another capture.
5. Reduce the editor width and increase text size if convenient. All required actions should remain reachable.

There is no need to create disk-full/permission failures. Do not rely on unverified close/Exit handling for important edits. Supports //.

### Local OCR

1. Capture synthetic compiler/terminal text. Use the dock OCR action and paste into Notepad or an unsent Codex draft.
2. Open the editor and use `OcrArea` to select only part of the text; verify the copied output is limited to that area.
3. Try small text and a multiline synthetic stack trace. Record character errors and rough completion time.
4. If OCR fails, report the message and whether region capture still works. You do not need to remove DLLs/models or change runtime installations.

OCR is local; English is the current bundled language. Review output before relying on it. Supports /.

## Additional acceptance when convenient

### Hotkeys and Windows interception

Test region, desktop, active window and window picker shortcuts from the README; verify modes and Esc behavior. Test a configurable fallback, pause/resume and whether holding a key produces duplicate captures. If Print Screen opens Windows capture instead, check that SnippyGrab offers useful guidance/fallback.

If comfortable, test Windows **Settings → Accessibility → Keyboard → Use the Print Screen key to open screen capture** both on and off, restarting SnippyGrab as instructed. Record and restore your preferred setting afterward. Do not edit the registry or terminate other hotkey applications just for this test. Supports /.

The 2026-10-06 user test confirmed this setting needed to be **off** for SnippyGrab to receive Print Screen on the tested system. If you prefer to keep Windows interception enabled, use a configurable SnippyGrab fallback shortcut; remaining fallback acceptance is tracked in .

### Your monitor/DPI layout

Record monitor resolutions, scaling percentages, arrangement and HDR status. On each available monitor, capture a clearly outlined rectangle, including near screen edges. Compare the saved dimensions/pixels with the intended physical region. Try a selection spanning monitors, reversed drag and dock placement in all four currently supported corners.

The remaining coverage is 125%, 175%, 200%, negative origins, mixed scaling, vertical monitors and HDR. Test only available layouts. If changing scaling/arrangement, record your original configuration and restore it. Check cursor inclusion on/off; report unexpected cursor position or HDR color differences. GDI output is currently SDR. Supports //.

### Other applications you actually use

For each available receiver, try immediate image paste, one-file drag and 2–4-file drag. Record support separately; Windows does not provide universal multi-image bitmap paste.

| Receiver/version | Image Ctrl+V | One-file drag | Multiple-file drag | Notes |
|---|---|---|---|---|
| Codex | | | | |
| ChatGPT desktop/browser | | | | |
| VS Code | | | | |
| File Explorer (use a disposable folder; image paste may be unsupported) | | | | |
| GitHub issue composer (do not submit) | | | | |
| Slack/Discord or another receiver | | | | |
| Your IDE/terminal, where images are supported | | | | |

Unsupported image paste in a plain text terminal is not automatically an app defect; test OCR text paste there instead. Supports .

### Tray, history, pinning and OS lifecycle

1. Close editor/settings/history windows and confirm the tray app still captures. Pause/resume hotkeys through the tray.
2. Pin a disposable capture and detach it. Try resize, opacity, topmost and click-through; use tray **Restore pins** to regain interaction. Edit and inspect the detached image for staleness.
3. Clear temporary captures via the tray. Pins should survive. Restart normally and verify pin records remain available in history/dock. Do not use private captures for this test.
4. Dismiss a capture and restore it through Recent captures. For an expired capture, use a short configured shelf lifetime and then restore; report if it does not reappear. Open history, capture again and check whether the open list updates.
5. Enable login startup only if desired. At your next normal login, check silent tray startup and hotkeys. Disable afterward if that is your preference.
6. At your next ordinary sleep/resume or monitor reconnect, check capture and dock position. Explorer restart is optional and should only be tested when it will not interrupt your work.
7. Exit explicitly, then restart. For now use disposable edits when checking Exit with an editor open;  covers the required failure-safe fix.

Do not clear files manually or shorten transfer grace. Supports ////.

### Keyboard, text scaling and themes

Try keyboard-only navigation through the dock, editor, history, setup and settings. Look for visible focus, accessible labels/tooltips, reachable buttons and multiple-selection behavior. Test dark/light/system theme, your normal Windows text scaling, and high contrast if you use it. Report invisible controls, clipped text and states indicated only by opacity. Screen-reader testing is useful if already available. Supports //.

### Spare clean Windows environment (optional for you)

Only if you already have a spare VM/account/machine: test portable launch and per-user installation without administrator privileges, OCR without preinstalled .NET, startup at login, upgrade and Apps uninstall. Confirm uninstall removes startup registration but intentionally retains captures/settings. Record Windows version, VC++ runtime availability and any policy/SmartScreen prompt. Otherwise mark NOT AVAILABLE; clean Windows 10/11 testing remains an agent/lab release task. Supports //.

### Normal-use performance observations

Report noticeable startup/capture/editor lag, memory growth, sustained CPU when idle or freezes during rapid captures and large images. If convenient, note Task Manager CPU/memory after launch, one minute idle, 20 captures and closing editors. You do not need to run hours-long stress tests or the pointer-moving benchmark; instrumented latency/resource measurements belong to /.

## Reporting results

Reply in chat or use a sanitized issue. No need to run unit tests, change security policies, inject clipboard locks, modify startup registry entries, corrupt history, or upload private screenshots.

```text
Build/version and portable/installed path category:
Windows version:
Monitor resolutions/scaling/HDR:
Receiver/version (where relevant):
: PASS / FAIL / NOT RUN / NOT AVAILABLE — notes
: ...
Other test IDs: ...
Failure: steps → expected → observed; frequency; rough delay
```

Provide only test IDs you ran. If you can do just two tests, prioritize ** and  in Codex**. A passed test closes its acceptance requirement only after results are recorded and any associated implementation gaps are resolved. Automated tests and code review remain complementary evidence.
