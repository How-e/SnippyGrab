# Targeted acceptance continuation — 2026-10-07

## Q15 — shelf focus and selected keyboard actions

PASS / CLOSED for Q15. Broader accessibility remains Q19/Q32; external receivers remain Q12/Q30/Q50.

Windows 11 build 26300, development Release source `5213acb`, zero-warning warnings-as-errors build. Actual Windows Computer Use input targeted only the isolated interaction fixture. Its synthetic CAPTURE 01–20 images, intercepted clipboard and disabled hotkeys left the owner's installed instance and normal cache/settings unchanged.

- Fixture **Focus shelf** acquired visible keyboard focus without opening an editor. End reached CAPTURE 20, Space displayed **Focus · selected**, and Ctrl+C recorded an image copy.
- Home retained the offscreen selection; Space selected CAPTURE 01 as well. Ctrl+C recorded a file payload. End returned to CAPTURE 20 with selection intact; Enter opened the rendered CAPTURE 20 editor, rather than the unrelated primary image.
- After closing, clicking the numbered 20/20 badge visibly restored **Focus · selected** without opening an editor. Tab reached the badge and then Edit; Enter invoked that toolbar button and reopened CAPTURE 20.
- Shelf Ctrl+S opened the native export dialog with CAPTURE 20's remembered filename/destination. Cancel left its existing output intact.
- The owner's current-build test already passed Delete and the prescribed focus/keyboard workflow, recorded in USER-TESTING. The earlier independent native Delete check dismissed only the selected nonprimary record. Those observations supplement five selection regressions and the 64-layout command-routing probe; Delete was not repeated in this continuation.
- Focus/composer preservation on real capture retains the recorded owner U01 pass. This fixture does not establish production receiver delivery, screen-reader support or the wider theme/hardware matrix.

Focused tests: 11 core ShelfSelection/CaptureExport and 15 Windows EditorDocument tests PASS, no failures/skips. Local ignored final report `artifacts/acceptance-results/keyboard-final-20261007.json`, SHA-256 `D7517A41495331DD296BBF19304B31A814439D48DD046BB4D355A203DAF56CDE`. The report confirms copy formats and capture flags; it does not record image identity or selected file membership. Rendered CAPTURE labels, source routing and existing independent membership/order tests supply those complementary checks; no stronger report claim is made.

Some automation clicks initially rejected stale bounds/index/screenshot references. Fresh window selection plus activation recovered input; rejected actions were not counted as passes.

During editor checks, drawing left focus in the color input and suppressed Ctrl+S. That separate editor focus defect is tracked under Q19; Q15's shelf shortcut/selection routing passed.

## Q19 — canvas shortcut remediation

FIXED / native regression PASS; Q19 remains OPEN for the complete accessibility matrix.

Reproduced on `5213acb`: focus remained in the color TextBox after an actual arrow gesture, and Ctrl+S did not open Export PNG. The editor intentionally preserves text-editing shortcuts when a TextBox owns focus. The canvas was already focusable but did not explicitly acquire focus during its mouse-down handler.

The handler now focuses the canvas when an allowed gesture begins. Text fields retain their own shortcut handling while the user edits them; document gestures transfer input to the canvas. No pointer-position or clipboard behavior changed.

Windows native verification on the rebuilt Release executable, `4753123` plus this one-line focus change: click the color field, draw an arrow, Ctrl+Z visibly removes it, Ctrl+Y visibly restores it, Ctrl+S opens the actual native export dialog. Its saved PNG contains the annotation. This establishes executed pointer/keyboard behavior, not merely raised routed events. UI Automation continued to report a stale color-field focus string, so the actual shortcut outcomes and rendered pixels are the acceptance evidence.

Local fixture report: `artifacts/acceptance-results/editor-focus-20261007.json`. Broader keyboard-only flows, screen-reader/high-contrast/text-scale/theme acceptance stay under Q19/Q32.

## Q21 — explicit export and managed-copy behavior

PASS / CLOSED for Q21. Windows native gestures/dialogs on the same isolated fixture supplement six passing CaptureExport tests, flattened redaction/export tests and existing real clipboard evidence under Q02. Broader editor tools, receiver and accessibility matrices remain separate.

