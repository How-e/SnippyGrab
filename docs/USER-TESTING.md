# Desktop testing requested from you

**Current next step:** the current dock retest passes its reported cases, including owner-confirmed insertion order. Continue with targeted real-receiver membership/order/delayed-paste checks below. The published alpha predates the latest fixes; the launcher verifies current source and bundle hashes. Keep previous paste/drop passes below.

The [2026-10-07 acceptance record](TAKEOVER-ACCEPTANCE.md) records fresh computer-use checks against the packaged native upgrade, including editor close/Discard, shelf keyboard selection and PNG export/overwrite. These use isolated synthetic data and intercepted clipboard writes. External receiver, recovery, hardware, accessibility and prolonged gates remain open. Earlier results below are historical observations of their stated builds.

Start with **U01–U05**, about 20–30 minutes. These validate the actual AI workflow that automated checks cannot prove. Remaining tests depend on available hardware/apps and can be scheduled later. Mark unavailable environments **NOT AVAILABLE**; you do not need to acquire hardware or install every listed app.

Use the current **0.1.0 alpha** and record whether it is the portable or installed build. Capture only synthetic content: a Notepad window with `CAPTURE A`, `CAPTURE B`, `CAPTURE C` and `error CS1002: ; expected`, for example. Test attachment/paste in an unsent draft; there is no need to send a message or publish an issue. Keep personal screenshots, OCR output and private logs out of the repository.

This is alpha acceptance. Implementations and agent checks are recorded in [TASK_QUEUE.md](../TASK_QUEUE.md) and [VALIDATION.md](VALIDATION.md). Use disposable captures. Do not deliberately corrupt your cache or make files unwritable. Automated failure injection and regressions are the agent's responsibility.

## Recorded user results — 2026-10-07

### P0 editor/recovery acceptance

Q02 and Q03 are now PASS / CLOSED in [the P0 acceptance record](P0-ACCEPTANCE-20261007.md). Owner supplied the actual fixture tray Exit clicks for blocked and successful multi-editor shutdown, and clicked Discard on a pending synthetic edit. Independent report/file/clipboard inspection verified each outcome. Agent exercised real metadata/OS clipboard locks, native close/retry, recovery Cancel/failed promotion/confirmation and process restarts in isolated synthetic data. No further P0 user input is needed. The current next receiver check below remains separate.

### Current local dock retest

Owner tested `0.1.0-alpha.acceptance.20261007.1+6849be66527cc69254addb551b2dcc6d75b4931f`; report `artifacts/acceptance-results/interactive-20261007-160916-061.json`. The isolated fixture uses intercepted clipboard writes. Reported monitor DPIs are 96/96/144 (100%/100%/150%). Windows build was not supplied in this feedback. The attached screenshot is not copied into Git.

| Requested step | User result | Evidence boundary / follow-up |
|---|---|---|
| 1: 1/3/5/20-capture hover, reachability and scrolling | PASS — user reports everything works as expected | Current prescribed fixture workflow; broader settings/accessibility/hardware cases remain separate. |
| 2: nonadjacent selection across leave/re-entry | PASS — user reports everything works as expected | Current fixture interaction; no external receiver evidence. |
| 3: Alt-reorder | PASS — reported adjacent and forward nonadjacent moves; dragging does not open editor | Owner explicitly chose to retain insertion and mark `[1,2,3] → [2,3,1]` for 1→3 as correct behavior. Screenshot, source and README agree: move into the target's original slot, shifting intervening items. No swapping change is required. Reverse nonadjacent acceptance was not explicitly reported. |
| 4: focus/keyboard/editor/export | PASS — user reports everything works as expected | Report confirms one intercepted image copy and an edited/dismissed capture; its final snapshot has no exported flag. Preserve the user report separately from automated export evidence. Full tool/export-dialog/accessibility matrices remain open. |
| 5: supplied local report | RECEIVED | Exact build identified. Isolated data and clipboard interception do not establish actual receiver delivery. |

