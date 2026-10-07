# Agent gate review — 2026-10-07

Windows x64, .NET SDK 10.0.400. Review starts from clean `ff0db0a` and reads CONTRIBUTING, TASK_QUEUE, MILESTONES, VALIDATION and REMAINING-WORK before edits. No repository or parent AGENTS.md was found. Historical implementation records are retained. Every fixture uses synthetic pixels and isolated temporary directories. Offscreen WPF probes use injected clipboard writes, disabled hotkeys and invisible tray icons; they do not move the pointer, capture the desktop or change OS preferences. They establish programmatic native-window behavior, not actual gestures or external receiver acceptance.

One focused commit per gate, in requested order; Q01 last. Commit identities are available from `git log -- docs/AGENT-GATE-REVIEW-20261007.md` and the final report. Open acceptance is explicit below. No gate is closed merely because its code exists.

## Q02

Inspected: EditorWindow Closing/RequestCloseAsync, EditorCommitCoordinator, AppController ExitAsync and metadata rollback.

Agent work: Added reentrant/cancelled coordinator regressions and actual offscreen editor locked-metadata, clipboard exhaustion and retry checks with an independent second editor. Existing close/Exit coordination retained.

Checks: EditorCommitTests 6/6 PASS; Release solution build zero warnings/errors; --check-reliability q02-reliability.json PASS.

Status / remaining acceptance: OPEN: actual tray Exit with multiple dirty editors, concurrent apply, retry/Discard UI and real OS clipboard lock; isolated injected failures pass.

## Q03

Inspected: CaptureRepository Load/ReadState/ConfirmHistoryRecovery and HistoryWindow explicit OK/Cancel confirmation.

Agent work: Added promotion-write failure after successful sidecar persistence, reviewed unpin survival across restart, retry and idempotent confirmation coverage. Existing conservative recovery retained.

Checks: HistoryRecoveryTests 11/11 PASS, corrupt/truncated/oversized/newer schema across launches and injected promotion failure; git diff --check PASS.

Status / remaining acceptance: OPEN: actual recovery confirmation/cancel dialog, failed promotion feedback and retry interaction using isolated data. Durable recovery and cleanup blocking pass.

## Q49

Inspected: DockWindow expansion/leave timers, fixed selection borders, anchored placement and coalesced render-position dispatch; prior jitter fixes retained.

Agent work: Cold-cache reset for diagnostic runs; 12 temporal primary-anchor samples per layout over three raised hover cycles, recorded counts/orientation/placement/opacity/animation settings and thumbnail/card work.

Checks: DockLayoutTests 15/15 PASS; zero-warning Release build; 64-layout native offscreen dock retry PASS with 768 stable anchor samples. Initial run failed foreground preservation; retained q49-dock.json, successful q49-dock-retry.json.

Status / remaining acceptance: OPEN: visible cold/warm first-hover and re-entry with 1/3/5/20 captures using actual pointer, no jump/freeze, all displayed captures reachable, collapse with/without selections. Desktop idle was not confirmed; no pointer gestures run.

## Q18

Inspected: DockWindow bounded card/thumbnail caches, warm card reuse, rounded clip, arrival-only fade with OS/reduced-motion/high-contrast gates. Expansion animation remains deferred pending actual hover acceptance.

Agent work: Added unrelated-thumbnail preservation during revision invalidation and asserted zero card construction/decoding on warm hover/selection cycles. Added durable structural checkpoint before environmental invariants.

Checks: BoundedCacheTests 4/4 PASS; zero-warning Release build. q18-dock-checkpoint.json.layout.json: all 64 layouts/cache/revision/20-item scroll assertions PASS, 5 cards/12 thumbnails maximum. Full probe FAIL at final pointer-preservation invariant; first run likewise, retained reports, no overall PASS claimed.

Status / remaining acceptance: OPEN: actual visible transitions, clipping/contrast and repeated hover/re-entry; animation/reduced-motion/high-contrast behavior with real settings. Desktop pointer changed during offscreen checks; structural evidence is separate from full probe acceptance.

## Q15

Inspected: ShelfSelection Targets/Update, DockWindow HandleKey/OnKey/Execute, numbered badge/tray focus entry and composer-preserving ShowActivated=false.

Agent work: Added removal/refresh selection regression, unassigned-modifier command rejection and raised-hover no-focus assertions. Current explicit focus and selected-set routing retained.