- Initial Export PNG opens the configured isolated output directory. Native Cancel reports cancellation, leaves Exported false and keeps Open export folder disabled. No PNG is created.
- A drawn arrow exports successfully; full destination is visible, the folder button becomes enabled, and Explorer opens that exact directory containing the PNG. The export hash equals the current managed PNG hash.
- Add a second arrow and export again: the dialog remembers the earlier filename/directory. Native overwrite No preserves the original SHA-256 `20AF908F1B86C8BBD86CDB08175856F2E51D863677BE2D15C4D46A77847305D8`. Native overwrite Yes writes the updated revision, hash `48A762B675E87724C9A1E99CFAEBC75E25FD0C8DA8488F326946A73397D344CD`, exactly matching the managed PNG. Only disposable fixture output is overwritten.
- Both exports leave the intercepted clipboard untouched (zero operations). Native Apply + copy then records exactly one image publication and displays the managed-copy explanation; export hash remains unchanged. Actual OS clipboard publication/retry already passed Q02; this fixture proves action routing and file semantics, not receiver delivery.
- Six current core export tests cover repeat/stale revision rejection, destination conflict/cache restrictions, failure preservation/retry and metadata-warning behavior. Native Cancel, decline/accept overwrite and folder opening complete the previously missing dialog acceptance.

Local ignored evidence: `export-final-20261007.json` SHA-256 `52115D5DC090D629AFEADE8B433B8660636A81B4C7E9917404C722EC93258396`; `export-verification-20261007.json` SHA-256 `485DD133872195D6B3121BF752342344C85A4839CFE6D09A570E128DC4766E04`, both under `artifacts/acceptance-results/`. No private pixels or raw user exception payloads are tracked.

Validation after the focus fix: zero-warning warnings-as-errors Release build, full **170 core + 61 Windows tests**, no failures/skips, full formatting PASS. Queue and milestone/release policy checks retain stable blocking with **30 required gates open; 21 entries checked**.

## Owner receiver handoff — completed after native checks

Q30/Q50: exact nonadjacent receiver membership/order and one-minute delayed file paste, using the new local `0.1.0-alpha.acceptance.20261007.2` candidate. The launcher verifies committed source and matching bundle hashes/revision. Exit all installed/fixture SnippyGrab instances normally first, then run `pwsh ./scripts/start-acceptance.ps1 -RealApp`. Follow USER-TESTING's targeted receiver instructions; use only synthetic A/B/C content and unsent drafts. Record candidate and Codex version, count/content/displayed order and supported/unsupported separately for each case. Earlier ordinary paste/drop successes stand.

Q16/Q49 remaining gesture/settings coverage, Q18 motion/contrast, Q19 accessibility and later storage/history/OS/hardware gates stay open. This continuation closes only evidenced Q15/Q21 and fixes the independently reproduced canvas shortcut issue; it does not waive the other required acceptance criteria.

## Q50 — real Codex transfer-order acceptance

PASS / CLOSED. Owner reports **Codex 0.160.1, steps 2–4 all PASS** on the preceding handoff's `0.1.0-alpha.acceptance.20261007.2` candidate (clean source `06f4c9a`, verified bundle provenance). Nonadjacent C/A drop, reordered A/C drop and one-minute delayed selected-file paste satisfy the prescribed receiver count/content/order checks. Existing independent reverse/nonadjacent/duplicate/reordered/restart/scrolling payload checks establish SnippyGrab's ascending-current-shelf-number contract.

The owner reported PASS, without a separate exact order trace; retain that evidence level. This is actual user-reported receiver acceptance, not synthetic receiver or agent-observed attachment evidence. Other receivers remain Q12; native drag/retention/crash and longer delayed reads remain Q13. The one-minute case does not prove a receiver defers reads after first ingesting files.

## Q30 — primary Codex acceptance

PASS / CLOSED. Exact candidate and receiver identity plus all targeted real-receiver steps 2–4 PASS now complete the remaining count/content/nonadjacent/reordered/delayed-paste gaps. Earlier owner U01 preserved-focus/latest-image/Esc/zero-selection passes and ordinary single-file drop remain valid complementary observations. Q50's contract and independent regressions are closed above. This is the tested Codex 0.160.1 result; compatibility with future versions is not implied.

Current queue: **28 required gates open; 23 entries checked**. Stable packaging remains blocked. No application source changed while recording these owner results.

## Completed owner check — reverse nonadjacent insertion under Q16

Use the currently running tested `.2` application; no receiver repetition or rebuild is needed for this check. With the preceding test's shelf order B,A,C, clear selection and Alt-drag C from slot 3 onto slot 1: expect C,B,A. Alt-drag C back from slot 1 onto slot 3: expect B,A,C. Cards between the source and target shift; items do not swap. No editor should open during either drag. Then select shelf 1 and 3, leave/re-enter three times and confirm both remain selected and reachable without jitter or premature collapse. Report PASS/FAIL per direction and for leave/re-entry. If captures have changed, create fresh synthetic A then B then C and identify the images rather than relying on old numbers.