These reports improve the recorded native interaction evidence without automatically closing broad queue gates. The reorder expectation is resolved; insertion is the accepted contract. Earlier reported failures remain historical results of their tested builds.

### Reverse nonadjacent reorder and selection re-entry — PASS

Owner reports **PASS for all three prescribed Q16 steps** on the same `.2` build used for the receiver check: B,A,C → C,B,A by moving C from 3 to 1; C,B,A → B,A,C by moving C from 1 to 3; selected shelf 1/3 retained and reachable after three leave/re-entry cycles without jitter or premature collapse. Neither reorder opened an editor. These are owner-reported passes in the tested current configuration, supplementing the earlier current-build adjacent reorder, reachability and scrolling results.

The specifically missing reverse-nonadjacent gesture is now verified. Q16 remains open for actual cancellation/release-outside-card/drag-threshold and scrolling/settings edge cases. Q49's wider cold/warm placement/orientation matrix and Q18/Q19 visual/accessibility cases remain separate. No full gate or configuration matrix is inferred from this three-step pass.

Completed edge-case procedure, in the same running build with synthetic captures: (1) begin an Alt-reorder drag, press Esc while holding the mouse, then release: order stays unchanged and no editor opens; (2) Alt-drag to empty desktop space and release: no reorder/editor/stuck expansion; re-enter and verify normal controls; (3) with at least ten disposable captures, Ctrl-select two captures separated by scrolling, scroll between them, leave/re-enter and verify both selections survive; perform a nearby Alt-reorder among the later captures, confirm the intended insertion and no accidental editor. Owner subsequently reports **steps 1–3 PASS**. Never drop onto a receiver or file-management target for the cancellation tests.

**Q16 PASS / CLOSED** for its stated interaction criteria. Combined owner results now cover adjacent/nonadjacent insertion both ways, reachability, selection transitions, scrolling, cancellation/outside release and no editor on drag. Previous actual click-to-edit and Q15 focus/keyboard evidence cover click/drag and keyboard-use distinctions; existing independent pointer-capture/routing/layout/cache checks remain complementary. The full settings/visual/hardware matrix belongs to Q18/Q19/Q32/Q49, which remain open. No all-settings or all-hardware pass is claimed.

### Next dock settings check — Q49/Q18

Use the current local **0.1.0-alpha.acceptance.20261007.3** app and existing disposable captures. Start with `pwsh ./scripts/start-acceptance.ps1 -RealApp`. The completed `.2` results above retain their original source provenance. Record current corner, orientation, thumbnail size, animation and collapse/hide settings before changing them. First switch orientation (vertical ↔ horizontal) and place the dock at the opposite corner. Enter, reach every visible card/control, leave and re-enter five times, with and without two selected images; scroll through the shelf. Expect an anchored primary card, inward expansion, no visible jump/freeze or unreachable controls, and stable selection. Then test the smallest and largest thumbnail sizes offered in Settings, with animations off and on, repeating those hover checks. Report each configuration separately; restore original settings afterward. Do not change Windows accessibility/animation preferences for this test. Existing current-configuration passes do not need immediate repetition.

### Codex selected-transfer acceptance — PASS

Owner report in chat: **Codex 0.160.1; all prescribed steps 2–4 PASS**. Tested candidate from the preceding handoff: **0.1.0-alpha.acceptance.20261007.2**, clean source `06f4c9acd70be3253fbb11fcc02d75ffea391ed1` as verified in its local BUILD-PROVENANCE.json. These are owner-reported real receiver results, not agent automation.

| Case | Owner result |
|---|---|
| Nonadjacent shelf 1/3 drop, expected exactly C and A | PASS |
| Drop after Alt-reorder to B,A,C, expected exactly A and C | PASS |
| Selected-file Ctrl+C, one minute without another clipboard write, then paste in an empty draft | PASS |

