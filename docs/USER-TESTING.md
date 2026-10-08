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

### Completed dock settings check — Q49/Q18

Use the current local **0.1.0-alpha.acceptance.20261007.3** app and existing disposable captures. Start with `pwsh ./scripts/start-acceptance.ps1 -RealApp`. The completed `.2` results above retain their original source provenance. Record current corner, orientation, thumbnail size, animation and collapse/hide settings before changing them. First switch orientation (vertical ↔ horizontal) and place the dock at the opposite corner. Enter, reach every visible card/control, leave and re-enter five times, with and without two selected images; scroll through the shelf. Expect an anchored primary card, inward expansion, no visible jump/freeze or unreachable controls, and stable selection. Then test the smallest and largest thumbnail sizes offered in Settings, with animations off and on, repeating those hover checks. Report each configuration separately; restore original settings afterward. Do not change Windows accessibility/animation preferences for this test. Existing current-configuration passes do not need immediate repetition.

Owner subsequently reports **PASS for all steps** on `.3`, clean source `9103b9d` verified in the bundle provenance. This covers the alternate orientation/opposite corner, reachability/scrolling, five leave/re-entry cycles with/without selection, smallest/largest thumbnails with animations off/on, and restoring original settings. Exact corner names, numeric sizes and original setting values were requested but not supplied; record PASS for the prescribed relative configurations without inventing those values.

**Q49 PASS / CLOSED** with this actual owner report, earlier 1/3/5/20 cold/repeated-hover acceptance, Q16 selection/reorder/cancellation results and the independent 64-layout/768-anchor-sample evidence. It establishes the stated jitter/reachability/collapse criteria, not every monitor/topology/accessibility setting.

### Completed visual check — Q18

The completed hover tests used existing captures. **Fade new captures into the shelf** affects new arrivals, so its actual fade remains untested. In the current running build, turn that setting off and capture one synthetic image: expect immediate stable appearance. Turn it on and capture another: expect a brief stable fade if Windows animations are enabled, otherwise immediate appearance. Inspect Light and Dark application themes at smallest/largest thumbnail sizes: screenshot edges stay inside rounded cards, badges/selection/focus/buttons remain readable, and controls are not clipped or obscured. Restore original settings. Report PASS/FAIL for arrivals off/on and both themes, including whether fade was visible. If you already use Windows reduced motion/high contrast, mention that existing configuration; no OS preference change is requested. Q18 remains open for this visual/motion acceptance; bounded thumbnail work and hover stability have evidence.

Owner subsequently reports **PASS for all**. The actual supplied chat procedure specified immediate stable arrivals with application fade off/on because a read-only WPF OS query showed **ClientAreaAnimation=false, HighContrast=false**. Both arrival cases and Light/Dark themes at minimum/maximum thumbnail sizes pass, including rounded clipping, readable badges/selection/focus/buttons, and restoring settings. The handoff allowed the running `.3` or matching rebuilt `.4`; owner did not specify which was used. Both have identical application source; retain that exact-version limitation rather than inventing a version.

**Q18 PASS / CLOSED for its stated bounded-work, presentation and reduced-motion criteria**, combining this visual report, earlier hover/settings/scrolling reports, Q49/Q16 closure and independent cache/revision/layout tests. No normal-animation-enabled fade or actual high-contrast/screen-reader result is claimed; those configurations remain unverified in the broader Q19/Q32 accessibility matrix. Existing arrival-only animation design and deferred expansion animation are unchanged.

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

## Next owner gate — Q19 keyboard navigation (2026-10-07)

Use the fresh acceptance .5 package: exit older SnippyGrab instances through their tray, then run `pwsh ./scripts/start-acceptance.ps1 -RealApp` from the repository. This package contains the hotkey-field Tab correction; .3/.4 do not. Use synthetic captures. Keep existing Windows accessibility preferences.

