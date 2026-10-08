# Acceptance continuation — 2026-10-07

Current status: **10 required gates OPEN, 41 entries CLOSED**. Stable packaging remains blocked. Historical counts in earlier reports describe their own snapshots.

## Owner acceptance and explicit scope decision

Owner reports **F–I all PASS**, then responds to requests for conditional OCR cancellation, contrast-theme/Narrator and exact monitor/text-scale details: **“just remove these gates, they work”**. Treat this as acceptance and an explicit decision to remove those remaining detail requirements, rather than claim independently observed results for unspecified conditional cases. Preserve the existing running acceptance .5 application; no rebuild is needed.

| Closed gate | Evidence and closure basis |
|---|---|
| Q08 | A–E and H owner capture-mode, cancellation, reversed/tiny regions, partly offscreen/maximized windows, rapid captures and available display-edge workflow PASS; omitted hardware details accepted by owner. |
| Q10 | H available-layout owner PASS plus existing monitor identity/placement regressions; untested DPI percentages, vertical topology and reconnect matrix removed from this acceptance requirement by owner. |
| Q11 | B/H cursor owner PASS and independent hotspot/negative-coordinate tests; GDI SDR contract remains explicit. No verified HDR fidelity claim. |
| Q14 | A/G real OCR/copy owner PASS, existing P0 real OS clipboard exhaustion/retry PASS, format-contention and generation/cancellation regressions; precise close-while-busy trace waived by owner. |
| Q19/Q32 | Previously recorded keyboard capture/copy/export/pin/history/settings and Light/Dark/System interaction PASS, I owner accessibility PASS. Exact contrast/Narrator/text-scale configuration is unspecified and its additional detail requirement is waived. |
| Q24 | Full/area synthetic text, repeats and G large/blank OCR owner PASS, native corpus/input/model/loader/cancellation regressions. Conditional cancellation detail waived; recognized text accuracy beyond tested content is not guaranteed. |
| Q26 | Earlier settings/navigation/validation/dock tests and F changed theme/width/default-mode/start-silently persistence owner PASS, full-property round-trip and schema/default/invalid-value regressions. Remaining field-by-field/reset details accepted by owner; login registration remains Q27. |
| Q29 | Prior live History/dismiss/restore and pin revision/lease/gestures PASS, F persisted pin image/timestamp/geometry/opacity owner PASS; wider optional-history/mixed-DPI details accepted by owner. |
| Q31 | H available hardware owner PASS, with unavailable expanded lab matrix accepted as a scope limitation. This does not establish universal Windows/DPI/HDR compatibility. |

These entries remain present in the queue because release policy validates all 51 IDs. Checked entries represent owner acceptance/scope closure where stated, rather than deletion of history or fabricated test evidence.

## Independent verification this continuation

At source `7aca27d`, application source remains the tested .5 candidate source. Fresh Release tests using existing compiled binaries: **15 core + 24 Windows tests PASS**, zero failures/skips. Core checks cover clipboard retries/supersession/cancellation, settings round trips/schema recovery, storage failures and payload-safe failure categories. Windows checks cover clipboard formats/contention and OCR boundaries/native parsing/model/error/cancellation. TRX reports remain ignored under `artifacts/acceptance-continuation-20261007/`. These tests do not establish actual UI gestures or exact assistive-technology configuration.

## Remaining gates and concrete next work

| Gate | Remaining requirement |
|---|---|
| Q07 | Isolated real settings/storage/cache-fallback failure feedback and retry. Injected production salvage and rollback regressions already pass; do not damage normal cache or fill the disk. |
| Q09 | Actual other-owner/fallback conflict and first-run Print Screen unavailable path. Existing normal fallback/pause/held-key/help owner passes stand. |
| Q12 | Remaining available receiver identity/version/format matrix, including GitHub availability. Existing Codex/ChatGPT/VS Code/Discord/Explorer passes stand. |
| Q13 | Native transfer with clear/expiry/session-only exit/crash and an external consumer reading retained paths later. Fake-clock/isolated crash checks pass; do not clear the owner's normal history to create faults. |
| Q25 | Explorer tray restoration, sleep/resume hotkeys and display-change lifecycle. F normal silent restart PASS does not exercise these events. |
| Q27 | Real login launch and fresh-profile setup/startup/uninstall after upgrade; isolated registration/rollback tests pass. |
| Q28 (CLOSED) | Failure categorization/privacy, native readable Settings denial/retry and earlier real editor/recovery/clipboard failure interactions satisfy its stated feedback/recovery criteria. Native shell notification appearance remains a Q25 observation limit. |
| Q34 | Actual end-to-end capture/OS clipboard distributions and same-machine Snipping Tool comparison; earlier startup/OCR/synthetic distributions remain valid. |
| Q41 | Clean Windows 10/11 profiles without .NET, install/upgrade/Apps uninstall and data preservation. Requires an available spare environment. |
| Q44 | Privacy-safe real capture-to-Codex GIF, current support/limitations and stable closure. Next publication must independently validate the corrected upload path; no publication authorized here. |
| Q01 | Reconcile final milestone acceptance after the remaining requirements are satisfied or explicitly scoped. |

## Next owner batch J–L

Use the existing running .5 application and synthetic captures; report PASS/FAIL/NOT AVAILABLE and exceptions separately.