No failure, missing image or missing-file error was reported. PASS covers the prescribed count/content/order checks; no separate attachment-order trace or screenshot was supplied. The delayed case proves the tested one-minute paste/file availability, not deferred reads by an already-reading receiver or the full 24-hour crash grace.

Q50 transfer-order receiver acceptance closes with these results plus existing independent payload/order/scrolling regressions. Q30 primary Codex acceptance closes with these results and the earlier U01/single-drop passes. Q12 stays open for exact-version coverage of other receivers; Q13 keeps its native retention/crash criteria.

### Completed targeted real-receiver procedure

The procedure below is retained as the exact test specification for the PASS above. No immediate repetition is needed. Documentation commits made after acceptance do not change the tested binary; the launcher's strict HEAD check will require a newly matching package for a future relaunch through that script.

Use the newly prepared local **0.1.0-alpha.acceptance.20261007.2** candidate, which includes the verified canvas-shortcut fix. Q15 and Q21 are closed in [targeted native acceptance](NEXT-GATE-ACCEPTANCE-20261007.md); full accessibility and receiver gates remain open. The default launcher now selects this candidate and checks its source revision/hashes.

Exit the fixture with **Exit checks** and exit other SnippyGrab instances through their trays. Run `pwsh ./scripts/start-acceptance.ps1 -RealApp`. This mode uses normal capture history, hotkeys and the OS clipboard. Use synthetic content and an unsent Codex draft; no message needs sending.

1. Capture distinct A, then B, then C images. Shelf numbers should be 1=C, 2=B, 3=A. Record SnippyGrab and Codex versions.
2. Ctrl-select shelf numbers 1 and 3 and drag a selected card into the draft. Expect exactly C and A; SnippyGrab payload order is C,A. Record the receiver's displayed order separately.
3. Clear selection, Alt-drag C from slot 1 to slot 3, then select A and C. Current shelf order is B,A,C, so the selected payload should be A,C. Verify exactly those two images and their displayed order.
4. With A and C selected, focus the shelf and press Ctrl+C to copy file-drop data. Wait one minute without another clipboard write, then paste into an empty unsent draft. Record supported/unsupported, exact count/content/order and any missing-file error. This tests delayed paste/file availability, not proof that a receiver which already read files deferred its reads.

Report PASS/FAIL/NOT SUPPORTED per case and exact receiver version. Ordinary previous single-image paste/drop successes stand; only these gaps need targeted verification now.

During publication review, the owner reported that drop and paste worked in Codex, ChatGPT, VS Code and a browser. These are user-reported passes for ordinary receiver behavior. Exact build/receiver versions, browser identity, selected-image membership/order and delayed-read coverage were not supplied. Preserve these successes without treating the complete Q12/Q30/Q50 matrix as closed.

## Recorded user results — 2026-10-06

Source: user report in chat following U01–U06 testing. These results were not independently rerun by the agent. Exact tested build/commit, portable/installed category, Windows version, receiver version, monitor layout, repetition counts and timings were not provided. Record those details on the next relevant retest; unspecified checklist cases remain unverified. PASS below applies to reported behavior, not an entire milestone or stable-release gate.

