# Validation — 2026-10-06

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

[Recorded  results](USER-TESTING.md#recorded-user-results--2026-10-06) establish reported real Codex focus/latest-image paste/cancellation, correct single-image drag, a three-image drop matching PiP order, generally working editor behavior and OCR. Windows Print Screen interception needed to be off. Exact build/environment versions and full checklist coverage were not supplied; these are user reports, not fresh agent verification.

## Remaining stable gates

ACCEPTANCE.md covers remaining Codex paste/drop cases and regression retests after fixes, other receivers, clipboard-lock injection, 125/175/200% scaling, HDR/vertical displays, Explorer restart, sleep/resume, display reconnection, fresh-user startup/install/remove, text scaling/high contrast and long resource stress. Hosted workflows cannot be observed before publication. Signing is not configured.

Generated reports remain in ignored artifacts or user-selected paths. Never commit desktop captures. This summarizes observed evidence, not an invented hardware PASS.

##  implementation — 2026-10-06

Close and Exit await shared editor apply/clipboard work. Apply failure retains the editor and its leases; retry/discard guidance appears in the editor. A metadata replace failure rolls back the capture record, preserving its original revision. Clipboard exhaustion reports a saved shelf image and retains the editor for retry or explicit Discard. Exit pauses capture hotkeys, waits for editors and cancels shutdown on failure.

Release build: zero warnings/errors. 53 core and 10 Windows integration tests passed on this Windows checkout. New regressions exercise pending-work sharing, clipboard false results, retry after storage failure, separate editor work and locked-history rollback using isolated storage. These are coordinator/storage checks, not fresh WPF interaction or OS clipboard-lock acceptance. Targeted manual checks: edit then close and paste; Copy then immediately close/Exit; Exit with multiple dirty editors; Discard unsaved edits. Do not damage the real cache to induce faults.

##  implementation — 2026-10-06

Unreadable primary history now loads a validated recovery sidecar on subsequent launches. Unknown old images are conservatively pinned, including when history is disabled and beyond the ordinary 2,000-orphan scan limit. Recent captures offers explicit confirmation; the original is copied to a unique archive before recovered metadata replaces it. Cleanup only resumes after successful promotion. An invalid recovery sidecar is also archived before replacement. Failed confirmation remains blocked and retryable; oversized recovered metadata cannot replace the original or enable cleanup.

Portable retest bundle: `artifacts/SnippyGrab-0.1.0-alpha.p0.20261006-win-x64` and matching ZIP. Self-contained locked-dependency publish succeeded; all 14 bundle checksums and the ZIP SHA-256 were verified. This packaging check does not establish installed or interactive runtime acceptance. Exit the previous app before launching the updated build.

## Capture overlay regression — 2026-10-06

User reported a compressed overlay with the lower live desktop exposed after launching the P0 package. The overlay positioned its HWND once in SourceInitialized, before WPF/Win32 completed size negotiation. New offscreen WPF regression checks reproduced clamping: requested 6400×1821 became 5567×1330; requested 3840×4320 became 3840×1330 in the Windows test host. Earlier overlay checks validated cursor selection coordinates without asserting the final window or rendered snapshot size, so they missed this failure.

The overlay now overrides native maximum tracking bounds and maintains its physical desktop rectangle through subsequent position/size requests. It waits for normal WPF Show rather than exposing the window from SourceInitialized. Tests assert final native bounds, DIP-to-physical layout, complete snapshot mapping and resistance to a later shrink/move request. The [Windows size-negotiation documentation](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-getminmaxinfo) describes the native tracking limits being overridden.

Release build passed with zero warnings/errors; 63 core and 13 Windows integration tests passed. A dedicated `--check-overlay-layout <absolute report path>` probe checks only offscreen synthetic windows without moving the pointer, capturing the desktop, writing the clipboard or changing focus. Development probe passed for the current virtual desktop size plus wide/tall layouts, on both offscreen sides. Observed probe DPI scale was 1.0; this does not establish every mixed-DPI layout or actual visible capture acceptance. Manual retest: full-screen freeze on every monitor, bottom-edge and cross-monitor region selection, Esc cancellation, then correct crop/paste. The reported screenshot remains untracked.

Updated portable bundle: `artifacts/SnippyGrab-0.1.0-alpha.overlayfix.20261006-win-x64` and ZIP. Locked self-contained publish passed; the packaged executable passed the four-layout offscreen probe, and all 14 bundle hashes plus the ZIP SHA-256 matched. Exit the P0 app from its tray before starting this executable. Pointer-driven capture acceptance remains pending.