1. Focus the shelf through its numbered badge/tray action, press Ctrl+, for Settings. Use Tab and Shift+Tab across all five hotkey fields and the rest of the controls. Existing bindings must stay unchanged; focus must advance/reverse and bring offscreen controls into view. Change Include cursor with Space twice to restore it, then Tab to Cancel and press Enter. Do not activate Reset or Clear temporary captures.
2. Focus the shelf and press Ctrl+H for Recent captures. Tab to the capture list, select a synthetic capture with arrow keys, and confirm the preview matches. Tab/Shift+Tab to Edit and press Enter. Selection and focus must remain understandable and controls reachable.
3. In the editor, Tab/Shift+Tab across tool choices, text/color/stroke fields, zoom/fit and action buttons. From a toolbar button (outside a text field), press Ctrl+S, then Cancel the export dialog with Escape. Close the unchanged editor with Escape. Expect visible focus, no trap, no accidental annotation/export and the original capture retained.
4. At your current Windows text scale, repeat a brief focus/readability check in Light and Dark application themes for Settings, History and Editor. Labels, values and action buttons must stay readable and unclipped. Restore your theme. Report any existing high-contrast/screen-reader coverage separately; if unavailable report NOT AVAILABLE. No OS changes or software installation are required.

Report PASS/FAIL for steps 1–4 and any confusing focus position. These are a keyboard/theme subset of Q19; unavailable high-contrast, screen-reader and other text-scale/hardware cases remain open under Q19/Q32.

## Q19/Q32 — owner keyboard and theme subset PASS (2026-10-07)

Owner reports PASS for all four prescribed steps on the handoff's acceptance `.5` build (source `0989076`): Settings Tab/Shift+Tab through all hotkey fields and controls without changing bindings, reversible checkbox navigation and Cancel; History arrow selection/matching preview and keyboard Edit; editor tool/field/zoom/action traversal, keyboard export cancellation and unchanged close; readable focus/labels/actions in Light/Dark at the current Windows text scale, with theme restored. This is owner-reported real interaction, distinct from the earlier isolated native agent checks.

No separate high-contrast or screen-reader result was supplied. System theme, keyboard capture/copy/export/pin end-to-end and the wider text-scale/hardware/assistive-technology matrix remain unverified. Q19 and Q32 stay OPEN; queue remains 25 required gates open / 26 checked. Next owner check targets the remaining shelf copy/export/pin keyboard workflow and System theme. Do not repeat the four successful checks solely for this documentation change.

## Next owner gate — remaining Q19/Q32 shelf workflow and System theme

Use acceptance `.6` after exiting older SnippyGrab through its tray and running `pwsh ./scripts/start-acceptance.ps1 -RealApp`. Application source is unchanged from the passed `.5`; this refresh keeps launcher provenance current after recording results. Create two disposable synthetic captures labelled A and B. Use an unsent draft or image-capable local app for paste inspection; do not send content.

1. Focus the shelf using its numbered badge/tray focus action. Press Escape to clear prior selection, refocus, use arrows to focus A and Space to select only A. Ctrl+C, switch to the draft and Ctrl+V: expect A, not newer B. Return to shelf, focus A, press Ctrl+P. Ctrl+H: the A row must show PIN. Close History with Escape, refocus A and Ctrl+P again to restore unpinned state. The pin state must be readable as text/icon, not only transparency.
2. With A focused, Ctrl+S from the shelf. Use the save dialog keyboard to export to a disposable PNG path. Open that file and confirm it is A. Refocus A and Ctrl+S again, then Escape: no extra file or false export-success message. This verifies the shelf keyboard route, not another repeat of editor drawing/overwrite tests.
3. In Settings choose System theme and Apply. Check shelf focus/selection/pin cues plus Settings, History and Editor at your current Windows text scale: readable contrast, visible focus, reachable controls and no clipping. Restore your original application theme. No Windows preference changes are required.

Report PASS/FAIL for steps 1–3, and separately whether high contrast or a screen reader is already available. These tests do not establish unavailable accessibility configurations or keyboard global-hotkey capture behavior; those remain open.

## Q19/Q32 — shelf copy/export/pin and System theme PASS (2026-10-07)