**J — Q25 OS lifecycle:** Close History/Settings/Editor: utility stays in tray. Pause/resume and double-click tray capture still work. Save unrelated work, use a short normal sleep/resume, then capture a synthetic region with the existing fallback shortcut: one overlay, correct capture/copy and no stuck keys. If convenient, manually restart Windows Explorer through Task Manager; expect the SnippyGrab tray icon to return and capture/settings actions to work. Report sleep and Explorer separately. Do not sign out during unsaved work or disconnect displays solely for this batch.

**K — Q12 receiver record:** Supply versions of already passed ChatGPT, VS Code, Discord and Windows/Explorer where visible. If GitHub is available, use an unsent synthetic issue draft for one-image paste and two selected-file drop; record exact contents/count/order or unsupported format, then discard without submitting. Otherwise report GitHub NOT AVAILABLE. Passed receiver tests need no repeat merely to obtain versions.

**L — Q27/Q41 environment availability:** Report whether a spare clean Windows 10 22H2 or Windows 11 account/VM/machine without .NET is available, and whether actual sign-out/login testing is practical. Availability alone is not PASS. Do not create an account/install a VM/change startup registration just to answer this question; the install/login procedure will target the available environment.

No prolonged workload, rebuild, remote push, tag, publication, automatic OS preference change or new software installation was performed by this continuation.

## Agent takeover of J–K and native failure acceptance

The owner asks the agent to perform J–K, aside from the spare VM, and explicitly selects **Brave**. The earlier J–L handoff above is superseded: receiver version collection, the available GitHub tests and Explorer restart were performed by the agent. Only the shell observation, actual sleep/resume and spare clean-environment availability need owner input.

| Check | Result and evidence boundary |
|---|---|
| Installed receiver identities | VS Code executable and active package both 1.140.0 (uninstall registry is stale at 1.136.1); Discord standard 1.0.9259, with PTB 1.0.1090 also installed. Explorer executable 10.0.26100.8117; actual Windows build 26300.9457 / 26H2. Brave executable ProductVersion 154.1.96.61. OpenAI.Codex package 26.1002.7124.0 owns the current window titled ChatGPT; this does not independently identify the historical separately reported ChatGPT receiver version. Installed versions are current observations, not proof of the exact receiver build used in earlier owner tests. |
| GitHub native image paste | PASS in authenticated Brave. Actual input clicked Apply + copy in the isolated P0 production editor, then native Ctrl+V into the unsent GitHub issue description. The preview rendered P0 SYNTHETIC 2 correctly at 720×360. The browser tool's virtual clipboard could not consume the OS clipboard; native Windows input supplied the real paste. |
| GitHub two-file selection | PASS through the native Windows file picker, choosing only the two known synthetic fixture PNGs. The extension's file-chooser API required additional file-URL permission; that permission was left unchanged. Native picker selection succeeded. The draft insertion initially split the previous image markup at the existing caret; the two successfully uploaded links were isolated for their preview. This is picker/file-format acceptance, not native SnippyGrab OLE drag. |
| GitHub real file clipboard | PASS. Native History selection and Copy published exactly two CF_HDROP paths, independently read from the OS clipboard in their supplied order. Native Ctrl+V into an empty GitHub draft accepted both files. Both previews decoded at 720×360 and rendered P0 SYNTHETIC 2 followed by P0 SYNTHETIC 1. The synthetic fixture exited normally while the consumer finished ingesting the paths. This does not establish clear/expiry, unpinned session-only cleanup, deferred post-ingest reads or native drag. |
| Explorer restart | PARTIAL. Agent terminated the actual shell process 14288; Windows recreated it as 20724. Both normal and disposable SnippyGrab processes survived. The Windows helper does not expose the taskbar as a target; Task Manager has higher integrity and its controls are inaccessible to the helper. Native tray-icon/menu observation remains unverified. No OS security/permission setting was changed. |
| Real Settings storage failure/retry | PASS. A separate existing-build interaction fixture used its own marked temporary root. Opening only its settings.json Read with FileShare.Read prevented atomic replacement. Actual Apply kept Settings open and visibly displayed: “Settings could not be applied. Access was denied. Check folder permissions or Windows startup access, then retry.” The lock auto-released after 45 seconds; actual Apply then succeeded and the Settings window closed. Custom cache paths/startup registration are disabled in this lane and are not claimed. Q07's custom-cache fallback remains open. |

**Q28 PASS / CLOSED** for its stated category/privacy/actionable-feedback/usability criteria: current OperationFailure tests and existing diagnostic privacy/source review, the native Settings denial/retry above, earlier P0 actual storage/clipboard/editor/recovery errors and owner OCR/validation/help checks provide complementary evidence. No blanket claim that every possible failure permutation or shell-rendered balloon was exercised; actual Explorer tray behavior remains Q25. **10 required gates remain open / 41 checked**.

Local ignored evidence: github-native.json, settings-fault.json, settings-failure-verification.json, explorer-restart.json, and rendered synthetic GitHub preview PNGs under artifacts/acceptance-continuation-20261007/. No signed attachment URLs, personal screenshot bytes, private OCR text or raw user paths are copied into tracked evidence. GitHub uploaded only the generated fixture pixels; no issue/comment was submitted. The test draft was cleared/cancelled and its Brave tab closed. Disposable SnippyGrab fixtures exited normally; the original running .5 application is preserved. Q12 remains open for missing native receiver/drop and historical exact-version details; Q13's wider transfer/cleanup lifecycle remains separate.

The outstanding owner prompt now requests only normal tray-icon/menu observation after the completed Explorer restart, a short actual sleep/resume capture/copy check, and availability of a clean Windows 10/11 environment. No repeat of F–I, Explorer restart or receiver-version collection is requested.