| Test | Reported result | Findings | Queue follow-up |
|---|---|---|---|
| Dock first-hover / U03 / U12 | FAIL | With multiple captures, first hover causes visible glitching/movement before settling. When expanded, moving to the top image makes the dock lose hover and collapse, preventing access. | Q49/Q16/Q18: first-hover stability and reachable expansion; highest P1 work. |
| U01 | PASS — reported cases | Capture preserved Codex composer focus; Ctrl+V uploaded the correct latest image. Esc and click without selection created no capture and left no dimming. | Q08/Q12/Q30: retain success; exact repeat count/timing and other capture cases remain unreported. |
| U02 | PARTIAL — correct single drag; order observed | One dragged image was correct despite multiple captures. Selecting 3, 2, 1 displayed 1, 2, 3 in Codex, matching PiP order rather than selection-click order. Latest capture is 1; previous captures move down. No explicit exact-count/nonadjacent/delayed-read result supplied. | Q50: define ordering contract and verify payload versus receiver order; Q12/Q30 remain open for remaining cases. |
| U03 | FAIL / PARTIAL | Without selection, pointer leave collapses as intended. With selections, leaving keeps up to three captures open; re-hover instantly collapses without a click, preserving selections. Alt-reorder works but moving 1 to 2 requires dropping on 3; upward moves accept the adjacent item. Delete did not work on a selected capture; Enter did not work on hover. Dismiss works; clicking a capture opens the editor. | Q16: collapse/hover/reorder; Q15/Q19: explicit keyboard focus/actions; Q32: retest after fixes. Keyboard focus during failed shortcuts was not confirmed. |
| U04 | PARTIAL — editor reported successful; Save unclear | Editor behavior was reported working well. Save destination could not be verified: no images found in the configured directory. Save As opened the correct directory. | Q21: distinguish apply/copy from permanent export and show destination. Current source labels apply/copy as Save. Q20/Q22/Q23 retain detailed/unreported and fault/large-image checks. |
| U05 | PASS — reported behavior | OCR was reported working as intended; no errors reported. Separate full-image/area results, input cases and timing were not supplied. | Q14/Q24: preserve successful observation; fault/cancellation/package and unreported cases remain open. |
| U06 | PARTIAL — prerequisite confirmed | Windows “Use the Print Screen key to open screen capture” had to be off to prevent Snipping Tool taking control of SnippyGrab. Other modes/fallback/pause/repeat cases were not reported. | Q09: actionable setup/help and fallback; Q08: remaining capture-mode checks. |

Next targeted retest order, after corresponding fixes and agent verification: dock first-hover/top-card reachability and selection leave/re-enter; adjacent Alt-reorder both ways; dock focus/Delete/Enter; Save/Save As destination; selected transfer order after reordering. Check Codex focus/latest paste again after dock changes. U07–U12 have no separate completed report; the initial dock performance observation is relevant to U12.

Overlay follow-up: user reported that the corrected overlay now covers the monitors properly after the overlay-bounds fix (`c4b78ab`). This establishes reported coverage only; bottom-edge/cross-monitor crop accuracy, Esc and paste were not separately reported. Exact running build/version was not supplied. Keep broader U01/U07 and hardware gates open.

## First batch: the primary workflow

### P0 implementation retest — Q02/Q03

After installing the updated P0 build, use disposable synthetic captures for U04: edit then close and paste, Copy then immediately close, Exit with two dirty editors, and Discard an unsaved edit. Confirm applied edits survive restart in Recent captures. Record the build and any premature close, wrong clipboard content or lost edit. Failure messages should keep the editor open and explain retry/Discard.

History recovery is tested automatically in isolated directories. Do not corrupt your real cache. If recovery is already offered naturally, Recent captures explains that unknown old images are pinned and cleanup is disabled. Cancel confirmation first, restart and verify new pins remain. Confirm only after reviewing the explanation; unknown images should remain pinned afterward. Recovery UI interaction remains unverified if the banner is never encountered. Do not run the pointer-moving self-test during normal desktop use.

### U01 — Print Screen → immediate Codex paste

1. Focus an unsent Codex composer with synthetic content visible nearby.
2. Press Print Screen, select a region and release. Immediately press Ctrl+V without clicking the dock.
3. Confirm Codex receives the correct image, the dock appears quietly, and keyboard focus remains usable in Codex.
4. Repeat with different regions, including several rapid captures, up to 20 times. Check that the pasted capture is always the latest one.
5. Try Esc cancellation and a click without a meaningful selection. Neither should add an unwanted capture or leave the desktop dimmed.

