# Agent gate review — 2026-10-07

Historical 15-gate pass. The later [P0 native acceptance](P0-ACCEPTANCE-20261007.md) closes Q02/Q03 and leaves 32 required gates open, 19 entries checked. Their OPEN statements below describe this earlier pass, not current acceptance.

Windows x64, .NET SDK 10.0.400. Review starts from clean `5355aca` and reads CONTRIBUTING, TASK_QUEUE, MILESTONES, VALIDATION and REMAINING-WORK before edits. No repository or parent AGENTS.md was found. Historical implementation records are retained. Every fixture uses synthetic pixels and isolated temporary directories. Offscreen WPF probes use injected clipboard writes, disabled hotkeys and invisible tray icons; they do not move the pointer, capture the desktop or change OS preferences. They establish programmatic native-window behavior, not actual gestures or external receiver acceptance.

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

Status / remaining acceptance: CLOSED for Q04 after owner detach/edit/unpin/Return/close gestures PASS on 2026-10-07, combined with the isolated lease/clear/revision evidence above. Opacity/topmost/click-through recovery also owner PASS; wider persistence/mixed-DPI hardware stays Q29/Q31. See NEXT-GATE-ACCEPTANCE-20261007.md for closure scope.

## Q05

Inspected: CaptureRepository Restore rollback, CaptureLifetime visibility, retention age and HistoryWindow/PinWindow restore-to-dock calls.

Agent work: Added isolated fake-clock batch-restore failure/restart/retry and repeated-restore regressions, preserving original timestamps and capture-time history order.

Checks: CaptureRestoreTests plus existing expired-restore test 3/3 PASS: immediate visibility, exact 30-minute expiry, storage-age reset, full batch rollback and persistence across restart.

Status / remaining acceptance: OPEN: actual Recent captures Restore and detached Return to shelf interaction shows expired image immediately; verify configured shelf lifetime in the rendered workflow. Data/lifetime behavior passes.

## Q06

Inspected: CaptureRepository full orphan enumeration, lazy bounded PNG dimensions, 512-record content-addressed pages, manifest admission and 24-hour unreferenced-page compaction; HistoryPage bounded UI paging; existing Q35 resource review.

Agent work: Added failed-manifest-replacement test with 1,025 records, retained durable pin state, abandoned-page cleanup and referenced-page preservation across restart. Existing 2,100-orphan/over-4-MB/never-retention and lazy-dimension tests retained.

Checks: HistoryPaging/Recovery/Behavior tests 17/17 PASS with zero warnings after analyzer correction. Existing reviewed Q35 evidence: 7,279 retained captures over 7,201 seconds with bounded handles and managed recovery; not rerun.

Status / remaining acceptance: CLOSED for stated agent criteria: orphan dimensions, bounded pages, failed-write/crash metadata safety, safe compaction and never-retention resource evidence. Repository metadata still scales with retained history in memory; all-day use and history/pin gestures remain broader gates, not claimed verified.

## Q13

Inspected: Lease acquire/release durability, grace after immutable edits, cleanup/session cohorts/staging and README retention/24-hour crash contract.

Agent work: Added dedicated temporary child-process crash fixture and bounded runner: durable transfer is held, revision edited, child terminated without release, restart reads identical old PNG at fake 23h and expires it at 24h while preserving pin. Added nested lease failed-release persistence/retry regression.

Checks: TransferLifecycle/Order/stale staging focused tests 13/13 PASS; zero-warning Release build after correcting helper visibility; scripts/test-transfer-crash.ps1 PASS (q13-crash.json). Only its returned child process was terminated, fixture root validated and removed.

Status / remaining acceptance: OPEN: actual native drag with clear/expiry, OS file clipboard delayed external receiver reads, normal session-only tray exit and process termination during real transfer. Fake-clock/process durability and documented 24-hour tradeoff pass; external receivers must read within grace.

## Q22

Inspected: Optimistic editor revision guard, one-editor-per-capture routing, immutable transferred files, history/pin preview refresh and failed revision rollback.

Agent work: Reproduced failed metadata edit reappearing as a second orphan capture after restart. Rollback now removes only the never-published new revision, preserving original pixels. Separated durable persistence from view notifications so observer failure cannot roll back/delete a committed revision; revision leases refresh before general history notification. Added stale/failed/observer-error regressions.

Checks: Cross-view/failed metadata/view lease/superseded restart focused tests 7/7 PASS (orphan regression failed before fix); zero-warning Release build; q22-reliability-final.json PASS for native offscreen history/pin pixel refresh and actual editor close/copy retry; scoped formatting and git diff --check PASS.

