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

## Next owner acceptance

Q30/Q50: exact nonadjacent receiver membership/order and one-minute delayed file paste, using the new local `0.1.0-alpha.acceptance.20261007.2` candidate. The launcher verifies committed source and matching bundle hashes/revision. Exit all installed/fixture SnippyGrab instances normally first, then run `pwsh ./scripts/start-acceptance.ps1 -RealApp`. Follow USER-TESTING's targeted receiver instructions; use only synthetic A/B/C content and unsent drafts. Record candidate and Codex version, count/content/displayed order and supported/unsupported separately for each case. Earlier ordinary paste/drop successes stand.

Q16/Q49 remaining gesture/settings coverage, Q18 motion/contrast, Q19 accessibility and later storage/history/OS/hardware gates stay open. This continuation closes only evidenced Q15/Q21 and fixes the independently reproduced canvas shortcut issue; it does not waive the other required acceptance criteria.