Report incorrect/stale images, duplicate captures, visible delay, focus loss or any need to click Codex again. Approximate timing is useful; you do not need specialized measurement tools. Supports Q08/Q12/Q30/Q34.

### U02 — Single and multiple screenshots dragged into Codex

1. Capture labeled A, B, C and D regions.
2. Drag one thumbnail into the Codex composer. Verify one correct image arrives, rather than a text path or internal object, and the editor does not open accidentally.
3. Ctrl-click 2–4 captures, including a nonadjacent pair; drag one of the selected images into the composer.
4. Verify exactly the selected images arrive, without duplicates or omitted files. Record their displayed order.
5. If possible, leave the draft for a minute and confirm the attachments remain readable. Cancel/discard the draft when done.

If a browser/editor rejects the drop, report what happened and its version; receiver support must be established per application. Supports Q12/Q13/Q30.

Q50 order retest: select captures in reverse-click order and a nonadjacent pair; both file-copy and drag should deliver exactly those captures in ascending current shelf-number order. Alt-reorder and repeat, including selections spanning scrolling. The numbered order runs away from the anchored primary card; it may run right-to-left or bottom-to-top. Record receiver presentation separately from this payload contract, with build/receiver versions. History file transfers use underlying shelf order, which can differ from the history list's time order.

### U03 — Compact shelf, scrolling and selection

1. With 10–20 disposable captures, move away from the dock. It should occupy a small footprint rather than cover the desktop.
2. Hover, wheel through captures, Ctrl-click several, and move away again. Check visible selection feedback and sensible expansion/collapse.
3. Alt-drag one thumbnail onto another to reorder; verify scrolling and later dragging use the expected order.
4. Select a capture other than the main thumbnail. With focus on the dock, test Ctrl+C and paste into a suitable draft; then test Delete on disposable captures. Check that these actions affect the selected items. This is a known code-review risk, so report mismatches.
5. Try Enter to edit, Ctrl+S to export, and Escape to close/deselect where supported. Check hover buttons/tooltips and accidental clicks while beginning a drag.
6. Change thumbnail size, orientation, opacity, topmost, auto-collapse and auto-hide in Settings. Check that the dock stays on-screen and usable.

For the reported regressions, also check first hover with 3/5/20 captures, move from the collapsed dock to the top expanded card, leave/re-enter with selections, and Alt-move adjacent items both upward and downward. Confirm keyboard focus is visibly on the dock before testing Delete/Enter; hovering alone may leave keys with the previous application.

Dock reliability retest: the primary card should stay at the chosen corner while other cards grow inward. Move through padding/gaps and reach every displayed card/control; leaving should collapse after a short delay, with the same selection restored on re-entry. Try cold and repeated hover with 1/3/5/20 captures, scrolling, adjacent/nonadjacent Alt-moves in both directions, starting a drag then canceling, and releasing a simple press outside its card. Repeat your normal Codex capture/paste and selected-file drop checks.

Keyboard implementation retest: click a numbered badge (it should focus without editing), or select Focus screenshot shelf (keyboard) from the tray. Check the visible Focus/Selected text. Navigate with arrows/Home/End and toggle Space on a nonprimary capture, then Ctrl+C and Delete on disposable content. Select two nonadjacent captures, scroll to focus an unselected third capture, and verify Copy/Delete still use exactly the selected pair while Enter/Ctrl+S/Ctrl+P use the focused third capture. Tab/Shift+Tab should reach card controls; Enter/Space on a button should activate that button. Ctrl+H/Ctrl+, should open history/settings. Escape should clear selection and collapse. Confirm a fresh capture still preserves composer focus. Actual native focus, clipboard output and accessibility acceptance remain pending until this retest.

Report screen coverage, hidden selections, premature hiding, controls that cannot be reached and any action using the wrong capture. Supports Q15/Q16/Q18/Q19/Q29/Q32/Q47/Q49; transfer ordering is Q50.