Owner reports steps 1–3 PASS: keyboard copy uses selected A rather than newer B; Ctrl+P pin state is visible in History and can be restored; shelf Ctrl+S exports the focused A and repeat-export cancellation creates no extra file/false success; System theme has readable contrast, focus/state cues and reachable unclipped shelf/Settings/History/Editor controls at the current text scale, with original theme restored. The existing acceptance application remains running; owner explicitly requests no further build unless necessary. Exact running version was not separately restated; the handoff named `.6`, whose application source is identical to `.5`.

Q19/Q32 remain OPEN for the unverified keyboard global-capture path and broader text-scale/high-contrast/assistive-technology coverage. High contrast/screen-reader availability was not separately reported. This closes the prescribed workflow/theme subset, not the complete gates. Current queue is **25 required gates open; 26 entries checked**. No application source change or package rebuild is needed. Use the existing running build for the next checks; the strict current-HEAD launcher will require a fresh matching package if a future restart is needed after these documentation commits.

## Next owner gate — Q09 fallback/help and Q19 capture path

Keep the existing acceptance app running. No rebuild/restart or Windows preference change is needed. Use only synthetic on-screen content. These checks follow completed shelf/editor/settings interactions and cover the remaining global-hotkey entry path. Existing automated registration/no-repeat/pause-state tests remain complementary, not proof of actual Windows delivery.

1. Tray → Hotkey help / conflicts: verify guidance identifies the Windows Print Screen interception setting, restarting after changing it, the currently configured region fallback and tray Capture region alternative. Close Help. Press the displayed fallback (default Ctrl+Shift+S), then Escape: no new capture. Repeat the fallback and capture a synthetic rectangle: exactly one correct new capture. Focus its shelf badge, Ctrl+C and paste into an unsent draft: expect that capture.
2. Tray → Pause hotkeys. Press the fallback: SnippyGrab must not open an overlay or add a capture. While paused, open Settings and close with Cancel; confirm Pause hotkeys remains checked and fallback still does not capture. Toggle Pause hotkeys off, then fallback and Escape: normal operation returns. Do not treat another app responding to the key as a SnippyGrab capture.
3. With hotkeys resumed, hold the fallback combination for about two seconds, release all keys, then Escape. Expect one overlay, no repeated or queued overlay after cancellation, no new capture and no stuck keys. Finally tray → Capture region, then Escape: tray alternative works independently. Restore the original paused state if it was paused before testing.

Report PASS/FAIL per step. If the fallback conflicts or is disabled, report the configured combination and displayed warning instead of marking delivery PASS. No deliberate conflicts, OS-setting changes or termination of other hotkey applications are required. High contrast/screen-reader availability remains a separate unanswered Q19/Q32 coverage item.

## Q09/Q19 — actual fallback, pause and held-key subset PASS (2026-10-07)

Owner reports steps 1–3 PASS on the existing running acceptance application: complete Hotkey help / conflicts guidance; fallback Escape cancellation with no capture; fallback synthetic region produces exactly one correct capture and shelf copy/paste delivers it; Pause prevents SnippyGrab capture and is preserved across Settings Cancel; resumed fallback works; holding the combination produces one overlay with no queued repeats/stuck keys; tray Capture region alternative works and cancels. This establishes the actual global-keyboard entry/copy path alongside prior shelf/editor/history/settings acceptance. Exact configured combination/version was not separately restated; the prescribed fallback is default Ctrl+Shift+S unless changed.

Q09 stays OPEN: actual other-owner/fallback conflict, first-run recovery with Print Screen unavailable and the full interception/reconfiguration cases were not exercised by these steps. Earlier owner Windows Print Screen prerequisite and injected registration/pause/no-repeat regressions remain complementary. Q19/Q32 broader high-contrast/text-scale/assistive-technology coverage remains unverified; no separate availability report was supplied. **25 required gates open; 26 entries checked.** No rebuild or source change; preserve the current running acceptance application. Documentation commits do not invalidate its runtime evidence.

## Next owner gate — Q04/Q22 detached pin and edited revision

Keep the current acceptance application running; no new build, restart or OS changes. Use one disposable synthetic capture labelled PIN TEST and preserve its identity across the following steps. Agent storage/lease/cleanup and revision regressions already pass; do not clear the real cache or induce storage failures.

