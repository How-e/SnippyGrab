# Changelog

## Unreleased — UI Design import fixes

- Fixed the blank source window opened from UI Design: native dialogs now open after the editor's WebView2 message callback returns. Source controls show initialization/loading state, support Enter to load, and enable capture only after navigation succeeds. Regression coverage follows the real Import page → Load → capture route.
- Full-page capture now gives lazy images a bounded loading opportunity, materializes `content-visibility: auto` regions, and restores nested panel scroll positions. Missing image resources are reported explicitly.
- Preserve direct text inside `display: contents` containers as editable text, with a measured group for its rendered descendants.

- Accept scheme-free LAN/localhost addresses and prompt for HTTP Basic authentication. Form sign-in uses the source browser's retained session.
- Default URL import to the full loaded page and add full-page existing-tab import, including offscreen selected elements, bounded loaded scroll panels, high-DPI references and cleanup after errors. Reload the unpacked extension and accept its page-capture permission after updating.
- Preserve inline text geometry/spacing, computed typography, side borders, page background and display-contents/positioned children. Use explicit raster fallback for generated icons and complex SVG strokes. Bundle available WOFF/WOFF2 resources and font-face metadata in both adapters.
- Preserve the source viewport separately from the full-page artboard when comparing implementations. Add native authentication/layout regressions and isolated Chromium extension coverage; broad page fidelity and owner Chrome/Edge acceptance remain separate.

## 0.2.0-alpha.20261029.1 — 2026-10-29 (local only)

- Replaced assisted scrolling with UI Design: independent editable layers imported from a URL, localhost project, local HTML or optional existing-tab browser extension.
- Added layer selection, drag/resize, inline property/text editing, typography/appearance, primitives, grouping, duplication, reparenting, alignment, visibility/locking, intent and implementation scope. Original/edited comparison and net changes distinguish the final request from edit history.
- Added versioned portable design files, bounded transactional undo/redo, atomic saves, stale-save protection, owned recovery snapshots and PNG shelf links back to editable designs.
- Added reviewed agent briefs and verified ZIP handoffs with structured changes, source evidence, matching original/edited images and affected-region crops. Private targets are removed from scene/history/image evidence, including overlapping raster regions. No agent message or cloud upload occurs automatically.
- Added an optional unpacked Chrome/Edge extension and explicitly registered per-user native messaging bridge, plus implementation recapture/context and pixel-difference reports.
- Admitted a pinned WebView2 SDK and optional installed Evergreen Runtime. Browser privileges, navigation, resources, native messages and package admission are bounded; missing-runtime guidance preserves ordinary screenshot functionality.
- This unsigned alpha is built and tagged locally only. New workspace close/capture/accessibility acceptance is reopened; broad browser fidelity, live signed-in extension workflows, clean-profile prerequisites, independent agent implementation and prolonged resource behavior remain separate acceptance work.

## 0.1.0-alpha.20261028.1 — 2026-10-28

- Isolated capture PNG delivery and clipboard image preparation from long exports and editor effects. Native OCR keeps its separate reusable worker; release verification now checks production OCR isolation, real recognition and engine reuse in the packaged build.
- Streamed unchanged PNG exports through atomic temporary files, preserving cancellation, revision protection and overwrite decisions while avoiding a full-file encoded buffer.
- Skipped history writes, change notifications and shelf refreshes when cleanup has nothing to change. Expired transfer protection and completed deletions still persist.
- Added optional local performance diagnostics under Settings → Application. Resource, CPU, GC, handle and dispatcher-delay samples use two bounded 1 MiB files; the option is off by default and omits screenshot content, OCR text, window titles and private paths.
- Added correlated capture timing records that distinguish human selection time, queue wait, encoding, storage and clipboard delivery, with explicit cancellation and fallback outcomes.
- Fixed Recent captures Ctrl+S export, captured composition revisions before asynchronous preparation, and validated Combine's 2–20 selection limit before decoding.
- This remains an unsigned alpha with ten open acceptance gates. Automated, synthetic and packaged checks do not establish all-day leak freedom, universal latency or renewed physical desktop/receiver acceptance. The local dock probe's desktop-preservation guards remain unresolved.

## 0.1.0-alpha.20261017.1 — 2026-10-17

- Refreshed native windows, dialogs, menus, shelf and installer with neutral Dark/Light/System themes, clearer page headings, quieter controls and screenshot previews. Retained keyboard focus, text scaling and Windows high-contrast support.
- Bounded image work, moved thumbnail preparation off the UI thread, released closed view resources and reduced history persistence churn. Isolated capture acquisition from background image processing.
- Hardened redirected storage/export paths, cleanup synchronization, second-instance activation and bounded diagnostics.
- Preserved files that appear during export unless replacement was approved, and reserved OCR ordering before editor rendering so a newer Copy cancels older OCR.
- Rejected checksum path aliases and unlisted installer payloads, resolved all recovered selections before composition, and deferred pin layout when display enumeration is empty.
- This remains an unsigned alpha prerelease with ten open acceptance gates. Automated and offscreen checks do not renew physical desktop, external receiver, hardware, clean-profile or prolonged-resource acceptance.