### U04 — Editor and updated screenshot

1. Click a synthetic capture. Add an arrow, rectangle, ellipse, line, freehand stroke, text, highlighter and numbered markers. Try crop, zoom and undo/redo, including undo followed by a different edit.
2. On synthetic text, separately try blur, pixelate and solid redaction. Confirm the exported redaction covers the text completely. Blur/pixelation should not be treated as guaranteed secrecy.
3. Use Copy; paste into Codex and verify edits. Use Save and Save As; note whether their behavior matches their labels and whether the exported PNG opens correctly.
4. Edit again and close the editor. Paste and inspect the dock thumbnail; they should show the updated image. Test Discard on another capture.
5. Reduce the editor width and increase text size if convenient. All required actions should remain reachable.

There is no need to create disk-full/permission failures. Do not rely on unverified close/Exit handling for important edits. Supports Q02/Q20–Q23/Q32.

Q21 export retest: Apply + copy should update shelf/clipboard without creating a file in the export directory. Export PNG (Ctrl+S) should show that directory on first export and show the previous destination on repeat export. Verify the PNG contains edits, the full path appears, and Open export folder opens the right directory. Cancel a dialog and confirm no new success message/file. Check overwrite confirmation on a disposable export. Export should still work independently of clipboard availability; do not deliberately damage your cache or create real storage failures.

### U05 — Local OCR

1. Capture synthetic compiler/terminal text. Use the dock OCR action and paste into Notepad or an unsent Codex draft.
2. Open the editor and use `OcrArea` to select only part of the text; verify the copied output is limited to that area.
3. Try small text and a multiline synthetic stack trace. Record character errors and rough completion time.
4. If OCR fails, report the message and whether region capture still works. You do not need to remove DLLs/models or change runtime installations.

OCR is local; English is the current bundled language. Review output before relying on it. Supports Q14/Q24.

## Additional acceptance when convenient

### U06 — Hotkeys and Windows interception

Test region, desktop, active window and window picker shortcuts from the README; verify modes and Esc behavior. Test a configurable fallback, pause/resume and whether holding a key produces duplicate captures. If Print Screen opens Windows capture instead, check that SnippyGrab offers useful guidance/fallback.

If comfortable, test Windows **Settings → Accessibility → Keyboard → Use the Print Screen key to open screen capture** both on and off, restarting SnippyGrab as instructed. Record and restore your preferred setting afterward. Do not edit the registry or terminate other hotkey applications just for this test. Supports Q08/Q09.

The 2026-10-06 user test confirmed this setting needed to be **off** for SnippyGrab to receive Print Screen on the tested system. If you prefer to keep Windows interception enabled, use a configurable SnippyGrab fallback shortcut; remaining fallback acceptance is tracked in Q09.

### U07 — Your monitor/DPI layout

Record monitor resolutions, scaling percentages, arrangement and HDR status. On each available monitor, capture a clearly outlined rectangle, including near screen edges. Compare the saved dimensions/pixels with the intended physical region. Try a selection spanning monitors, reversed drag and dock placement in all four currently supported corners.

The remaining coverage is 125%, 175%, 200%, negative origins, mixed scaling, vertical monitors and HDR. Test only available layouts. If changing scaling/arrangement, record your original configuration and restore it. Check cursor inclusion on/off; report unexpected cursor position or HDR color differences. GDI output is currently SDR. Supports Q10/Q11/Q31.

### U08 — Other applications you actually use

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

Unsupported image paste in a plain text terminal is not automatically an app defect; test OCR text paste there instead. Supports Q12.

### U09 — Tray, history, pinning and OS lifecycle

