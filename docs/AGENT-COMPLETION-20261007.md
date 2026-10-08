# Agent completion review — 2026-10-07

Reviewed all 51 queue entries, milestone/closure records, user findings, release admission and current source. **34 required gates remain open; 17 entries are checked.** Implementations are committed; gestures, receivers, hardware and clean-OS acceptance retain their evidence requirements. Q35's accepted two-hour report stands; no prolonged workload was repeated.

## Separate commits

| Commit | Completed work | Evidence |
|---|---|---|
| `9f40b29` | Diagnostic Settings cannot write Windows startup, enable global hotkeys through pause restoration/resume or redirect its temporary cache. Startup/cache controls are disabled. | Native reliability probe applies a startup-enabled draft, checks unchanged OS registration/forced pause and rejects an alternate cache. Existing editor/recovery/clipboard assertions pass. |
| `c2810a4` | Distinct CAPTURE 01–20 images and build identity in the interaction fixture. | Release build passes; actual fixture/gesture retest remains below. |
| `85a0bc5` | Fresh-process startup and first/repeated native OCR distributions. | Ten processes, forty real OCR calls; every call must recognize exact synthetic CS1002 text. |
| `d4da19f` | One local verification command, strict native exit/report checks, matching-build acceptance launcher; milestone/diagnostic regressions added to CI. | All 24 local stages PASS at this exact revision with a clean starting tree. New hosted CI execution remains pending. |
| `003e2fd` | Launcher also rejects uncommitted working changes and offers read-only validation. | Final package validation exercises the current launcher before launch. |

Local ignored report: `artifacts/agent-verification/20261007-155124-794/summary.json`, source `d4da19f`, Dirty=false. Locked restore, zero-warning warnings-as-errors build, **170 core + 61 Windows tests**, formatting, policy fixtures, dependency/native integrity and whitespace pass. Native reliability, four overlay layouts, six editor layouts, OCR corpus, 64 dock layouts, pipeline/editor measurements and isolated transfer crash/restart pass. Crash evidence covers immutable delayed reads at fake 23h and cleanup at 24h while retaining pins.

The earlier report in `artifacts/agent-verification/20261007-154457-797` stays **FAIL**: dock structure passed, then the pointer-position invariant failed. The second run passed that assertion too. Neither establishes visible hover or receiver acceptance. Independent checks now continue after failure, while any failure still prevents an overall PASS. Stale/structural-only reports cannot satisfy the native runner.

## Q34 measurements

Windows 11 build 26300, x64, Intel Core i9-13900HX, 32 logical processors, approximately 31.8 GiB memory. Ten sequential fresh processes with uncontrolled OS/file caches:

| Measurement | Samples | Median | p95 |
|---|---:|---:|---:|
| Process creation to isolated controller ready | 10 | 611.0 ms | 657.8 ms |
| First native OCR per process | 10 | 185.4 ms | 217.7 ms |
| Subsequent native OCR | 30 | 69.8 ms | 71.7 ms |

First observed startup: 657.8 ms. The OS process timestamp includes CLR startup and isolated controller construction, excluding normal visible tray/global-hotkey readiness. OCR includes model validation/engine setup and checks recognized text without storing it in reports. These are not certified cold-boot measurements or agreed performance targets. Actual selection-to-OS-clipboard distributions and a same-machine Snipping Tool comparison remain Q34 acceptance.

Repeat with `pwsh ./scripts/verify-agent-work.ps1 -NativeChecks`; keep the desktop idle for pointer/focus invariants. It runs short isolated probes, not prolonged stress or the pointer-moving legacy self-test. Startup alone: `pwsh ./scripts/measure-startup.ps1`. Reports/logs stay in ignored artifacts.

## Remaining ownership

| Gates | Remaining evidence |
|---|---|
| Q02–Q05, Q07, Q13–Q14, Q22, Q28–Q29 | Native recovery/retry/Discard, multiple-editor Exit, pin/history/settings and real transfer/clipboard actions. Agent fault injection and isolated process-crash evidence pass. |
| Q15–Q16, Q18–Q21, Q32, Q49 | Actual hover, reachability, reorder, keyboard, tools/export dialogs, themes and assistive technology. Previously reported dock failures make this the immediate retest. |
| Q08–Q12, Q24–Q27, Q30–Q31, Q41, Q50 | Capture/hotkeys/lifecycle, exact receiver versions/count/content/order/delayed reads, arbitrary synthetic OCR, monitor/DPI/HDR and clean Windows/login environments. Previous ordinary receiver successes stand. |
| Q34 | Real workflow/comparison and controlled cold/warm definitions; fresh-process/OCR and synthetic pipeline distributions are now available. |
| Q01/Q44 | Required acceptance closure or explicit scope decisions, receiver GIF and corrected next-release publication. |

Native input automation is unavailable in this session: the enabled computer surface disables native apps, and no Computer Use node runtime tool is exposed. Programmatic WPF checks remain distinct from actual gestures. No scope waiver, user-cache corruption, OS preference change, account/hardware/receiver installation, push, tag or publication occurred. Available local build/process/filesystem work is complete for this review.

## Most important next step: retest the reported dock failures

Use local self-contained **0.1.0-alpha.acceptance.20261007.1**, prepared from final committed source. The published `.3` release predates these changes. This candidate is local and unsigned. The launcher checks every bundle hash, matching Git revision and committed source.

From repository PowerShell:

```powershell
pwsh ./scripts/start-acceptance.ps1
```

An isolated fixture opens with numbered synthetic captures, disabled global hotkeys and intercepted clipboard. Normal history and Windows startup remain untouched. Allow ten minutes:

1. Click **1 captures**, **3 captures**, **5 captures**, then **20 captures**. At each count, enter the shelf, reach the furthest visible card, leave and re-enter five times. Report jumps, jitter, premature collapse, freezes or unreachable controls, distinguishing first/later entry. Wheel through all twenty items.
2. With five visible captures, Ctrl-click numbers 1 and 3. Leave/re-enter. Selection should persist and displayed cards should remain reachable with predictable collapse.
3. Clear selection with Escape. Alt-drag item 1 onto item 2; it should become number 2. Move the same image back onto number 1. Repeat 1→3 and 3→1. CAPTURE labels identify images; shelf badges show current order. Dragging must not open an editor.
4. Click **Focus shelf** or a numbered badge. Use arrows, Home/End and Space. Enter should open the focused capture. Annotate, **Apply + copy**, close, reopen and confirm the edit. Copy is intercepted here. **Export PNG…** should create a file at the displayed path. Finally select a disposable capture and press Delete; only the intended selected set should disappear.
5. Click **Exit checks**. Reply with version, Windows build, monitor scaling, PASS/FAIL for steps 1–4 and exact failure/count/direction. The launcher prints the local report path. Text feedback suffices; no private screenshots/logs are needed.

This resolves the reported blocker before the full receiver/hardware matrix. If it passes, the next receiver test is `pwsh ./scripts/start-acceptance.ps1 -RealApp`, after exiting other SnippyGrab instances. That mode uses normal hotkeys/history/clipboard: capture distinct synthetic A/B/C content and test nonadjacent exact membership/order plus a one-minute delayed unsent attachment in the installed Codex version. Previous ordinary paste/drop successes need not be broadly repeated now.