## 0.1.0-alpha.20261010.1 — 2026-10-10

- Reused exact stored PNG bytes for Copy and moved image preparation onto workers while preserving immutable revisions, clipboard request ordering, retries and cancellation. Import and editor opening also prepare images asynchronously.
- Reduced history metadata churn with sealed older pages and an atomic inline head. Cleanup yields between bounded slices and rechecks leases/pins; individual slow file operations can still pause a slice.
- Reduced JPEG memory by flattening alpha one row at a time. Lossless WebP uses a faster exact-pixel preset and avoids a duplicate input surface; dimensions, transparency and export cancellation remain covered by regressions.
- Rendered editor effects and area operations only in their selected region. Frozen unannotated crops share backing pixels, retaining more undo steps under the existing memory budget.
- Loaded history/pin previews on workers, canceled obsolete requests and reused previews for metadata-only changes. Original preview quality is now bounded to four million pixels; full-resolution edit, copy, OCR and export remain available.
- Reused a serialized, integrity-checked native OCR engine and improved bounded small-text enhancement. The previously missed synthetic 14-pixel CS1002 fixture now passes enhanced recognition in all three layouts.
- Passed 314 automated tests, offscreen reliability/editor/OCR/crash-recovery probes, independent WebP pixel decoding and a 120-second synthetic resource run. Measurements on one development machine showed lower copy/export/effect costs; they do not establish universal latency or a leak-free claim.
- Reopened ten affected acceptance/milestone gates for renewed desktop/receiver, OCR, preview/editor, setup, end-to-end performance and prolonged-resource checks. Earlier owner acceptance remains specific to its recorded build. This is an unsigned alpha prerelease; checksums verify integrity, not publisher identity.

## 0.1.0-alpha.20261009.3 — 2026-10-09

- Added quiet six-hour release checks, green/red Help & about status, in-app changelog and verified transactional updates with restart for installed and portable releases.
- Kept source builds explicitly alpha and compared legacy dated alpha releases by date/revision so older release labels cannot outrank newer builds. Downloaded installer scripts are admitted using the installed checksum validator.
- Centered tray labels, shortcuts and icons, corrected dialog heading alignment, and moved Delete onto the dock toolbar at all thumbnail sizes. Deletion retains shelf selection and history/transfer semantics.
- Added clearer composition and scrolling instructions, thumbnails and state-specific actions; dock controls now overlay a translucent image strip. Fixed exact scrolling joins at large viewport heights and batch Skip collisions appearing during export. Single-image exports expose progress/cancellation.
- Added actionable OCR readiness checks and timestamp/publisher verification tooling; real publisher signing still requires external identity/access.
- Added explicit monitor capture, shared PNG/JPEG/WebP export, ordered cancellable batch export, and native-pixel strip/grid composition.
- Added bounded user-assisted static vertical scrolling with seam correction/undo and private frame staging. No automatic scrolling or recording.
- Added pinned WebP source/recipe/binary provenance and codec/license review, and corrected the editor export tooltip to include WebP.
- Closed all 51 acceptance gates for the owner-approved unsigned alpha scope using current-build desktop, Windows 11 VM and full monitor/DPI/HDR batch PASS reports plus separate agent verification. Product code is unchanged from the accepted build; exact hardware/receiver versions and warning text were not supplied. Automated dock foreground checks failed and the earlier hotkey probe was skipped; those outcomes remain documented separately from owner acceptance.
- This is an unsigned prerelease. Windows or browsers may warn; SHA-256 establishes download integrity, not publisher identity. Capture remains SDR, English is the supported offline OCR language, and scrolling is bounded user-assisted static vertical capture.

## 0.1.0-alpha.design.20261008.1 — 2026-10-08

- Unified WPF windows, menus, dialogs and tray icons with shared Light/Dark/System themes, line icons, visible keyboard focus and high-contrast colors; refreshed the installer appearance.
- Organized settings into seven categories with preserved drafts and field-level validation. Added a scrollable editor tool rail, contextual properties and a responsive history list/preview view.
- Updated README previews and contributor testing guidance. Replaced internal planning and acceptance reports with an executable release-gate manifest; detailed results remain local.
- Checked the current design at normal and narrow widths; automated coverage includes theme changes, complete settings/tool access and editor bounds at enlarged text sizes. New-design receiver, hardware and assistive-technology acceptance remains separate.

## 0.1.0-alpha.acceptance.20261007.8 — 2026-10-08

