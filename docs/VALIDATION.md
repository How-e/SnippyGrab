# Validation — updated 2026-10-07

Latest [P0 acceptance](P0-ACCEPTANCE-20261007.md): Q02/Q03 CLOSED with native isolated storage/editor/recovery gestures, actual OS clipboard contention/retry, owner tray Exit/Discard and restart evidence. Zero-warning Release build, 170 core + 61 Windows tests and full formatting PASS. **32 required gates remain open; 19 entries checked.** Older counts and scope statements below are historical.

Latest local results and exact source/probe boundaries: [agent completion review](AGENT-COMPLETION-20261007.md). All 24 local stages pass, including 231 tests and isolated native probes; the initial dock pointer-invariant failure remains recorded. Fresh-process startup/native OCR distributions and the next actual dock retest are included.

## Current remaining-work reconciliation

See [REMAINING-WORK.md](REMAINING-WORK.md) for live hosted checks, publication and protection evidence. Q36, Q35 and Q06 are now closed; 34 required entries remain open. The completed manual two-hour report passes completion and trend/recovery review; see [resource acceptance](RESOURCE-REVIEW-20261007.md). No stress workload was rerun. Earlier dated results below retain their original test counts and gate counts. The read-only resource report reviewer and release-gate fixtures are verified in this reconciliation.

## Current 15-gate verification

The [current gate pass](AGENT-GATE-REVIEW-20261007.md) has one focused commit for each requested gate, with Q01 last. Locked restore, warnings-as-errors Release build (zero warnings/errors), **170 core + 61 Windows integration tests** (zero failures/skips), full formatting verification and Git whitespace checks pass. The actual milestone map and five malformed mapping/ownership/closure fixtures pass; all ten release-gate fixtures pass. Q06 closes on bounded history/orphan/compaction regressions plus the existing reviewed two-hour Q35 evidence. Q01 stays open with 34 required gates unresolved.

Current offscreen failure/reliability, editor layout and dedicated child-process crash/restart probes pass. Dock Q49 initially failed final foreground preservation, then its full repeat passed 64 layouts and 768 anchor samples. Q18 runs failed final pointer-preservation checks while structural checkpoints retained passing layout/cache assertions; these are not overall PASS results. The later Q15 full dock probe passed. No pointer-moving desktop checks, OS clipboard writes, startup preference changes or real-cache damage occurred. No new package or published build is claimed; current probes use the development Release executable and synthetic isolated data.

## Earlier short verification

See [the latest acceptance record](TAKEOVER-ACCEPTANCE.md) for the environment, real computer-use observations, unavailable cases and exact release gates. Locked restore, zero-warning/error Release build, 152 core tests, 54 Windows integration tests on isolated rerun, formatting, dependency/native integrity, policy fixtures and package verification pass. The first Windows run had one foreground-window assertion failure during desktop focus activity; the rerun passed without concurrent UI actions. These checks do not close broad acceptance gates.

After the authorized history rewrite, the full Release build and all 206 tests pass again with zero warnings/errors/failures/skips. Source/configuration/test/image blobs match the starting tree. Stored Git objects contain no original machine-user path or Gmail-domain metadata; commit messages contain no task/milestone planning references. Stable packaging still rejects all 37 open required gates.

## Historical evidence

The dated sections below retain results and limitations for earlier source/builds. Their test counts, pending-at-the-time statements and packaged provenance are historical, not the current gate state. Local history is rewritten for privacy; historical build provenance retains the original source identity and no old package is relabeled as a new build.

## Installer and image quality fix — 2026-10-06