1. Right-click the PIN TEST shelf card → Detach pin. Expect one separate floating view of the same image. Drag it to another position, resize it, adjust its context-menu opacity then restore full opacity. Toggle always on top off/on and restore the intended state. Expect the image and menu to remain reachable without blanking or losing the capture.
2. Leave Recent captures open with PIN TEST selected so its preview is visible. From the floating pin's context menu choose Edit (or double-click it), add a conspicuous rectangle/arrow and Apply + copy. Confirm the floating pin, shelf thumbnail, already-open History preview and pasted image in an unsent draft all show the same updated annotation. Opening Edit again for that same capture should activate the existing editor rather than create a conflicting second editor. Close the applied editor normally.
3. Unpin PIN TEST from the shelf/history while the floating window remains open. Expect the floating image to remain usable; its Copy action must still deliver the edited image. Do not run Clear temporary captures. Floating pin → Return to shelf: expect the window to close and the edited capture to be immediately reachable on the shelf.
4. Detach PIN TEST again, context menu → Click-through. Confirm the floating view lets a click reach a harmless underlying local window. Tray → Restore pins must restore pin interaction; its context menu must work again. Choose Close pin window: only that floating window closes, with the capture still available in shelf/history. Unpin the disposable capture if needed to restore its original state.

Report PASS/FAIL for steps 1–4 and identify any stale/blank view or lost image. These are actual pin/revision/return/interaction cases under Q04/Q22/Q29; persistence after restart, expiry timing and mixed-DPI/hardware cases remain separate. Do not repeat broader editor tools or external multi-image delivery for this batch.

## Q04 — detached pin lease/revision and actual gesture closure

Owner reports all four prescribed pin steps PASS on the existing running acceptance application: detach/move/resize/opacity/topmost; edited image refresh in detached pin/shelf/open History/pasted output; unpin while floating remains usable and copies edited pixels; Return to shelf closes floating view and reveals image; click-through is recoverable through Restore pins and Close pin window preserves the capture. Exact package version was not separately restated; the prior handoff used .6 (same application source as .5).

**Q04 PASS / CLOSED** for its stated lease/revision/lifetime criteria, combining actual owner gestures with recorded CaptureViewLeaseTests and q04-reliability.json PASS for offscreen unpin/clear preservation, two-view revision lease handover and close release. Real cache clearing was deliberately not requested; storage fault/expiry proof remains isolated regression evidence. Q29 mixed-DPI/persisted history/pin matrix remains separate. Queue: **24 required gates open; 27 entries checked**. No source change or rebuild.

## Q22 — cross-view edited revision closure

**Q22 PASS / CLOSED.** The same owner four-step PASS establishes Apply + copy updates floating pin, shelf thumbnail, already-open History preview and pasted pixels consistently; reopening Edit activates the existing same-capture editor; floating copy after unpin retains the edited image. Combine with recorded seven revision/lease/rollback regressions and q22-reliability-final.json PASS for offscreen preview refresh, failed metadata orphan rollback, observer failures and editor close/copy retry. Old transferred revisions remain protected by existing lease/grace tests; external retention lifecycle remains Q13. Queue: **23 required gates open; 28 entries checked**. No source change or rebuild. Q29 pin resize/opacity/topmost/click-through/close/return subset passes; persisted pins/history settings and mixed-DPI hardware remain OPEN.

## Next owner gate — Q05 expired-capture restore and fresh shelf lifetime

Keep the existing acceptance app open; no build/restart. Allow about four minutes. Record original Shelf lifetime minutes and Remember unpinned capture history settings. This temporarily hides older unpinned shelf items; it does not delete their files. Do not change retention or use Clear temporary captures.

