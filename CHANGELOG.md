# Changelog

## Unreleased — local improvements, 2026-10-09

- Added actionable OCR readiness checks and timestamp/publisher verification tooling; real publisher signing still requires external identity/access.
- Added explicit monitor capture, shared PNG/JPEG/WebP export, ordered cancellable batch export, and native-pixel strip/grid composition.
- Added bounded user-assisted static vertical scrolling with seam correction/undo and private frame staging. No automatic scrolling or recording.
- Added pinned WebP source/recipe/binary provenance and codec/license review. Local synthetic/offscreen verification does not close physical, receiver or clean-profile acceptance gates. These changes are not yet published.

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