This resolves the specifically unreported reverse-nonadjacent gesture. Q16's broader release-outside-card, drag-threshold/scrolling/settings coverage and Q49's full cold/warm placement matrix are not automatically closed by this short check.

Owner subsequently reports **steps 1–3 PASS**: both C insertion directions, no accidental editor, selection preservation/reachability and three jitter-free leave/re-entry cycles. This closes that acceptance subset. Q16 remains OPEN for the remaining native cancellation/release/threshold/scrolling/settings edge cases. No source changed and the current count remains **28 required gates open; 23 entries checked**. Next test procedure is recorded in USER-TESTING under reverse reorder acceptance.

## Q16 — cancellation, outside release and scrolling closure

Owner reports the next **steps 1–3 PASS** on the same tested `.2` build: Escape cancels Alt-reorder without changing order/opening an editor; releasing Alt-drag over empty desktop does not reorder/open an editor/stick the dock; ten-plus captures retain two selections across scrolling/leave/re-entry, and later-item insertion works without accidental editing.

**PASS / CLOSED for Q16.** Combine these with recorded actual click/edit behavior, adjacent/nonadjacent reorder both ways, hover reachability and selection-collapse passes, Q15 native keyboard acceptance and independent production pointer-capture/drag-threshold/64-layout/selection/cache regressions. The formerly listed settings matrix is retained under Q18/Q19/Q32/Q49 rather than expanding Q16's stated interaction criteria into a new all-settings requirement. This closes no broader motion/accessibility/hardware gate.

Current queue: **27 required gates open; 24 entries checked**. Next owner check is the previously unverified alternate orientation/corner, thumbnail sizes and application animation settings under Q49/Q18, recorded in USER-TESTING. No application source changed for this acceptance update.

## Q49 — actual dock settings and hover closure

**PASS / CLOSED.** Owner reports all prescribed settings checks PASS on local `.3`, clean source `9103b9d`: alternate orientation/opposite corner, every visible card/control reachable, scrolling, five leave/re-entry cycles with/without selection, smallest/largest thumbnails with animation setting off/on, and original settings restored. No jump/freeze/premature collapse/lost selection was reported. Numeric settings and named corner were not separately supplied; the report is a PASS against the prescribed relative configurations.

This completes the earlier 1/3/5/20 cold/warm owner hover evidence, Q16 native selection/reorder/cancellation passes and independent 64-layout/768-sample anchor/cache/routing checks. Historic failed pointer/foreground-preservation probes remain failed records; later structural/full probe passes and actual owner observations supply complementary evidence. No broader hardware/accessibility or actual new-capture fade claim is inferred from toggling animations during hover.

Current queue: **26 required gates open; 25 entries checked**. Q18 new-capture fade/clipping/contrast and reduced-motion/high-contrast acceptance remain separate.

## Q18 — bounded thumbnails and hover/settings acceptance

Owner's `.3` all-steps PASS verifies repeat hover/re-entry, reachability and smallest/largest-thumbnail behavior with the application's animation flag both off and on. Existing independent cache/revision/scrolling regressions bound five card views/twelve thumbnails and require zero warm card creation/decoding. Combined with Q49/Q16 closure, this completes the bounded-work and stable-hover subset.

Q18 remains OPEN for actual arrival fade, visible rounded clipping/contrast, and native reduced-motion/high-contrast settings. Source inspection confirms **Fade new captures into the shelf** affects arrival, not hover expansion: it fades only on a new arrival when application Animate and Windows ClientAreaAnimation are enabled and HighContrast is false. The owner's hover tests toggled that setting with existing captures; no new-capture animation observation was requested. Do not claim that outcome from the reported PASS. Expansion animation remains deferred by the existing design; this pass adds no new animation requirement.

Next owner check: in the running tested app, turn **Fade new captures into the shelf** off, capture one synthetic image and expect immediate stable appearance. Turn it on, capture another, and expect a brief stable arrival fade if Windows animations are enabled, otherwise immediate appearance. In Light and Dark application themes, inspect smallest/largest thumbnail sizes: screenshot edges stay inside rounded cards, badges/selection/focus/buttons remain readable, and no controls are clipped or obscured. Restore settings. Report PASS/FAIL for arrival off/on and each theme, including whether fade was visible. If already using Windows reduced motion/high contrast, report that existing setting; do not change OS preferences for this test. Broader assistive-technology/OS-theme acceptance stays Q19/Q32.