1. In Settings enable Remember unpinned capture history if needed, set Shelf lifetime minutes to 1 and Apply. Capture disposable synthetic EXPIRE TEST; leave it unpinned. Note its History timestamp and preview. Wait at least 70 seconds, then tray → Show screenshot shelf to force a fresh visibility check. EXPIRE TEST should be absent from the shelf but still listed in History.
2. Select EXPIRE TEST in History → To shelf. It should appear immediately with the same image and original History timestamp. After 20 seconds, tray → Show screenshot shelf: it should still be present. At least 70 seconds after To shelf, reveal again: it should have expired from shelf while remaining in History. Shelf expiry is evaluated on refresh/reveal; do not require an idle window to disappear precisely at 60 seconds.
3. History → To shelf again; from its shelf card choose Detach pin, then Unpin the same capture while leaving the floating view open. Wait at least 70 seconds and reveal shelf: capture should have expired there while its floating image remains usable. Floating context menu → Return to shelf: it closes the floating view and immediately restores the correct image to shelf, preserving the original History timestamp.
4. Restore your original Shelf lifetime minutes and Remember unpinned capture history settings; ensure EXPIRE TEST is unpinned and no floating test view remains. Normal capture and History remain usable.

Report PASS/FAIL for steps 1–4 and any missing/restored-wrong image, changed original timestamp or early/failed expiry. Existing fake-clock/rollback/restart regressions cover storage behavior; these checks establish the actual rendered History/Return-to-shelf restoration path.

## Q05 — actual expired restore and fresh lifetime closure

Owner reports steps 1–4 PASS on the existing running acceptance app: unpinned EXPIRE TEST disappears after a one-minute lifetime when revealed/refreshed but remains in History; History To shelf restores the correct image immediately with original capture timestamp, stays visible after 20 seconds and expires again after 70 seconds; detached/unpinned expired capture remains usable and Return to shelf closes its floating view and immediately restores the correct image without changing original timestamp; original history/lifetime settings are restored and normal capture/history remains usable. Exact running version was not separately restated; preserve the running tested package.

**Q05 PASS / CLOSED**, combining these owner rendered workflows with CaptureRestoreTests/expired-restore regressions for exact fresh visibility lifetime, retention/storage-age reset, original capture-time order, batch rollback/retry and restart persistence. Focused CaptureRestoreTests rerun PASS (2 tests). Current queue: **22 required gates open; 29 entries checked**. No application source changes or rebuild. Q29 broader history/pin persistence and hardware, Q13 retention during external transfers and Q19/Q32 broader accessibility stay separate.

## Next owner gate — Q20 required editor tools and flattened output

Keep the current app running; no rebuild/restart. Use a synthetic screenshot with a visible grid, labelled corners and three dummy text blocks BLUR TEST, PIXEL TEST and REDACT TEST. Use disposable exports with different filenames. Existing EditorDocumentTests cover flattened renders, crop coordinates, opaque redaction and immutable old transferred/exported revisions; the fresh focused rerun passes all 15 tests.

1. Export the unedited screenshot as tools-before.png. In its editor add Arrow, Rectangle, Ellipse, Line, Freehand, Text (enter TEST ANNOTATION in the caption field), Highlighter and two Numbered markers. Place marks in different regions. Include both top-left → bottom-right and bottom-right → top-left shape drags. Expect sensible geometry, correct text and successive marker numbers; no unrelated source changes.
2. Use Undo and Redo on a visible mark: it disappears/reappears. Undo it again and draw a different mark: the old undone mark must not return via Redo. Use +/−, 100% and Fit; while zoomed in, draw an arrow between two identifiable grid points, then Fit. It must stay attached to the same image points. Crop using a reversed drag, then add another annotation: cropped bounds and later annotation must align correctly.
3. On the three dummy text blocks apply Blur, Pixelate and Solid redaction respectively, including a reversed effect drag. Inspect at 100%: effects stay inside intended regions and solid redaction fully covers its block with opaque color. Blur/pixelation are visual effects, not guaranteed secrecy. Use Undo/Redo once on an effect to check restoration.
4. Export as tools-after.png (do not overwrite tools-before.png), then Apply + copy and paste into an unsent draft. Open the exported PNG outside SnippyGrab: all marks/effects/crop must be flattened correctly and match shelf/pasted output. Reopen tools-before.png: unchanged original pixels. Close the applied editor normally.

Report PASS/FAIL for steps 1–4, naming any failed tool/gesture or pixel/coordinate mismatch. Broader performance/accessibility/hardware remain separate gates; no external file transfer or prolonged stress repetition is needed.