- Release build with warnings as errors and format verification pass. All 196 tests pass (145 core, 51 WPF integration), including 11px dialog OCR across all layout choices, enhancement bounds, preview/original fidelity and legacy settings defaults.
- The real install.ps1 ancestor guard passes against a hidden ancestor fixture under Windows PowerShell 5.1, reproducing the hidden AppData condition from the supplied setup error. Isolated fresh install, upgrade, rollback, checksum rejection and data-preservation checks pass under Windows PowerShell and PowerShell 7. This does not exercise Start menu/Apps registration or a clean user profile.
- Development offscreen reliability, editor layout and dock layout checks pass. Reliability checks verify all preview-quality decode widths, unchanged stored PNG bytes and a resized pin decoding beyond its previous 800px cap. Editor screenshots were inspected at 660 DIP / 225% text with the new 100% zoom action. Dock coverage retains bounded five-card/twelve-thumbnail caches. DPI handlers defer work and ignore bubbled child-image events to avoid reentrant WPF layout.
- The revised real OCR service was run locally against the supplied 825×421 image without modifying it. The chosen enhanced sparse-text output correctly reads `Get-Item` and `Could not find item`, which the original automatic pass misread. The comparative native passes report 0.77 confidence for original Auto and 0.81 for 2× SparseText; confidence is not measured transcription accuracy. Paths, punctuation and some characters remain imperfect. 3× scaling performed worse and is not used.
- Enhancement/layout choices follow [Tesseract's image-quality guidance](https://tesseract-ocr.github.io/tessdoc/ImproveQuality.html). OCR still uses original capture pixels; the preview-size setting never controls its input. Auto retains the original pass unless a candidate improves reported confidence, and does not merge duplicate text from multiple passes.
- The corrected setup/bundle is generated locally as `0.1.0-alpha.qualityfix.20261006`. Public release publication and clean-profile Windows installation acceptance remain separate gates.

Status: runnable **0.1.0 alpha**, not stable acceptance.

## Evidence

- Release build: zero warnings/errors with warnings as errors.
- 48 Core checks and 10 Windows non-interactive integration checks passed.
- Formatting and NuGet vulnerability audit passed; no known vulnerable dependencies reported by the current feed.
- Development and self-contained runtime checks passed: English OCR, exported opaque redaction, crop/blur/pixelation, physical capture pixels and PNG/file-drop construction.
- Native Print Screen key injection drove region selection to a valid PNG and visible shelf, with the original foreground window restored. Clipboard writes were disabled only in the harness to preserve user data.
- Per-user installation, installed runtime checks, and removal of files/shortcut/uninstall registration passed on the current Windows user. Clean-machine/login acceptance remains pending.
- Native OLE delivered two PNG paths to a synthetic WPF receiver; files remained valid after drop. **Not Codex acceptance.**
- Overlay drags matched exact 160×120 physical selections on connected monitors: (0,0), 1920×1080 / 96 DPI; (-1920,9), 1920×1080 / 96 DPI; (1920,-221), 2560×1600 / 144 DPI.
- Twenty captures produced a collapsed 226×128 DIP shelf. Dark editor/settings/history/first-run views rendered and were visually inspected; previews contain only synthetic content.
- Small 400×240 native capture plus PNG encode: 12–23 ms. English OCR: about 94–100 ms. Samples, not guarantees.

## Fresh-process benchmark

| Pipeline | Time |
|---|---:|
| 720×360 PNG encode → durable file → 448px thumbnail | 34 ms |
| 3840×2160 synthetic code, same pipeline | 119 ms |
| 7680×4320 synthetic code, same pipeline | 407 ms |

These exclude screen acquisition, user selection, receiver work and OS clipboard transfer. Fresh tray: 128.1 MB working set, 15.625 ms CPU over 5 seconds. After 4K/8K work and twenty captures: 156.1 MB, 0 ms CPU over 5 seconds. Short samples do not establish all-day resource behavior; memory tuning remains a target.

## User-reported desktop testing — 2026-10-06

[Recorded U01–U06 results](USER-TESTING.md#recorded-user-results--2026-10-06) establish reported real Codex focus/latest-image paste/cancellation, correct single-image drag, a three-image drop matching PiP order, generally working editor behavior and OCR. Windows Print Screen interception needed to be off. Exact build/environment versions and full checklist coverage were not supplied; these are user reports, not fresh agent verification.

Open reported issues: first-hover dock jitter with multiple captures, unreachable top expanded image, inconsistent selection collapse/re-entry, asymmetric Alt-reorder targeting, ineffective Delete/Enter with focus not confirmed, and unclear Save destination. Multi-image transfer followed preview order rather than selection-click order; the intended contract remains to be documented/verified. Follow-up priorities and acceptance criteria are in Q49/Q16/Q18/Q15/Q21/Q50/Q09 in [TASK_QUEUE.md](../TASK_QUEUE.md). Previously successful observations do not close unreported hardware, failure-handling or release cases.

## Remaining stable gates

ACCEPTANCE.md covers remaining Codex paste/drop cases and regression retests after fixes, other receivers, clipboard-lock injection, 125/175/200% scaling, HDR/vertical displays, Explorer restart, sleep/resume, display reconnection, fresh-user startup/install/remove, text scaling/high contrast and long resource stress. Hosted workflows cannot be observed before publication. Signing is not configured.

Generated reports remain in ignored artifacts or user-selected paths. Never commit desktop captures. This summarizes observed evidence, not an invented hardware PASS.

## Q02 implementation — 2026-10-06

Close and Exit await shared editor apply/clipboard work. Apply failure retains the editor and its leases; retry/discard guidance appears in the editor. A metadata replace failure rolls back the capture record, preserving its original revision. Clipboard exhaustion reports a saved shelf image and retains the editor for retry or explicit Discard. Exit pauses capture hotkeys, waits for editors and cancels shutdown on failure.

Release build: zero warnings/errors. 53 core and 10 Windows integration tests passed on this Windows checkout. New regressions exercise pending-work sharing, clipboard false results, retry after storage failure, separate editor work and locked-history rollback using isolated storage. These are coordinator/storage checks, not fresh WPF interaction or OS clipboard-lock acceptance. Targeted manual checks: edit then close and paste; Copy then immediately close/Exit; Exit with multiple dirty editors; Discard unsaved edits. Do not damage the real cache to induce faults.

## Q03 implementation — 2026-10-06

Unreadable primary history now loads a validated recovery sidecar on subsequent launches. Unknown old images are conservatively pinned, including when history is disabled and beyond the ordinary 2,000-orphan scan limit. Recent captures offers explicit confirmation; the original is copied to a unique archive before recovered metadata replaces it. Cleanup only resumes after successful promotion. An invalid recovery sidecar is also archived before replacement. Failed confirmation remains blocked and retryable; oversized recovered metadata cannot replace the original or enable cleanup.

Release build: zero warnings/errors. 63 core and 10 Windows integration checks passed on this Windows checkout. New isolated regressions cover corrupt, truncated, oversized and newer-version history across three launches, new pins and 24-hour transfer grace, history disabled, corrupt recovery sidecar, locked confirmation/restart/retry, absent primary metadata, 2,001 possible old pins and oversized recovery rejection. Unknown images still have placeholder dimensions pending Q06. WPF confirmation/cancel interaction and the Q02 desktop checks remain pending; no pointer-moving desktop self-test was run. This is a P0 implementation milestone, not stable acceptance.

Portable retest bundle: `artifacts/SnippyGrab-0.1.0-alpha.p0.20261006-win-x64` and matching ZIP. Self-contained locked-dependency publish succeeded; all 14 bundle checksums and the ZIP SHA-256 were verified. This packaging check does not establish installed or interactive runtime acceptance. Exit the previous app before launching the updated build.

## Capture overlay regression — 2026-10-06

User reported a compressed overlay with the lower live desktop exposed after launching the P0 package. The overlay positioned its HWND once in SourceInitialized, before WPF/Win32 completed size negotiation. New offscreen WPF regression checks reproduced clamping: requested 6400×1821 became 5567×1330; requested 3840×4320 became 3840×1330 in the Windows test host. Earlier overlay checks validated cursor selection coordinates without asserting the final window or rendered snapshot size, so they missed this failure.

The overlay now overrides native maximum tracking bounds and maintains its physical desktop rectangle through subsequent position/size requests. It waits for normal WPF Show rather than exposing the window from SourceInitialized. Tests assert final native bounds, DIP-to-physical layout, complete snapshot mapping and resistance to a later shrink/move request. The [Windows size-negotiation documentation](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-getminmaxinfo) describes the native tracking limits being overridden.

Release build passed with zero warnings/errors; 63 core and 13 Windows integration tests passed. A dedicated `--check-overlay-layout <absolute report path>` probe checks only offscreen synthetic windows without moving the pointer, capturing the desktop, writing the clipboard or changing focus. Development probe passed for the current virtual desktop size plus wide/tall layouts, on both offscreen sides. Observed probe DPI scale was 1.0; this does not establish every mixed-DPI layout or actual visible capture acceptance. Manual retest: full-screen freeze on every monitor, bottom-edge and cross-monitor region selection, Esc cancellation, then correct crop/paste. The reported screenshot remains untracked.

Updated portable bundle: `artifacts/SnippyGrab-0.1.0-alpha.overlayfix.20261006-win-x64` and ZIP. Locked self-contained publish passed; the packaged executable passed the four-layout offscreen probe, and all 14 bundle hashes plus the ZIP SHA-256 matched. Exit the P0 app from its tray before starting this executable. Pointer-driven capture acceptance remains pending.

## Dock geometry and state — 2026-10-06

User subsequently confirmed overlay coverage is restored; crop/paste/cancellation were not separately reported. Dock expansion now grows inward from the anchored primary card. The transparent window padding and shelf gaps participate in hit testing. Enter is idempotent; leave waits 180 ms and checks the physical window bounds before collapsing. Mouse selection survives collapse/re-entry without keeping the dock expanded just because a click gave it focus. Keyboard use, an open context menu and dragging hold it open. Selection frame thickness stays fixed to avoid resizing the window. The dock retains its monitor through hover/layout instead of selecting a monitor repeatedly from the changing pointer location.

Alt-drop takes the target card's original shelf position symmetrically upward/downward. Pointer capture protects the click/drag threshold and release outside the card does not open the editor. Native drag failure restores the interaction state.

78 core and 13 Windows integration tests pass. `--check-dock-layout` uses isolated synthetic captures, disabled hotkeys and an invisible tray icon, and keeps the dock offscreen. Its 32 layouts cover 1/3/5/20 captures, four corners and two orientations. Checks cover primary anchor, displayed-card bounds, repeated enter, selection-preserving leave/re-entry and unchanged foreground/pointer. Raised events are not actual pointer interaction; first-hover jitter, all hover controls, Ctrl-click, adjacent/nonadjacent Alt-drags and keyboard hold still need U03/U12 retest. The baseline probe measured 192 card constructions and 4 thumbnail decodes across its expansion/leave/re-entry sequences; this motivates Q18 card reuse and bounded thumbnail work. Its synthetic screenshot was visually inspected; screenshot content dominates the cards.

## Bounded dock rendering — 2026-10-06

The dock now retains at most five card views and twelve recently used thumbnail entries. Hover toggles controls on reused views; unchanged shelf content remains attached. Selection painting does not change frame size. Thumbnail keys include decoding size, while card keys include revision, dimensions, number, pin state and theme brushes. Refresh removes stale revisions. Both decoded image dimensions are bounded to twice thumbnail size (at most 800×800), including extremely tall images. Image content clips to rounded corners. Capture opacity fade honors Animate, Windows client-area animation and high contrast; expansion animation is deferred until actual hover stability is accepted.

81 core and 15 Windows integration checks pass. Cache tests cover bounded growth over 2,000 keys, recent-use eviction and failed-decode retry. New image checks cover extreme portrait/landscape sizing. The same 32-layout synthetic hover sequences reduced card construction from 192 to 6; a twenty-item scroll sweep kept both caches within their caps, and replacing a capture decoded its new revision. These counts measure rendering work, not user-visible latency or prolonged resource use. U03/U12 gestures, jitter/reachability, and themes/reduced-motion behavior remain acceptance gates.

The layout probe also covers mixed landscape, portrait and panoramic captures. Cards align to their anchored edge so a taller neighbor does not stretch/move the primary card in a horizontal strip. The hover surface uses alpha 8/255 in its padding/gaps, rather than alpha zero: [Windows layered-window hit testing passes mouse messages through alpha-zero pixels](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features). Rendered padding alpha is checked in the probe; WPF hit testing alone would miss that native behavior.

Portable retest bundle: `artifacts/SnippyGrab-0.1.0-alpha.dockfix.20261006-win-x64` and ZIP. Locked self-contained publish succeeded. The packaged executable passed all 32 dock layouts (including minimum configured opacity) and four overlay layouts; all 14 bundle hashes and the ZIP SHA-256 matched. No real pointer gestures, desktop captures or clipboard writes were performed by these probes. Exit the previous app from its tray before launching this build. Next: targeted U03/U12 and Codex capture/paste/drop regression checks; Q15 keyboard actions remain pending.

## Explicit dock keyboard focus — 2026-10-06

Numbered badges now focus a capture without opening its editor; the tray also has Focus screenshot shelf (keyboard), and context menus offer focus. Capturing/revealing/hovering stays nonactivating. Arrow direction follows the anchored visual order; Home/End reach scrolling captures; Space toggles selection. Copy/Delete use the selected set even if focus is on an excluded capture. Enter/export/pin use the focused identity. Ctrl+H/Ctrl+, open history/settings. Escape clears selection/collapses and clears WPF keyboard focus. Tab cycles through card controls, with ordinary button Enter/Space behavior preserved. Focus/Selected text supplements color; accessible card names include ordinal and selection state.

85 core and 15 Windows integration checks pass. Focus/selection tests cover a single nonprimary item, nonadjacent selection with an excluded focused item, reorder identity/order and removed records. The offscreen dock probe additionally routes actual command-handler calls through an intercepted sink, testing target membership and scrolling without clipboard writes, dialogs or edits. This verifies command routing, not native focus acquisition or assistive technology. U03/U10 focus/navigation/buttons, U01 composer focus, themes/scaling/high contrast and target sizes remain acceptance gates.

## Apply/copy and PNG export semantics — 2026-10-06

The editor now separates Apply + copy (managed capture plus clipboard) from Export PNG (Ctrl+S, file dialog, no clipboard dependency/write). The configured export directory is used initially; the last successful destination is stored in optional local history and prefilled on repeat export, with overwrite confirmation. Success shows the full path and enables Open export folder; the dock context menu also offers that folder after export. Cancellation and failed file writes do not mark a new success. If PNG creation succeeds but history persistence fails, the result explicitly reports a safe exported file with a metadata warning. Managed-cache destinations (including a trailing-separator root), wrong extensions and redirected destinations are rejected. Toolbar wrapping keeps all editor actions reachable at minimum width.

89 core and 16 Windows integration checks pass. Isolated export tests verify current revision/repeat export, destination persistence, destination conflicts, cache/extension rejection and a locked metadata file. A real PNG decode verifies applied opaque redaction in the export. The offscreen editor probe validates labels, enabled folder action and every action's bounds at 660/1000 DIP widths; the 660-DIP synthetic preview was visually inspected. Actual file dialogs, cancel/overwrite prompts, native folder opening and interactive editor copy/export/close behavior remain U04 acceptance checks.

## Multi-file transfer order — 2026-10-06

The contract is ascending current shelf-number order, not selection-click order. Both file clipboard and native drag now normalize through the same repository order and reject stale revisions/unknown identities instead of silently changing membership. Duplicate requests yield one file. Native drag adds image/PNG formats only when the normalized payload has one unique file. Reordering changes subsequent payload order; History transfers use underlying shelf order even when its time-sorted list differs. The physical strip can run upward/leftward from its anchored primary card; number order remains unambiguous.

92 core and 17 Windows integration checks pass. Core regressions cover twenty-item reverse/nonadjacent requests, exact membership, duplicates, reorder/restart and stale/unknown identities. A Windows DataObject regression confirms drag file order matches the file-clipboard input and verifies multi-file versus single-file image/PNG formats. This constructs actual OLE data without running a drag or writing the OS clipboard. U02 receiver order/membership and delayed reads remain open; a receiver may reorder its attachment presentation independently.

Portable retest bundle: `artifacts/SnippyGrab-0.1.0-alpha.keyboardexport.20261006-win-x64` and ZIP. Locked self-contained publish succeeded. The packaged executable passed the dock, overlay and editor layout probes; all 14 bundle hashes and ZIP SHA-256 matched. These probes use offscreen synthetic windows and do not establish real keyboard focus acquisition, file-dialog behavior or receiver acceptance. Exit the previous app from its tray before launching this build. Target U03/U10 keyboard focus and selection actions, U04 apply/export/cancel/overwrite, and U02 transfer order after reordering. Next implementation priority is Q09 hotkey/setup guidance.

## Ten-item reliability implementation and testing pass — 2026-10-06

The requested implementations were committed before running the testing pass, in this exact order. Q33 regression coverage was added with each item; Q33 was not counted as an extra manual task.

| Item | Implementation commit | Automated evidence | Remaining acceptance |
|---|---|---|---|
| Q09 | `173a368` | Injected registration conflicts, fallback/no-repeat flags, nested setup pause state | U06 real Windows interception/other hotkey owner/held keys |
| Q07 | `d249727` | Injected PNG/metadata/permission failures, startup rollback and rollback failure, unchanged startup, isolated writable-cache probe | Real cache/storage/settings UI recovery |
| Q14 | `52aeea8` | Retry exhaustion/recovery/supersession; real DataObject image/PNG/files/path/filename/text construction with injected clipboard; cancelled OCR publication | Real locked clipboard and OCR interaction |
| Q28 | `4171cff` | Error categories, cancellation and feedback payload privacy | Dialog/event error UX |
| Q04 | `20848a0` | Unpin/clear/revision/close view leases; offscreen native pin refresh/cleanup | U03/U09 resize/topmost/opacity/click-through/return and mixed DPI |
| Q05 | `e374f34` | Expired restore, new shelf lifetime, original timestamp, persistence and storage age | Real history/pin return workflow |
| Q06 | `6595229` | 2,100 orphan files; aggregate history over 4 MB, bounded pages/restart/pins; lazy PNG dimensions and corrupted page fail-closed | Q35 prolonged memory/latency stress |
| Q13 | `b819e17` | Fake-clock retention 1h/24h/7d/never, leases, extended grace/restart/revisions, failed transfer persistence, session cohort | Native drag/OS clipboard/delayed receivers and process termination |
| Q22 | `93cd7a3` | Stale editor revision rejection, no duplicate superseded revisions on restart, offscreen history/pin refresh | Actual editor apply/copy across views |
| Q29 | `2654d9f` | 200-row history pages, optional history, persistent pins, dismiss semantics/rollback; open-history capture/edit/cleanup refresh | U03/U09 pin gestures and DPI |

Final Windows x64/.NET SDK 10.0.400 results: **120 core tests + 18 Windows integration tests passed**, no skips/failures. `dotnet test SnippyGrab.sln -c Release --no-restore` passed; `dotnet build SnippyGrab.sln -c Release --no-restore` passed with **zero warnings and errors**. `git diff --check` passed.

Development executable probes passed: 32 dock layouts (1/3/5/20 captures, all corners/orientations, scrolling/revisions/keyboard routing), four overlay layouts (observed offscreen scale 1.0), editor toolbar bounds at 660/1000 DIP, and the new `--check-reliability <absolute report path>` lifecycle probe. The lifecycle probe opens offscreen WPF history and pin windows with synthetic PNGs, proves selection preservation and actual preview revision refresh, unpin/clear protection and pin-close lease release, and injects pending OCR/clipboard operations to prove newer copy and editor cancellation cannot publish stale text. Reports are ignored local artifacts under `artifacts/reliability-checks/`.

The testing pass initially had one incorrect regression expectation: when startup and rollback both deny access, the transaction intentionally returns an aggregate failure. That expectation was corrected. The new native lifecycle probe then reproduced a stale history preview: WPF retained equal selected rows without firing SelectionChanged after edit. Refresh now explicitly updates the preview, and the probe passes. Review also corrected overlapping setup/settings suspension, late OCR after a newer copy, copied-path transfer grace, old revision orphan resurrection, and metadata-page checksum/schema safety. Paged manifests use schema 2; legacy schema 1 still loads, while older builds fail closed on schema 2 rather than ignoring persisted pins in pages. Future-schema recovery is tested with schema 3.

No real desktop screenshots, pointer movements, OS clipboard writes, startup registration changes or external transmissions were performed by the added probes. No pointer-moving self-test was run. No new portable/installer package was generated in this pass. This is implementation/automated evidence; the shared/user acceptance gates above remain open. Large history is paged on disk and in the UI but the repository retains its metadata list in memory; prolonged resource measurements remain Q35.

## Second ten-item implementation and testing pass — 2026-10-06

The requested ten items are Q38, Q37, Q39, Q08, Q10, Q11, Q20, Q24, Q23 and Q17. Shared acceptance gates are preserved rather than counted as fully verified from local tests.

| Item | Change and evidence | Residual gate |
|---|---|---|
| Q38 | Content signatures/decoder allowlist; PNG pre-decode dimensions/end marker; actual PNG/JPEG/BMP, disguised GIF, truncation/oversize tests; per-operation redirected-path checks and isolated post-startup junction substitution preserving an external sentinel | Same-user check/use races and exhaustive native codec fuzzing remain outside this boundary |
| Q37 | Machine-readable NuGet policy, 14 fixtures, restore warnings-as-errors, immutable Action references, native/model SHA-256 manifest, CI/release/package enforcement | Hosted checks Q36; full native codec inventory/upgrade Q51 |
| Q39 | Source/tracked-file/package boundary review, revised threat model, upstream native model advisories recorded, runtime pinned model verification | Stable security gate remains tied to Q51 and hosted analysis |
| Q08 | Production region-selection normalization for reversed/clipped/tiny rectangles; native active/window/desktop/region/Esc computer-use checks | Full hardware/hotkey/protected-content matrix |
| Q10 | Persist Windows monitor interface identity, migrate existing numeric choices, retain identity on disconnect, primary fallback, hidden topology refresh; deterministic reorder/reconnect tests and 100/125/150/175/200% placement checks | Physical reconnect and remaining real DPI/driver configurations |
| Q11 | Explicit snapshot-time cursor and SDR contracts; physical hotspot math used by compositor and regression-tested across DPI/negative coordinates | Native cursor boundaries and HDR fidelity are not claimed verified |
| Q20 | Every flattened required annotation/effect render, opaque redaction, crop/annotation coordinates, real undo branches and immutable prior transfer revision; visible reversed redaction, undo/redo, crop, zoom and freehand checks | Full human/assistive-technology gesture acceptance |
| Q24 | Validate encoded input and model digest before native parsing; wrapped loader guidance; full/area terminal, traceback and dialog OCR; queued cancellation, malformed/repeated requests, modified-model rejection; visible local OCR | Model can confuse similar glyphs; native library gate Q51 |
| Q23 | Freehand preview shares live points and snapshots once; committed strokes render as one path; image render/crop/effects/PNG encoding on workers; 20-state/256 MiB approximate retained undo budget and explicit release on close; readable labels | End-to-end targets and prolonged stress remain Q34/Q35 |
| Q17 | Top/Bottom/Left/Right edge positions expand inward, center perpendicular to expansion and clamp to work area; existing corner numeric values retained; all 64 offscreen count/position/orientation layouts preserve primary anchor | Actual edge/mixed-DPI gesture acceptance remains part of Q32 |

Windows x64/.NET SDK 10.0.400: 132 core + 42 Windows integration tests pass, no skips/failures. Locked restore and dependency/model/native integrity audits pass. The 64-layout dock probe, four overlay layouts, six editor layouts (660/1000 DIP, 100/150/225% text) and reliability probes pass. Every action/tool stays in bounds, including the readable OCR-area label. Reliability additionally verifies closing during a worker blur waits for the finished document, applies it, and reaches the injected clipboard sink. Computer-use evidence and reduced manual retest scope are in [USER-TESTING.md](USER-TESTING.md#agent-interaction-results--second-ten-item-pass-2026-10-06).

Measured 20,000-point strokes initially allocated about 1.07 GB of managed memory and rendered in 2.5–2.9 seconds using separate line calls. One path reduced managed allocation to 0.85–0.92 MB. Final worker renders measured 215 ms at 1080p, 168 ms at 4K and 621 ms at 8K; three successive crops took 132/439/1698 ms. A 200×160 blur, including full-image flattening, took 37/122/505 ms. The journal retained three undo steps at 1080p/4K and one at 8K under its budget. These are individual synthetic measurements, not latency distributions, native memory totals or all-day stress acceptance. `--check-editor-performance` is repeatable without desktop capture, input injection or clipboard writes.

The OCR corpus exposed a zero/@ substitution in a 30px terminal fixture. Scaling and confidence selection did not reliably improve the existing corpus, so the production recognizer was preserved and the limitation is recorded. The pre-existing 32px exact CS1002 test still passes; new 30px tests check representative text while retaining the observed identifier limitation.

All reports/screenshots are ignored local artifacts. Real full-desktop test pixels were not copied into repository files; the exact small region artifact contains only synthetic text. The interactive lane uses isolated temporary storage. Automatic approval review rejected cleanup of the two development caches, giving only "blocked by policy" as the reason. They remain under Windows Temp; the exact locations are the `Root` entries in local interaction reports. The packaged fixture retained synthetic data only. The pointer-moving legacy `--self-test` was not run because the computer-use skill requires its API for Windows inputs; computer-use supplied native inputs. No external message/upload or repository publication occurred.

Implementation commit: `ae3f5ac`; formatter-only follow-up: `51f2d2e`. The full `dotnet format --no-restore --verify-no-changes` gate now passes. Its first run exposed existing formatting violations; normalization is separated from functional changes. Final locked restore, warnings-as-errors Release build, 174 tests and live dependency/native/model audit pass.

Portable bundle and ZIP: `artifacts/SnippyGrab-0.1.0-alpha.hardening.20261006-win-x64`. Fresh self-contained publish and all **14 bundle hashes plus ZIP SHA-256** pass. The packaged executable independently passes the 64 dock/four overlay/six editor layouts, worker-close reliability and 1080p/4K/8K editor-performance probes. Final packaged computer-use confirms local OCR success, reversed blur rendering, Apply + copy persistence plus text/image sink writes, and the named three-monitor picker; the fixture exited normally. Clipboard interception still means no external receiver delivery was tested.

The available packaged synthetic `--benchmark` also passed: fresh-tray working set 122.4 MiB, 15.625 ms process CPU over five seconds; twenty-capture shelf 160.6 MiB and zero observed CPU over five seconds. PNG encode/storage/thumbnail pipeline took 35/122/439 ms for 720×360/4K/8K. These short samples exclude screen acquisition, clipboard transfer, full workflow latency and hours-long stress; Q34/Q35 remain open. Hosted CI/CodeQL, clean Windows installation/login, other hardware and external Codex receiver acceptance are not available in this lane.

## Third ten-task pass — Q25
Queued OS callbacks are suppressed after disposal. Taskbar recreation reasserts tray visibility; resume refreshes topology without revealing a hidden shelf, preserves hotkey pause and reports conflicts. Double-click uses the configured default capture mode. Lifecycle-disposal regression passes; Release build passes. Actual Explorer restart and sleep/login hardware acceptance remain U09.


Q34: --check-pipeline-latency records ten synthetic 1080p/4K/8K samples with nearest-rank median/p95 and hardware context; clipboard uses an injected sink. This run: warm medians 35.7/104.4/379.9 ms, p95 38.3/109.9/384.7 ms, controller startup 383 ms. Capture PNG encoding now uses a worker. Production logs request-to-overlay and selection-to-crop separately from total time (which includes user selection). No cold CLR, OS clipboard or Snipping Tool comparison is claimed; Q34 stays open for those measurements.


Q35: repeatable --check-resource-stress <report> [seconds] defaults to two hours and always runs at least 300 captures. Checkpoints include CPU, working/private/managed memory, process/GDI/user handles, post-GC and post-disposal samples. This bounded run passed 411 captures in 121 seconds, 4 real OCR requests, worker blur/revision refresh and three offscreen pins per 25 cycles. No desktop capture/input/OS clipboard writes. The two-hour/all-day gate remains open; the short run does not establish leak freedom. Reports remain in ignored artifacts/third-pass/stress.json.


## Third ten-task implementation and test pass — 2026-10-06

Ten individual task commits cover Q25/Q26/Q27/Q34/Q35/Q42/Q40/Q41/Q43/Q33. This is an implementation milestone; broad shared and prolonged acceptance remains open. Q33 adds meaningful startup rollback, installer admission, per-user upgrade and packaging retention boundaries, alongside the existing storage/crash/schema/clipboard/DPI/editor/OCR regressions. CI and release run the isolated fixtures; release also checks embedded setup and artifact digests before publication. No hosted workflow was observed.

Windows x64 / SDK 10.0.400: **145 core + 42 Windows integration tests pass**, zero skips/failures. Locked restore, warnings-as-errors Release build, formatting and diff checks pass. All five expected projects must appear exactly once in the live dependency audit; **16 policy fixtures** pass, and native/model integrity passes. Packaging exposed the installer single-file SDK dependency; its dedicated publish lock now pins that dependency. Testing also corrected stale-startup disabling/rollback, prevented closing setup during active installation, and bounded total embedded payload size.

Isolated PowerShell labs pass valid/tampered/traversal/duplicate/incomplete payloads; fresh installation, obsolete-sidecar upgrade separation, rollback and separate-data preservation; dry-run/three-build retention and modified/running/unowned-output preservation. The running-path cleanup branch uses an injected process-path snapshot, not process termination. Registry failure restoration is implemented but not fault-tested against the real registry. Old install backups, pre-registry artifacts and failed staging outputs are preserved for explicit review.

The offscreen installer preview was inspected visually. Portable ZIP and self-contained setup candidate `0.1.0-alpha.lifecycle.20261006.1` passed complete bundle inventory, ZIP/setup SHA-256 and independently embedded payload verification. Its executable passed dock/overlay/editor/reliability probes and a packaged 300-capture/three-OCR smoke run. A final package is rebuilt from the committed tree as `0.1.0-alpha.lifecycle.20261006.3`; outputs are ignored under artifacts. Development stress passed 411 captures/121 seconds/four OCR requests. Working/private bytes after GC were about 162/91 MiB; post-disposal USER handles dropped from 28 to 24. These are short samples, not proof of leak freedom or all-day acceptance.

Latency distributions and their excluded stages are recorded above. No real desktop capture, pointer/key injection, OS clipboard write, startup registration, sign-out/login, system preference change, external upload or publication was performed in this pass. Real Explorer restart/sleep, settings UI, Windows 10/11 fresh-profile installation/login/Apps uninstall/SmartScreen/policy, Snipping Tool comparison, hours-long stress, receiver/hardware/accessibility and Q51 native advisory remediation remain gates. Only Q33/Q40/Q43 local agent deliverables are checked off.

Final setup visual verification exposed a full commit hash wrapping to a lone final character. Setup now shows the complete release version plus an eight-character commit identity, preserving build provenance in the bundle.

## Remaining-queue implementation and verification — 2026-10-07

Q45 color sampling/point-centered zoom (`129b5b1`), Q46 persistent monitor-relative pin layout/opacity (`cdd1d54`), Q48 optional verified signing/language policy (`9ad48d9`), Q19 accessible labels/contrast/30-DIP focus targets (`25a682c`, `6cd6332`), Q44 stable gate enforcement (`9e78cf4`, `61c0d81`), Q01 milestone ledger (`996deff`), and Q47 documented optional redesign deferral (`4a182f8`) were committed individually. Narrow shelf actions remain reachable through an all-actions menu. Signing is disabled by default; eight isolated signature fixtures pass. Ten release-gate fixtures include the actual queue and padded identities. Stable packages refuse open required gates.

Q51 native remediation is committed in `d745697`, `99dc0a0` and publish correction `212267c`. Source-built Tesseract db20f322d03664d1e878e2fbf6e904f5da755594 and Leptonica 8ad618f103972eed12f195499b92cff1ab37067b replace the old NuGet binaries. Eleven reviewed upstream fixes are pin ancestors; external codecs/curl/archive are disabled and exported codec inventory is empty. Production decodes bounded WIC pixels into raw Pix; native PNG decoding is unavailable. See NATIVE-OCR.md for source/license/advisory and trusted build-receipt boundaries. The first publish attempt was rejected for missing sidecars; qualified MSBuild item metadata fixed it before delivery.

Windows x64 / SDK 10.0.400: **152 core + 54 Windows integration tests pass**, zero skips/failures, against the replaced native DLLs. Locked restore, zero-warning/error managed Release build, formatting and dependency audit pass. Upstream native compilation emits the documented OpenMP-SIMD warning with OpenMP disabled. Sixteen dependency, eight signing, ten release-gate and eight native-receipt policy fixtures pass; isolated installer/upgrade/retention checks pass.

Portable/setup **0.1.0-alpha.queue.20261006.3**, clean source commit `212267c`, passes complete inventory, ZIP/setup checksums and independently embedded payload verification. Provenance includes native source pins/recipe/receipt and model/dependency locks; five license files are included. Packaged six-case full/area OCR corpus, malformed input/cancellation, reliability, six editor layouts and four overlay layouts PASS. The short packaged resource smoke completes **300 captures, three OCR runs, 8.16 seconds**. This is a smoke, not prolonged evidence. A final packaged dock run fails its pointer-stability assertion during external desktop movement; no complete packaged dock PASS is claimed for this run. The earlier final-code development retest passed 64 layouts, narrow toolbar widths, 20-item traversal and bounded 5-card/12-thumbnail caches.

The agent-owned two-hour attempt was stopped at the user's request after about 59 seconds; its report remains RUNNING and is not acceptance evidence. Q35 is user-owned pending the documented manual 7200-second run and trend/recovery review in RESOURCE-TESTING.md. No prolonged probe will restart automatically. No new desktop capture, OS clipboard write, input injection, login/startup change, signed artifact or remote publication was performed in this pass. Hardware/receiver/accessibility/OS lifecycle and hosted release gates remain as mapped in MILESTONES.md.

Q42 independently cloned the clean tree with `git clone --no-hardlinks`, created a matching **local-only tag inside that isolated clone**, and ran provision/locked restore/package with RequireTag. Build 0.1.0-alpha.cleancheck.20261006 reports commit 212267c07fda2d31a97326889cb7e0926f359136 and Dirty=false. Native sources were compiled independently in that clone. Complete bundle, ZIP/setup SHA-256 and embedded payload verification pass. Two archives of the identical final folder produce SHA-256 **ADB8588427FACB1AD1676E05C59A5B6EF500F10D2C807C065A8D35ED4445CE44**. Reproducibility means deterministic archives from fixed inputs plus complete rebuild provenance; independent native/ReadyToRun binary byte identity is not claimed. Curated changelog is included in the .3 bundle. Hosted tag/workflow execution remains Q36. No tag was added to the primary checkout or published.

## Current agent gate review — 2026-10-07

See [per-gate current checks and acceptance](AGENT-GATE-REVIEW-20261007.md). Historical results above retain their original scope; current evidence does not imply gesture or receiver acceptance.