Owner subsequently reports **all prescribed visual checks PASS**. The chat procedure used the read-only observed OS state ClientAreaAnimation=false/HighContrast=false, expecting immediate stable arrivals with the application animation toggle off/on. Light/Dark at smallest/largest thumbnails also pass rounded clipping and readable/unclipped controls. Owner did not distinguish running `.3` from the handoff's rebuilt `.4`; application source is identical in those candidates.

**Q18 PASS / CLOSED for the stated cache/transition/presentation criteria** with the current visual report, earlier actual hover/settings passes, Q49/Q16 closure, five-card/twelve-thumbnail bounds and revision/warm-reuse regressions. Normal-animation-enabled fade, high-contrast and assistive-technology native coverage are not established and remain broader Q19/Q32 matrix limitations. Current queue: **25 required gates open; 26 entries checked**. Stable remains blocked; no source changed for this closure.

## Q19 — hotkey-field keyboard navigation correction

An independent native isolated fixture reproduced Tab changing Primary hotkey from PrintScreen to VK 9 while focus stayed trapped. Settings now reserves unmodified Tab and Shift+Tab for normal WPF focus navigation and explains it in the hotkey guidance. Actual rendered checks on the rebuilt Release executable passed Primary hotkey → capture-mode combo → Entire desktop hotkey → Window picker hotkey traversal, reverse navigation, unchanged existing bindings, and Ctrl+Shift+K assignment in an unsaved draft. The accessibility focus string stayed stale; rendered focus indicators and actual binding changes supplied the evidence. Cancel discarded the draft and the isolated fixture exited. Normal user settings were untouched.

Validation: zero-warning/error Release build; 170 core + 61 Windows tests PASS; formatting verification PASS. Q19 remains OPEN for broader keyboard-only flows, focus visibility, text scaling, high contrast and assistive technology. Next owner procedure is the Q19 keyboard pass in USER-TESTING. Queue remains 25 required gates open / 26 checked.

## Q19/Q32 — owner keyboard and theme subset PASS (2026-10-07)

Owner reports PASS for all four prescribed steps on the handoff's acceptance `.5` build (source `0989076`): Settings Tab/Shift+Tab through all hotkey fields and controls without changing bindings, reversible checkbox navigation and Cancel; History arrow selection/matching preview and keyboard Edit; editor tool/field/zoom/action traversal, keyboard export cancellation and unchanged close; readable focus/labels/actions in Light/Dark at the current Windows text scale, with theme restored. This is owner-reported real interaction, distinct from the earlier isolated native agent checks.

No separate high-contrast or screen-reader result was supplied. System theme, keyboard capture/copy/export/pin end-to-end and the wider text-scale/hardware/assistive-technology matrix remain unverified. Q19 and Q32 stay OPEN; queue remains 25 required gates open / 26 checked. Next owner check targets the remaining shelf copy/export/pin keyboard workflow and System theme. Do not repeat the four successful checks solely for this documentation change.

## Q19/Q32 — shelf copy/export/pin and System theme PASS (2026-10-07)

Owner reports steps 1–3 PASS: keyboard copy uses selected A rather than newer B; Ctrl+P pin state is visible in History and can be restored; shelf Ctrl+S exports the focused A and repeat-export cancellation creates no extra file/false success; System theme has readable contrast, focus/state cues and reachable unclipped shelf/Settings/History/Editor controls at the current text scale, with original theme restored. The existing acceptance application remains running; owner explicitly requests no further build unless necessary. Exact running version was not separately restated; the handoff named `.6`, whose application source is identical to `.5`.

Q19/Q32 remain OPEN for the unverified keyboard global-capture path and broader text-scale/high-contrast/assistive-technology coverage. High contrast/screen-reader availability was not separately reported. This closes the prescribed workflow/theme subset, not the complete gates. Current queue is **25 required gates open; 26 entries checked**. No application source change or package rebuild is needed. Use the existing running build for the next checks; the strict current-HEAD launcher will require a fresh matching package if a future restart is needed after these documentation commits.

Next owner priority: Q09 actionable fallback/help, actual Windows global fallback delivery/cancel/capture, pause preserved across Settings, held-key suppression and tray fallback. Procedure: USER-TESTING next owner Q09 gate. Use the existing running build; no source change.

## Q09/Q19 — actual fallback, pause and held-key subset PASS (2026-10-07)