Status / remaining acceptance: OPEN: actual Apply/copy across shelf/history/detached pin and one-editor focus behavior. If the OS refuses deletion of an unpublished revision it is retained conservatively; no data-loss claim is made. Native programmatic consistency passes.

## Q01

Inspected: Foundation-first history, M0-M10 ledger, current gate evidence and ownership, stable package admission and historical/source provenance separation.

Agent work: Reconciled the 15 focused gate records with actual source/tests/commit identities; corrected review starting revision to 5355aca (ff0db0a was an older review). Q06 closes on current regression and existing Q35 evidence; 34 required gates remain open and 17 checked. Added executable milestone coverage/test-file/commit/closure-record verification.

Checks: Locked restore PASS; warnings-as-errors Release build zero warnings/errors; full suite 170 core + 61 Windows PASS, zero failures/skips; full dotnet format verification PASS. Mapping and release-policy results are recorded in final verification below.

Status / remaining acceptance: OPEN: required acceptance gates remain. Implementation and all available local regression work are complete for this pass; interactive, receiver and unavailable hardware/OS criteria retain their exact owners below and in MILESTONES. Stable remains blocked.

## Final verification and commit map

Locked restore, warnings-as-errors Release build, full **170 core + 61 Windows tests**, full formatting and Git whitespace verification PASS, zero warnings/errors/failures/skips. `scripts/test-milestone-map.ps1 -SelfTest` passes the actual 11-row map, referenced test files and implementation commits, 34 open required gates with owners and 15 current closure records; five malformed map/test/owner/premature-completion/record fixtures are rejected. `scripts/test-release-gates.ps1` passes all ten fixtures, with alpha admitted and stable blocked.

| Gate | Focused commit | Current focused result | Acceptance |
|---|---|---|---|
| Q02 | `c439fbc` | 6 coordinator tests; native failed-close/retry probe PASS | Open: multi-editor tray Exit and real clipboard/retry/Discard interaction |
| Q03 | `df63a47` | 11 recovery tests PASS | Open: actual recovery confirmation/cancel/failure/retry UI |
| Q49 | `a2e5a08` | 15 layout tests; 64-layout/768-sample retry PASS | Open: visible cold/warm actual hover; initial foreground invariant failed |
| Q18 | `8364748` | 4 cache tests; 64-layout structural/cache checkpoint PASS | Open: full run failed pointer invariant; visual/motion acceptance remains |
| Q15 | `59b5172` | 5 selection tests; full native dock routing probe PASS | Open: native focus acquisition, actual keyboard/selection/composer workflow |
| Q21 | `b688526` | 6 export tests; redaction decode; six editor layouts PASS | Open: dialogs/cancel/overwrite/folder and clipboard interaction |
| Q07 | `758f7f4` | 13 storage/startup/path tests; production salvage probe PASS | Open: actual settings/storage/cache-fallback feedback and retry |
| Q14 | `33fa43b` | 3 writer tests; 21 format/OCR tests; cancellation probe PASS | Open: real OS clipboard and OCR/editor interaction |
| Q28 | `b3c5623` | 3 failure tests; notice/log privacy probe PASS | Open: visible event/dialog/tray feedback and retry readability |
| Q04 | `7102dc4` | 2 multi-view lease tests; native pin refresh probe PASS | Open: detach/edit/return/close gestures and broader pin hardware acceptance |
| Q05 | `0d37463` | 3 restore/lifetime tests PASS | Open: rendered history Restore and pin Return to shelf workflow |
| Q06 | `53fd2f8` | 17 history/recovery tests; existing Q35 resources PASS | Closed for agent criteria; retained metadata scales with history, all-day use unverified |
| Q13 | `c5afb6f` | 13 transfer/staging tests; child termination/restart/delayed read PASS | Open: native drag, OS clipboard/external delayed receiver, real transfer crash/normal exit |
| Q22 | `cc65bf9` | 7 revision/lease/rollback tests; native preview/close probe PASS | Open: actual Apply/copy across views and one-editor focus workflow |
| Q01 | This mapping commit | Full suite/map/release/format checks PASS | Open while required gates remain |

Reports and synthetic images remain ignored under `artifacts/gate-review-20261007`. Failed dock reports are preserved alongside successful repeats and structural checkpoints. The isolated crash child used only a validated temporary root, then the runner removed that fixture. No stress rerun, new package, remote push or publication occurred. The real user cache and OS preferences were not modified.