- Added an explicitly illustrated, privacy-safe capture-to-Codex workflow animation at the owner's request.
- Recorded owner native receiver acceptance with receiver/version details waived, and accepted the measured region/clipboard performance scope. SnippyGrab median/p95 was 48.6/81.3 ms over ten captures; Snipping Tool was 62.6/1585.3 ms on the same host with varied regions.
- Added a passive timing collector that saves timing and clipboard format availability without reading image contents or injecting input. Exact hotkey-overlay distributions, cold boot and physical 4K/8K workflow remain unmeasured.
- Fixed an editor apply/close reentrancy race by installing the shared completion task before invoking apply; added a deterministic dispatch-order regression. The hosted pin-resize probe now uses realized window bounds instead of assuming a large monitor.
- Verified the corrected hosted release upload and independently downloaded ZIP/setup/checksums, embedded installer payload, clean source provenance and native probes. All 51 acceptance entries are closed within the documented owner-approved scope. The published build remains an unsigned prerelease.

## 0.1.0-alpha.acceptance.20261007.7 — 2026-10-07

- First-run setup prominently prompts users to disable Windows Print Screen screen capture and offers a button to open Keyboard settings.
- Added an initial Pictures / PNG export-folder choice with a native Browse picker, defaulting to the user's Windows Pictures location. Existing preferences remain intact; invalid destinations keep setup open with actionable feedback.
- Recorded owner acceptance of fresh Windows 11 VM installation and feature workflows without additional programs/extensions. Four required acceptance gates remain open; stable readiness is not claimed.

## 0.1.0-alpha.queue.20261006.3 — 2026-10-06

- Initial publication verification on 2026-10-07 fixed clean native builds under warnings-as-errors and made the synthetic desktop self-test wait for its own exposed pixels. Source pins and production capture behavior are preserved.
- Added color sampling from flattened edits and point-centered magnification without changing undo history.
- Detached pin position/size/opacity/topmost/click-through persist on explicit reopen, with disconnected-display clamping and continuous opacity adjustment.
- Added editor input names, theme-aware contrast, stable focus borders, opaque high-contrast shelf and reachable narrow-shelf action menus.
- Rebuilt native OCR from pinned maintained source commits with reviewed model-parser fixes and external codecs disabled. Routed encoded input through Windows WIC into raw RGBA, preserving alpha semantics and exact recognition regressions.
- Added opt-in verified signing and enforced stable packaging gates. English remains the supported OCR language.
- Added milestone evidence/ownership ledger and deferred optional shelf redesign. Hardware/receiver/OS/hosted/prolonged acceptance remains open.

## 0.1.0-alpha.qualityfix.20261006 — 2026-10-06

- Fixed Setup's hidden AppData ancestor check under Windows PowerShell; kept redirect rejection and staged-install integrity checks.
- Added Balanced/Sharp/Original preview quality, DPI-aware shelf/history decoding, high-quality scaling, and resized pin refreshes. Original captures, exports and OCR remain full resolution.
- Added editor 100% zoom, bounded small-text OCR enhancement, selectable text layout, and low-confidence scattered-text retry. Already readable text retains its original recognition path.
- Installer failures now direct users to the actual error details. Regression checks cover hidden ancestors, settings migration, preview/original fidelity and small dialog text.

## 0.1.0-alpha — 2026-10-06
Initial native Windows implementation. Desktop acceptance remains a release gate; see docs/ACCEPTANCE.md.

### Reliability implementations

- Actionable Print Screen setup, fallback/conflict help and preserved pause state.
- Recoverable capture/history/settings failures, categorized feedback and truthful clipboard/OCR results.
- Detached pin storage leases, synchronized revision previews and renewed restore lifetimes.
- Complete orphan discovery, bounded metadata/history pages and conservative schema/checksum recovery.
- Fake-clock retention/transfer protection, explicit session cohorts and stale-editor/revision safeguards.
- Live history/pin updates and Q33 regression coverage; 138 automated tests and offscreen runtime probes pass. Real user/hardware acceptance remains open.

### Second ten-item pass

- Bounded content-validated image imports and repeated storage-redirection guards.
- Enforced dependency policy, immutable build Actions, native/model integrity checks and reviewed threat model; native upgrades remain a stable gate.
- Reversed/clipped region geometry, persistent monitor identity, four edge placements and explicit cursor/SDR contracts.
- Worker editor rendering/effects/crops, efficient freehand geometry, bounded undo retention, readable tools and enlarged-text toolbar checks.
- Real editor document/OCR boundary regressions and isolated computer-use diagnostics; 174 tests pass. Shared hardware/receiver acceptance remains open.

### Third ten-task pass

- Guarded OS callbacks, tray recreation/resume handling, and configured double-click capture mode.
- Named retention settings, explicit arrival animation, complete settings round trips and exact startup registration repair/rollback.
- Worker capture encoding, stage timings, latency distributions and repeatable resource stress checkpoints.
- Self-contained per-user setup, embedded checksum admission, verified staged upgrades and rollback backups.
- Derived release version, SDK/source/dependency provenance, fixed-order/timestamp ZIPs and conservative three-build artifact retention.
- 187 automated tests plus isolated installer/upgrade/retention and packaged runtime checks pass. Real login/clean-machine/performance-duration and existing hardware/receiver gates remain open.