Owner reports steps 1–3 PASS on the existing running acceptance application: complete Hotkey help / conflicts guidance; fallback Escape cancellation with no capture; fallback synthetic region produces exactly one correct capture and shelf copy/paste delivers it; Pause prevents SnippyGrab capture and is preserved across Settings Cancel; resumed fallback works; holding the combination produces one overlay with no queued repeats/stuck keys; tray Capture region alternative works and cancels. This establishes the actual global-keyboard entry/copy path alongside prior shelf/editor/history/settings acceptance. Exact configured combination/version was not separately restated; the prescribed fallback is default Ctrl+Shift+S unless changed.

Q09 stays OPEN: actual other-owner/fallback conflict, first-run recovery with Print Screen unavailable and the full interception/reconfiguration cases were not exercised by these steps. Earlier owner Windows Print Screen prerequisite and injected registration/pause/no-repeat regressions remain complementary. Q19/Q32 broader high-contrast/text-scale/assistive-technology coverage remains unverified; no separate availability report was supplied. **25 required gates open; 26 entries checked.** No rebuild or source change; preserve the current running acceptance application. Documentation commits do not invalidate its runtime evidence.

Next owner priority: Q04/Q22 actual detached-pin gestures, edit revision propagation across an already-open History view/shelf/pin/clipboard, unpin while open and Return to shelf. Q29 click-through recovery/close subset included. Procedure is in USER-TESTING; retain running build and do not clear real cache.

## Q04 — detached pin lease/revision and actual gesture closure

Owner reports all four prescribed pin steps PASS on the existing running acceptance application: detach/move/resize/opacity/topmost; edited image refresh in detached pin/shelf/open History/pasted output; unpin while floating remains usable and copies edited pixels; Return to shelf closes floating view and reveals image; click-through is recoverable through Restore pins and Close pin window preserves the capture. Exact package version was not separately restated; the prior handoff used .6 (same application source as .5).

**Q04 PASS / CLOSED** for its stated lease/revision/lifetime criteria, combining actual owner gestures with recorded CaptureViewLeaseTests and q04-reliability.json PASS for offscreen unpin/clear preservation, two-view revision lease handover and close release. Real cache clearing was deliberately not requested; storage fault/expiry proof remains isolated regression evidence. Q29 mixed-DPI/persisted history/pin matrix remains separate. Queue: **24 required gates open; 27 entries checked**. No source change or rebuild.

## Q22 — cross-view edited revision closure

**Q22 PASS / CLOSED.** The same owner four-step PASS establishes Apply + copy updates floating pin, shelf thumbnail, already-open History preview and pasted pixels consistently; reopening Edit activates the existing same-capture editor; floating copy after unpin retains the edited image. Combine with recorded seven revision/lease/rollback regressions and q22-reliability-final.json PASS for offscreen preview refresh, failed metadata orphan rollback, observer failures and editor close/copy retry. Old transferred revisions remain protected by existing lease/grace tests; external retention lifecycle remains Q13. Queue: **23 required gates open; 28 entries checked**. No source change or rebuild. Q29 pin resize/opacity/topmost/click-through/close/return subset passes; persisted pins/history settings and mixed-DPI hardware remain OPEN.

Next owner gate: Q05 actual expiry → History To shelf → fresh one-minute lifetime, plus unpinned floating expired capture → Return to shelf. Procedure in USER-TESTING. Preserve running build and restore original settings; no retention/clear changes.

## Q05 — actual expired restore and fresh lifetime closure

Owner reports steps 1–4 PASS on the existing running acceptance app: unpinned EXPIRE TEST disappears after a one-minute lifetime when revealed/refreshed but remains in History; History To shelf restores the correct image immediately with original capture timestamp, stays visible after 20 seconds and expires again after 70 seconds; detached/unpinned expired capture remains usable and Return to shelf closes its floating view and immediately restores the correct image without changing original timestamp; original history/lifetime settings are restored and normal capture/history remains usable. Exact running version was not separately restated; preserve the running tested package.

**Q05 PASS / CLOSED**, combining these owner rendered workflows with CaptureRestoreTests/expired-restore regressions for exact fresh visibility lifetime, retention/storage-age reset, original capture-time order, batch rollback/retry and restart persistence. Focused CaptureRestoreTests rerun PASS (2 tests). Current queue: **22 required gates open; 29 entries checked**. No application source changes or rebuild. Q29 broader history/pin persistence and hardware, Q13 retention during external transfers and Q19/Q32 broader accessibility stay separate.
