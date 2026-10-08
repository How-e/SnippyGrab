# Final four acceptance gates — 2026-10-07

Current acceptance: **2 required gates OPEN / 49 checked**. Q12 is CLOSED; Q34 is CLOSED by owner performance acceptance; Q44 and Q01 remain OPEN. Stable packaging remains blocked.

## Receiver acceptance — Q12 CLOSED

The owner answers the targeted native nonadjacent two-file drop request: **“PASS without needing receiver/versions”**. Record this as owner-reported PASS and an explicit waiver of receiver/version details. The request covered an unsent GitHub draft in Brave and available previously tested ChatGPT/VS Code/Discord receivers; the response does not enumerate individual receivers, versions, contents or order traces. Do not invent those identities or an independently observed native drag result.

Earlier owner Codex membership/order/delayed-paste results, reported receiver passes, independent GitHub native image/file-clipboard previews, and immutable payload/transfer regressions remain complementary evidence. Slack is not established as tested. Unsupported receiver formats and exact historical builds are compatibility limits, rather than additional requirements after the owner's scope decision.

## Workflow animation — Q44 documentation requirement satisfied

The owner requests **“create an animation rather than me making a manual recording”**. The [workflow GIF](images/capture-to-codex.gif) illustrates Print Screen → select → release/copy → paste into Codex using generated text and drawn interface shapes. Every frame labels it an illustrated workflow, synthetic content, and not a screen recording. This replaces the requested real-recording documentation artifact by explicit owner direction; the animation is not evidence of executed runtime behavior.

Generation: `scripts/create-workflow-animation.py`, Pillow; 960×540, 27 encoded frames, 271,716 bytes. All encoded frames decode; the final frame was visually inspected. No private pixels, drafts or screen recordings were read. README embeds the GIF and distinguishes it from the real WPF preview images.

Q44 stays OPEN for performance-dependent stable acceptance and the corrected hosted upload's end-to-end verification. Local package validation does not establish a GitHub release upload. No remote publication is performed by this record.

## Performance — Q34 CLOSED

Ten current-source fresh processes measured controller startup median/p95 **588.3/717.9 ms**, first native OCR **175.2/240.1 ms**, and 30 repeated OCR samples **68.0/70.1 ms**. OS/file caches were uncontrolled; this is not a certified cold-boot or tray/hotkey-readiness measurement. Ignored reports: `artifacts/final-four-startup/20261007-234139-753/summary.json`.

Existing synthetic 1080p/4K/8K distributions retain their original scope. They exclude actual hardware acquisition and OS clipboard delivery. The owner completed both real region/OS-clipboard batches and reports **“responsivness is good but one sample from the second tests was a lot higher and noticable lag 1585 ms”**. Both reports contain ten finite nonnegative samples with the expected clipboard-owner process; the slow second-batch sample belongs to Snipping Tool and is retained.

| User region workflow | Count | Median | Nearest-rank p95 / max | Min |
|---|---:|---:|---:|---:|
| SnippyGrab | 10 | 48.56 ms | 81.28 ms | 47.52 ms |
| Snipping Tool | 10 | 62.63 ms | 1585.31 ms | 46.76 ms |

These measure mouse-release-to-image-format clipboard availability with polling uncertainty, not receiver decode time. Regions varied between and within batches, so this is a normal-use comparison rather than a pixel-matched controlled speed ranking. The same host reports Windows 26300, Core i9-13900HX and RTX 4080 Laptop GPU; observed display dimensions are two 1920×1080 displays and one 1707×1067 display in the observer's coordinate context. No physical 4K/8K or exact DPI claim is inferred. The process owning every SnippyGrab sample is the existing acceptance .5 instance (source `0989076`); its controller/capture/clipboard/repository files have no differences against current source `0ece501`.

Ignored source reports: `artifacts/region-clipboard/20261007-234233-315/summary.json` (SnippyGrab) and `artifacts/region-clipboard/20261007-234317-694/summary.json` (Snipping Tool). Exact hotkey-to-overlay distributions, certified cold boot, and physical 4K/8K workflow measurements remain unmeasured. The owner explicitly selects **“Accept tested performance scope and close Q34”**. Q34 is CLOSED for this measured and accepted scope; those unmeasured cases remain limitations.

`scripts/measure-region-clipboard.ps1` passively observes user region gestures and the subsequent image-format clipboard sequence change. It does not inject input, read clipboard image contents, capture screens, change preferences, or stop processes. Only the expected application's clipboard-owner process is admitted; missing or foreign-owner changes are rejected. Polling has approximately 5 ms resolution. COLLECTED means measurement completion, not acceptance PASS. Drag dimensions are observed cursor units, not verified image dimensions. Use comparable synthetic regions, auto-copy enabled, and no unrelated clicks/copies during each batch.

```powershell
pwsh -File scripts/measure-region-clipboard.ps1 -Receiver SnippyGrab
pwsh -File scripts/measure-region-clipboard.ps1 -Receiver SnippingTool
```

Each command collects ten captures or ends after five minutes. Invoke the normal region shortcut for each capture; Snipping Tool uses Win+Shift+S. Reports remain ignored under `artifacts/region-clipboard/`. Passive Win32 entry points were loaded and checked with `-ValidateOnly`; both owner batches then exercised actual gesture/clipboard correlation. No numerical service-level guarantee is inferred from these ten samples. The owner's responsiveness acceptance is the subjective performance criterion.

## Independent verification and milestone reconciliation — Q01 OPEN

At application source `0ece501`: locked restore, warnings-as-errors Release build (zero warnings/errors), **170 core + 61 Windows tests**, formatting, dependency audit, native OCR integrity, installer/rollback/data-retention fixtures, signing fixtures without certificate access, resource-review fixtures, milestone-map and release-gate fixtures all PASS. Ignored report: `artifacts/final-four-verification/20261007-233858-216/summary.json`. Prolonged resource acceptance was not rerun.

M0–M8 retain their recorded implementation and accepted scope. M9 is accepted within the recorded Q34 scope; M10 remains conditional on Q44. Q01 must stay OPEN until those required criteria are satisfied or explicitly scoped by the owner. No checked milestone or stable label is inferred from an illustration, owner-version waiver, or successful local build alone.

## Hosted reliability diagnostic correction — 2026-10-08

Initial hosted CI at `add12d1` failed its offscreen pin-resize assertion after build, tests, formatting and policy fixtures passed. A diagnostic-only wrapper change at `124c693` exposes the synthetic report's first line in hosted logs; the repeat establishes the failure as **“Resizing a pin decodes detail beyond the old fixed 800-pixel cap failed.”** Initial CodeQL passed. Failed runs remain available: CI `37724522154` and diagnostic repeat `37724965052`.

The probe requested a 1900-DIP window and assumed the decoder must return all 2240 source pixels. Windows can constrain the actual HWND on a smaller runner desktop. The probe now calculates the expected decode detail from realized width/DPI, explicitly constrains its requested width to 1000 DIPs to reproduce this case on larger local monitors, and waits for the resize preview to match. It still requires detail beyond the old fixed cap. Production `PinWindow`/capture/clipboard behavior is unchanged. The constrained native probe, warnings-as-errors build and formatting pass locally. Hosted confirmation remains required before publication.