1. Close editor/settings/history windows and confirm the tray app still captures. Pause/resume hotkeys through the tray.
2. Pin a disposable capture and detach it. Try resize, opacity, topmost and click-through; use tray **Restore pins** to regain interaction. Edit and inspect the detached image for staleness.
3. Clear temporary captures via the tray. Pins should survive. Restart normally and verify pin records remain available in history/dock. Do not use private captures for this test.
4. Dismiss a capture and restore it through Recent captures. For an expired capture, use a short configured shelf lifetime and then restore; report if it does not reappear. Open history, capture again and check whether the open list updates.
5. Enable login startup only if desired. At your next normal login, check silent tray startup and hotkeys. Disable afterward if that is your preference.
6. At your next ordinary sleep/resume or monitor reconnect, check capture and dock position. Explorer restart is optional and should only be tested when it will not interrupt your work.
7. Exit explicitly, then restart. For now use disposable edits when checking Exit with an editor open; Q02 covers the required failure-safe fix.

Do not clear files manually or shorten transfer grace. Supports Q03–Q05/Q10/Q25/Q27/Q29.

### U10 — Keyboard, text scaling and themes

Try keyboard-only navigation through the dock, editor, history, setup and settings. Look for visible focus, accessible labels/tooltips, reachable buttons and multiple-selection behavior. Test dark/light/system theme, your normal Windows text scaling, and high contrast if you use it. Report invisible controls, clipped text and states indicated only by opacity. Screen-reader testing is useful if already available. Supports Q19/Q23/Q32.

### U11 — Spare clean Windows environment (optional for you)

Only if you already have a spare VM/account/machine: test portable launch and per-user installation without administrator privileges, OCR without preinstalled .NET, startup at login, upgrade and Apps uninstall. Confirm uninstall removes startup registration but intentionally retains captures/settings. Record Windows version, VC++ runtime availability and any policy/SmartScreen prompt. Otherwise mark NOT AVAILABLE; clean Windows 10/11 testing remains an agent/lab release task. Supports Q27/Q40/Q41.

### U12 — Normal-use performance observations

Report noticeable startup/capture/editor lag, memory growth, sustained CPU when idle or freezes during rapid captures and large images. If convenient, note Task Manager CPU/memory after launch, one minute idle, 20 captures and closing editors. Your completed two-hour Q35 test passes the [resource acceptance review](RESOURCE-REVIEW-20261007.md). [RESOURCE-TESTING.md](RESOURCE-TESTING.md) retains the command and interpretation for future manual regressions. The agent will not run that prolonged test automatically. The pointer-moving benchmark and broader Q34 latency comparison remain separate.

## Reporting results

Reply in chat or use a sanitized issue. No need to run unit tests, change security policies, inject clipboard locks, modify startup registry entries, corrupt history, or upload private screenshots.

```text
Build/version and portable/installed path category:
Windows version:
Monitor resolutions/scaling/HDR:
Receiver/version (where relevant):
U01: PASS / FAIL / NOT RUN / NOT AVAILABLE — notes
U02: ...
Other test IDs: ...
Failure: steps → expected → observed; frequency; rough delay
```

Provide only test IDs you ran. If you can do just two tests, prioritize **U01 and U02 in Codex**. A passed test closes its acceptance requirement only after results are recorded and any associated implementation gaps are resolved. Automated tests and code review remain complementary evidence.

## Agent interaction results — second ten-item pass, 2026-10-06

These are independent computer-use results in an isolated development fixture. Normal cache/settings/startup registration were not changed. Clipboard writes were intercepted; hotkeys were disabled. The fixture exposes dock/overlay taskbar entries so the computer-use inventory can target them. This means keyboard handlers and gestures were exercised, but ordinary nonactivating/taskbar behavior and external clipboard delivery are separate gates. Real native capture buttons called the production capture service. The actual monitor layout was 1920×1080 at 100%, another 1920×1080 at 100% with negative X, and 2560×1600 at 150% with negative Y. Native virtual desktop was 6400×1600.