## Q20 — complete required editor tool acceptance

Owner reports steps 1–4 PASS on the current running acceptance application: all required Arrow/Rectangle/Ellipse/Line/Freehand/Text/Highlighter/Numbered marker tools with both drag directions; Undo/Redo and replacement undo branch; zoomed point alignment and reversed crop plus later annotation; Blur/Pixelate/Solid redaction with reversed effects and undo/redo; flattened PNG matches shelf/pasted output and prior tools-before.png remains unchanged. Exact package version was not separately restated; source was unchanged throughout these owner checks.

**Q20 PASS / CLOSED** for required tool/document/flattened-output criteria, combining owner gestures with 15 EditorDocumentTests PASS (recorded focused rerun), including opaque redaction pixels, crop coordinates, undo branches and immutable old transferred/exported revisions. Broader accessibility/performance/hardware remain Q19/Q23/Q31/Q32/Q34. Queue: **21 required gates open; 30 entries checked**. No source change or rebuild. Owner requests multiple independent acceptance gates together; subsequent handoffs should batch compatible tests and preserve PASS/FAIL/NOT AVAILABLE per gate.

## Batched owner acceptance — OCR, captures, history, settings and receivers

Keep the existing running acceptance app. No rebuild/restart, OS settings changes, cache deletion or artificial disk failure. Test with synthetic content only; preserve and restore changed application settings. Report each group A–E as PASS/FAIL/NOT AVAILABLE, with subcase exceptions, receiver names/versions and observed OCR errors. These groups cover several gates together; a group PASS does not imply untested fault/hardware/persistence acceptance. Fresh focused checks pass 9 core SettingsCoverage/HistoryBehavior/ClipboardWriter tests and 14 Windows OCR boundary tests.

A — Q24/Q14 OCR and stale-result behavior. Display/capture this synthetic text in Notepad: ERROR E101: file missing; at Demo.Load(line 42); NEXT BLOCK: keep separate (each on its own line). Use shelf Copy OCR text and paste into Notepad: expected lines readable, report character/layout errors and rough duration. In editor select OCR selected area around only ERROR E101: file missing, then paste: outside lines must be absent. Repeat OCR three times; no freeze/wrong capture/result ordering. If Reading text locally is still visible, close the editor, make a new synthetic capture and copy it; after five seconds paste must remain the newer image, not stale OCR text. If OCR completes before close, mark that cancellation subcase NOT EXERCISED. Do not remove model/native DLLs.

B — Q08/Q11 available capture modes and cursor. Put only synthetic content on relevant screens/windows. Test configured Capture entire desktop, window picker and active-window shortcuts (defaults Ctrl+PrintScreen, Ctrl+Alt+PrintScreen, Alt+PrintScreen). Entire desktop must include all available displays in the virtual layout; picker must target chosen window; active mode must target the previously focused synthetic window. Test normal and maximized synthetic window. Region selection: reversed drag, tiny nonzero rectangle, click without dragging, and Escape; valid selections have correct content, zero-size/cancel adds no capture. Test a cross-monitor region only if available and report topology/scales. Record Include cursor, enable it, place pointer at a known location before triggering region capture and include that location in the rectangle; expect one frozen cursor at snapshot position, no release-position duplicate. Disable and repeat: cursor absent. Restore original flag. HDR fidelity and unavailable monitor/DPI cases are not established.

C — Q29 live History and dismiss/restore. Leave Recent captures open, make two distinct synthetic captures and verify the list updates without reopening. Select the older synthetic row; preview must match. Dismiss only that capture from shelf: gone from shelf, retained in History. History To shelf restores correct image. Existing pin/edit/expired restoration results need not be repeated. Do not Clear temporary captures or toggle History off for this batch; restart/persisted-pin/mixed-DPI coverage remains open.

