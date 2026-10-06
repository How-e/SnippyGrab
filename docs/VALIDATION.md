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

## Pending stable gates

ACCEPTANCE.md covers real Codex paste/drop, other receivers, clipboard-lock injection, 125/175/200% scaling, HDR/vertical displays, Explorer restart, sleep/resume, display reconnection, fresh-user startup/install/remove, text scaling/high contrast and long resource stress. Hosted workflows cannot be observed before publication. Signing is not configured.

Generated reports remain in ignored artifacts or user-selected paths. Never commit desktop captures. This summarizes observed evidence, not an invented hardware PASS.