Checks: ShelfSelectionTests 5/5 PASS; zero-warning Release build; q15-dock.json full 64-layout offscreen command/focus/cache/geometry probe PASS.

Status / remaining acceptance: OPEN: acquire visible native focus through tray or badge, Enter/Delete/Ctrl+C/Export on nonprimary and nonadjacent/scrolled captures, toolbar Tab/Enter behavior and capture preserves composer focus. Hover alone is not keyboard focus.

## Q21

Inspected: EditorWindow ApplyCopy/ExportPng labels/status, AppController Save dialog and CaptureExport guarded atomic write/persistence warning.

Agent work: Reject noncurrent capture objects before export writes, with stale-revision overwrite regression. Added failed repeat-export preservation/retry regression. Path validation precedes identity validation so cache destinations retain actionable input errors.

Checks: CaptureExportTests 6/6 PASS after correcting guard ordering and Windows lock exception expectation; exported redaction integration 1/1 PASS; six-width/text-scale editor layout final PASS; Release build zero warnings/errors.

Status / remaining acceptance: OPEN: configured/remembered destination, actual dialog cancel/overwrite, folder opening and Apply/copy versus export interaction with OS clipboard. Current labels and file/metadata outcomes pass automated checks.

## Q07

Inspected: Capture PNG/storage/metadata salvage, retry-history action, custom-cache fallback, atomic settings save and startup transaction rollback.

Agent work: Extracted the existing post-capture persistence path for synthetic execution and injected repository writer in internal controller. Offscreen probe now executes PNG-failure clipboard salvage and metadata-failure shelf/clipboard preservation, cleanup blocking and recovery retry. Added locked-settings replacement preservation/retry regression.

Checks: StorageFailure/StartupRegistration/ManagedPath tests 13/13 PASS; zero-warning Release build; q07-reliability.json PASS through production persistence/fallback helper using isolated injected writes.

Status / remaining acceptance: OPEN: actual settings/setup failure and retry feedback, custom-cache fallback visibility and real disk/permission failure interaction. No real cache, registry or OS preference was altered.

## Q14

Inspected: ClipboardWriter generation/retry/cancellation, ClipboardService image/PNG/files/text, controller CopyPath/OcrText and editor OCR lifetime.

Agent work: Added independent exhaustion and recovery tests for image, PNG, single/multiple files, path, filename and text; cancellation during retry-delay prevents late publication and permits fresh copy.

Checks: ClipboardWriterTests 3/3 PASS; seven new format-contention cases plus OCR boundaries 21/21 Windows PASS; q14-reliability.json PASS including newer-copy and closed-lifetime stale OCR prevention, actual editor retry with injected clipboard.

Status / remaining acceptance: OPEN: real OS clipboard lock and native OCR/editor close/new-capture interaction; delayed external consumers remain Q13. No OS clipboard writes occurred.

## Q28

Inspected: OperationFailure mapping, controller notice/details/diagnostic log and startup/editor/drag/settings/export failure surfaces.

Agent work: Removed raw exception payloads from five production UI surfaces. Wrapped reflection failures are categorized; aggregate rollback failures cannot be hidden by an initial cancellation, and empty aggregates are handled safely. Added notice/log sentinel privacy and cancellation preservation checks.

Checks: OperationFailureTests 3/3 PASS; zero-warning Release build; q28-reliability.json PASS for category-only diagnostics, useful retry notice, cancellation preservation and actual failed-close feedback; source search finds no remaining ex.Message/error.Message UI publication in App.

Status / remaining acceptance: OPEN: visible dialog/event/tray-details failure UX, truncation/readability and retry guidance in actual settings/export/drag/editor interaction. Automated payload privacy passes.

## Q04

Inspected: PinWindow lifetime/revision subscriptions/Return to shelf and CaptureViewLease acquire-before-release behavior.

Agent work: Added two-view/multiple-revision regression proving one close cannot release the remaining view source and disposed views cannot lease later unrelated revisions. Existing detached pin lease/refresh retained.

Checks: CaptureViewLeaseTests 2/2 PASS; q04-reliability.json PASS for actual offscreen pin/history revision refresh, preview-quality/resize refresh, unpin/clear preservation and close release.

Status / remaining acceptance: OPEN: actual detach, edit, Return to shelf and close gestures; visibility/topmost/click-through and mixed-DPI hardware remain broader pin acceptance. Native programmatic refresh and storage lease invariants pass.