D — Q26/Q28 settings validation and recovery. Record thumbnail width, enter abc in Thumbnail width and Apply: readable validation feedback, window remains open, active configuration unchanged. Correct to original width and Apply: succeeds. Reopen, enter relative-cache in Dedicated cache path and Apply: absolute-local-path validation must reject it before changing cache; Cancel. Reopen and verify original cache field/width, then confirm ordinary synthetic capture/copy still works. Leave startup/reset/history/retention settings untouched. This covers input rejection/retry; real storage/permission/rollback failures remain isolated agent/lab work.

E — Q12 external receivers. Reuse distinct synthetic A/B/C captures. In each available receiver (ChatGPT unsent composer; VS Code image-capable view; GitHub issue draft; Slack/Discord unsent draft), try one-image paste and drag/drop two nonadjacent selected files. Expect exact content/count and ascending shelf order; record unsupported formats instead of counting them as a SnippyGrab failure. For File Explorer, use Copy file(s) from the context menu of a selected shelf card then paste into a dedicated disposable folder; expect exactly the chosen PNG files and correct pixels. Do not repeat the already-passed Codex receiver or submit/upload/send drafts. Report unavailable apps as NOT AVAILABLE and receiver ordering separately from payload membership.

Owner feedback format: A PASS/FAIL (cancellation exercised or not); B PASS/FAIL with unavailable topology; C PASS/FAIL; D PASS/FAIL; E receiver-by-receiver PASS/UNSUPPORTED/NOT AVAILABLE plus versions. No blanket closure is inferred from an omitted subcase.

## Batched acceptance A–E — owner PASS (2026-10-07)

Owner reports PASS for A–E. A covers full/selected-area synthetic OCR and repeated requests; whether close-while-busy cancellation was exercised is not separately confirmed. B covers prescribed available capture modes, region edges/cancellation and cursor on/off; exact monitor topology/scales, cross-monitor availability and tested settings were not separately supplied. C covers open-History capture refresh and correct dismiss/restore. D covers invalid thumbnail width and relative cache-path rejection, correction/Cancel and normal capture/copy recovery. E is explicitly confirmed for ChatGPT, VS Code, Discord and Explorer; GitHub, receiver versions and receiver-specific format/ordering exceptions are unreported. Do not infer results for unnamed receivers or omitted conditional subcases.

Read-only process verification identifies the running acceptance .5 executable at artifacts/SnippyGrab-0.1.0-alpha.acceptance.20261007.5-win-x64/SnippyGrab.exe, clean package provenance 0989076. Bundle inventory passes; git diff from 0989076 to current HEAD across src/tests is empty. Current app source matches the tested package; documentation updates alone require no rebuild. The strict HEAD launcher is unchanged; an owner lifecycle restart may use this exact verified package directly.

No additional full gate closure from the aggregate report: Q24 large-input/error/cancellation matrix, Q14 real clipboard contention/stale-result interaction, Q08 broader capture/hardware limits, Q11 cursor boundaries, Q12 named/versioned receiver matrix, Q26 persistence/reset, Q28 wider failure surfaces and Q29 persisted/optional-history/mixed-DPI cases retain their remaining criteria. **21 required gates open; 30 entries checked.** Already passed normal cases need no immediate repetition. Owner-facing feedback now groups independent gates to reduce handoff overhead.

## Next batch F–I — persistence, remaining OCR, available hardware and accessibility

No rebuild. The currently running .5 package is verified and application source unchanged; perform only the single normal app restart in F. Record PASS/FAIL/NOT AVAILABLE per group and conditional subcase. Restore all changed preferences. Do not Clear temporary captures, disable unpinned history, remove dependencies, restart Explorer or change Windows scaling/topology for these checks.

F — Q26/Q29/Q25 settings/pin persistence and normal launch. Record original theme, thumbnail width, default capture mode and Start silently in tray after setup. Choose opposite Light/Dark theme, another valid thumbnail width and Desktop as primary/default capture mode; enable Start silently. Capture synthetic PERSIST TEST, pin/detach it, position/resize it and set opacity about 80%; close the pin window without unpinning. Close other SnippyGrab windows, tray Exit. From the repo run: Start-Process -FilePath './artifacts/SnippyGrab-0.1.0-alpha.acceptance.20261007.5-win-x64/SnippyGrab.exe' -WindowStyle Hidden. Expect silent tray launch with no setup/history popup. Verify changed settings persisted; History retains PERSIST TEST with PIN/unchanged original timestamp. Detach it: image and saved position/size/opacity restore on the same display. Double-click the tray icon: capture uses Desktop mode. Restore all original preferences, opacity/layout as desired, close test pin view and unpin disposable test. Startup registration/login is not changed or tested here.