| Check | Agent result | What still needs manual/lab coverage |
|---|---|---|
| Explicit shelf badge focus, Up, Space, Ctrl+C, Delete, Enter | PASS: nonprimary copy reached the image sink; Delete dismissed only the selected nonprimary record; Enter opened its separate editor | Ordinary production focus/composer restoration, assistive technology and remaining selection/reorder gestures |
| Three-capture first entry and top-card reachability | PASS for observed padding-entry/top-padding route; no jump/collapse observed | More cold/warm cycles, all placements, selection leave/re-entry and Alt-reorder; no broad jitter closure |
| Editor redaction, keyboard undo/redo | PASS: reversed redaction, undo removed it and redo restored it | Other human gestures; all required flattened tools have render/document regressions |
| Native Export PNG dialog | PASS: configured directory, exported file/full-path success, remembered filename, overwrite prompt, decline and cancel | Accepted overwrite/open-folder path, other destinations/permissions; failure cases are injected automatically |
| Crop, zoom, freehand and dirty close | PASS: reversed crop produced 615×120, 120% zoomed freehand aligned, close committed 615×120 and reached image sink | Real clipboard delivery and injected-failure UX; offscreen close-during-worker regression covers final worker coordination |
| Full-image OCR | PASS: actual editor displayed local progress and text-copy success in the intercepted sink | OS clipboard/receiver behavior and arbitrary text recognition; a 30px fixture confuses 0/@ |
| Capture modes | PASS: active-window and picked fixture both 746×423; full desktop 6400×1600; reversed region exactly 320×80 with decoded synthetic text; Esc preserved 25 captures and returned to fixture | Global hotkeys, protected/maximized/offscreen content, cross-monitor gestures, remaining DPI/HDR/topology hardware matrix |
| Hidden dock / display-change handler | PASS: dock stayed hidden after topology refresh | Physical disconnect/reconnect and driver/device identity changes |

Computer-use initially scaled its 1920×1080 overlay screenshot across the 6400×1600 target. Correcting that tool mapping produced the exact 320×80 crop; this was an automation coordinate issue, not an app crop fix. Initial offscreen runs were disturbed by external pointer/focus changes; a later run without concurrent computer-use inputs passed all 64 dock layouts and four overlays.

Manual retesting can focus on the rightmost column rather than repeating agent-proven file existence, editor document pixels, command routing, storage faults and ordinary dialog cancellation. Q30 external Codex acceptance is still open: the Windows computer-use skill does not allow automating the Codex/ChatGPT desktop UI. No attachment was transmitted by this pass.

The final portable build is **0.1.0-alpha.hardening.20261006**. Independent computer-use on that packaged executable also passed full-image OCR, reversed blur plus Apply + copy, persisted Edited state and intercepted text/image writes, and readable choices for all three connected displays. It exited normally. The development results above cover the longer capture/export/crop/keyboard scenario; the packaged pass retested the final worker/native-OCR paths. Shared acceptance still applies to the rightmost column. Development test-cache cleanup was blocked by automatic approval review; isolated caches remain in Windows Temp and their roots are recorded in local ignored reports.

Q27 login retest: enable login startup from the intended installed executable; confirm the quoted path and --background in the per-user Run entry. After an upgrade, confirm it points to the new executable. A stale/malformed/other-path entry is reported as disabled with repair guidance. Sign out/in to verify silent tray launch after completed setup. Fresh profiles still get opt-in setup. No real login was performed by the agent.


Third-pass retest focus: U09 Explorer restart, sleep/resume (including paused hotkeys), hidden shelf after monitor changes and configured tray double-click capture mode; U11 executable setup/startup opt-in, upgrade, fresh-login silent tray and Apps uninstall. Settings now uses named retention choices. Captures/settings are retained on uninstall; upgrade backups are kept outside the active application directory. These cases were not marked passed from file-only or offscreen fixtures. No immediate repetition of previously successful Codex/OCR cases is requested solely because of these packaging changes.