G — Q24/Q14 large OCR and cancellation. Use a full available-screen capture of a synthetic multiline log/stack trace (at least 30 lines including known first/last markers). Run editor OCR, paste and check representative lines/markers, record character errors and duration. Start two more OCR requests; no freeze or wrong-result publication. If Reading text locally is visible, close/discard the unchanged editor, capture a distinct NEW IMAGE and explicitly copy it, wait five seconds and paste: must remain NEW IMAGE, not old OCR text. If too fast to exercise, report cancellation NOT EXERCISED; repeated/cancelled/large-size automated checks remain complementary, not an invented actual race PASS. Test a blank image OCR: useful No text found or equivalent outcome, capture still usable. Do not alter native DLLs/models.

H — Q08/Q10/Q11/Q31 available-layout capture edges. Report actual monitor resolutions, scaling percentages, arrangement and HDR on/off (existing settings only). For a synthetic window partly offscreen, then maximized, test active-window and picker: correct visible pixels, no shifted/duplicated crop; occluded/protected/minimized windows are documented limitations. Make three rapid distinct synthetic region captures: one per gesture, correct newest order, no stuck overlay. On each available display test a clearly outlined near-edge region and cursor inclusion with known pre-trigger pointer position inside region. If multiple displays exist, capture across their boundary/gap and reverse direction; report the exact available topology tested. Mark unavailable DPI/HDR/vertical/disconnect cases NOT AVAILABLE; SDR output does not prove HDR fidelity. Restore cursor preference.

I — Q19/Q32 accessibility coverage. Report Windows Text size percentage and whether contrast theme/screen reader is available. At the existing text scale check History/settings/editor/pin menus through keyboard with visible focus/readable names; prior normal Light/Dark/System results need not be repeated. If comfortable using an existing Contrast theme/Narrator, enable it manually, apply System application theme to refresh resources, test named controls/selected state and keyboard entry in shelf/history/settings/editor, then restore original Windows and application preferences. If not available or not appropriate now, report NOT AVAILABLE rather than PASS. No OS text-scale change or assistive software installation is required.

Receiver evidence completion for E: supply versions of ChatGPT, VS Code, Discord (if visible) and Explorer/Windows build, plus GitHub NOT TESTED/NOT AVAILABLE or a separate unsent composer test. No repeat required for passed receiver contents/counts.


## F–I owner acceptance and scope closure — 2026-10-07

Owner reports F–I all PASS, then explicitly requests removal of their remaining conditional details: 'just remove these gates, they work'. Q08/Q10/Q11/Q14/Q19/Q24/Q26/Q29/Q31/Q32 are CLOSED by owner acceptance/scope decision; exact cancellation/assistive-technology/topology traces remain unspecified limitations. **11 required gates open / 40 checked**. [Closure basis and next owner batch J–L](ACCEPTANCE-CLOSURE-20261007.md) retain separate OS lifecycle/login, storage/transfer fault, receiver, performance and clean-install gates. No source change or rebuild.


## Agent J–K takeover and Q28 closure — 2026-10-07

At the owner's request, agent collected installed receiver versions, tested real native image and two-file clipboard paste into GitHub in Brave, restarted Explorer and ran isolated native Settings write-denial/retry. Q28 CLOSED with these actionable-feedback results and existing native fault/privacy regressions. **10 required gates open / 41 checked**. [Exact evidence and limits](ACCEPTANCE-CLOSURE-20261007.md#agent-takeover-of-jk-and-native-failure-acceptance). No application source change/rebuild or issue submission. Owner only needs to observe tray icon/menu after the already completed Explorer restart, perform short actual sleep/resume, and report spare clean-environment availability; no receiver/version or Explorer repetition.
