# P0 acceptance — 2026-10-07

**Q02 PASS / CLOSED. Q03 PASS / CLOSED.** The current queue has **32 required gates open and 19 entries checked**. Stable remains blocked. This record supersedes the open Q02/Q03 acceptance statements in earlier dated reviews; it does not broaden receiver, hardware, accessibility or release acceptance.

## Environment and provenance

Windows 11 build 26300, x64, .NET SDK 10.0.400. Development Release executable from source `bda4571` plus the new acceptance instrumentation, committed as `751ce75`. Native observations were collected before the instrumentation commit, so their displayed build metadata identifies the base revision, not a clean tagged package. The launcher records source/dirty status and binary hashes for repeat runs. No new package or release is claimed.

The opt-in `--p0-acceptance` lane creates synthetic 720×360 captures in a marked temporary root. It uses the production repository, Recent captures confirmation dialog, controller-registered editors, actual tray Exit, and real OS clipboard. Global hotkeys, normal settings/cache and Windows startup remain isolated. The explicitly identified fixture tray icon is visible; no unrelated SnippyGrab instance was exited. Windows automation supplied native input and observed rendered windows; programmatically prepared pending edits are identified below. Reports stay ignored under `artifacts/p0-acceptance/`; no screenshots, OCR text or raw user paths are tracked.

## Q02 — failure-safe editor close and Exit

| Case | Evidence and result |
|---|---|
| Native close with actual locked metadata | Two registered editors were prepared with pending redaction edits. An exclusive replacement-blocking handle held only the fixture's history file. Agent pressed Escape in the actual editor. It stayed open with actionable retry/Discard text; both original PNG hashes stayed unchanged and both registered editors remained. PASS. |
| Actual tray Exit while apply cannot persist | Owner clicked Exit on the fixture's tray icon. Exit was cancelled; two registered editors and both original files remained, `Exiting=false`. PASS; user action plus independent report verification. |
| Real Windows clipboard contention | A bounded worker opened the OS clipboard exclusively. After releasing the metadata handle, native editor close saved the revision but exhausted real clipboard retries. The editor stayed open and visibly said edits were saved and offered retry/Discard. PASS. The lock released automatically at 45 seconds. |
| Retry after contention | Agent retried native close after release. It closed, with the same saved revision filename/hash rather than a duplicate apply. The real OS bitmap RGB matched that capture; the independently decoded clipboard PNG matched every pixel including alpha. PASS. |
| Explicit Discard of other pending edit | Owner clicked Discard in the other synthetic editor. It closed, its original file hash remained unchanged and its Edited flag stayed false. Clipboard RGB still matched the previously applied other capture. PASS. |
| Saved revision across process restart | Same isolated root reopened in a new process; original/edited flags and hashes were preserved. Real clipboard bitmap RGB and full PNG independently matched the edited capture. PASS. |
| Successful actual tray Exit with multiple dirty editors | Both registered editors again received pending edits. Owner clicked actual tray Exit with no locks. Both became Edited, both files existed, registered editor count reached zero, `Exiting=true`, final Exit event was recorded and the fixture PID disappeared. PASS. |
| Exit during pending worker operation | Native input invoked the fixture control that starts production `StartDocumentEdit(Blur)` then immediately calls production `Exit`, before awaiting work. With two dirty registered editors, Exit waited for pending work, persisted both revisions, closed both windows and exited. The worker-start and application-Exit events bracket the operation; final files exist and both Edited flags are true. PASS. This covers the concurrent worker/Exit path; it is not a claim of timed simultaneous human gestures. |

The first strict BGRA clipboard comparison failed on alpha values for 2,368 text pixels while **all RGB bytes matched**. The fixture now checks bitmap RGB and the separate lossless PNG including alpha. This changes the diagnostic assertion, not production clipboard behavior. The observation is retained as a representation limit; receiver transparency/rendering support remains Q12/Q30, not a claim from this gate.

## Q03 — corrupt-history recovery

| Case | Evidence and result |
|---|---|
| Actual warning and Cancel | Agent opened Recent captures and observed guidance about possible old pins, disabled cleanup and preserving the unreadable original. Native Cancel dismissed confirmation without promotion: `CleanupBlocked=true`, original text unchanged, zero backups, both unknown captures pinned. PASS. |
| Actual failed promotion | A replacement-blocking file handle held the isolated corrupt original. Agent chose OK in the native dialog. Recovery sidecar persisted but promotion failed; actionable access-denied/retry feedback was visible in the fixture's rendering of production Last operation details. Recovery warning/action remained available, cleanup stayed blocked and original text/pins/files stayed intact. PASS. |
| Restart after failed promotion | New process loaded the same isolated root. Cleanup remained blocked; IDs, pin flags and pixel-file hashes matched the failed state; production startup guidance instructed recovery. PASS. |
| Actual retry confirmation | After restart released the old process handle, agent chose OK in the native confirmation. Warning/action disappeared and cleanup became enabled. Both pins/files stayed unchanged. Preserved invalid originals matched the exact initial unreadable content hash. PASS. |
| Third launch after successful recovery | New process loaded the recovered state. Cleanup remained enabled, both pin IDs/flags/files matched, and actual Recent captures showed no recovery warning/action. PASS. |
| Corrupt/truncated/oversized/newer schemas and failure boundaries | All 11 HistoryRecoveryTests pass, including multiple-launch new-pin/transfer protection, conservative cleanup, preservation, failed promotion and retry. Native UI checks above supplement rather than replace these independent storage assertions. PASS. |

An earlier modal target became ambiguous during the first fixture. Its intended Cancel was not counted as PASS. The complete Cancel/failure/restart/retry sequence above used a fresh fixture and screenshot-derived native modal actions.

## Repeating and verification

Build Release, then run `pwsh ./scripts/start-p0-acceptance.ps1`. To reopen a test root, pass its reported Root with `-Root` and a new `-ReportName`. Never pass normal application data. Reports are intentionally retained locally; the harness does not delete fixture caches or mark gates itself. Apply/copy replaces the OS clipboard with synthetic pixels. History locks are limited to the fixture; OS clipboard locks auto-release after 45 seconds or on fixture exit.

Warnings-as-errors Release build: zero warnings/errors. **170 core + 61 Windows integration tests PASS**, zero failures/skips. Focused EditorCommit/HistoryRecovery tests: **17 PASS**. Full format and Git whitespace verification PASS. Milestone-map verification passes with 32 open gates mapped to owners and five malformed fixtures rejected. All ten release-policy fixtures pass; prerelease admission remains available and stable is blocked. No prolonged workload, OS preference change, clean-account creation, remote push, tag or publication occurred.

## Local evidence digests

| Report in artifacts/p0-acceptance | SHA-256 |
|---|---|
| q02-user-tray-exit-blocked.json | FA7F4D47C519E093FFBCD3AB532D2DC80C3C26ADB1A110E0689A8E8B304F7786 |
| q02-real-clipboard-exhausted-final.json | B369788A5A80871443DA5274AA667999DBD2AB7FB84537C4C82D2B0F2BEE7A1A |
| q02-user-discard.json | 02D3C2A4883CDDBBE7BC312FBBE5A47A15D8EE88CA213A629C6F38B75E077E14 |
| q02-restart.json | EDFF778F8A7697A02B9FB380328C16208B2536B333149288ED7FA6C55CDC29A2 |
| q02-concurrent-exit.json | 6AA5B48AB88AA74986D066A5AA295C521B9B74086436CB9881B4EE6E46F3F303 |
| q03-cancel-verified.json | C2B4403C633DDB5708600F172A1A86BB4667BB876A230FADC9C999C64E54FB37 |
| q03-promotion-failed.json | 17CEFFB3164B192999469D2DEDEFA8BEC4B3CE17AB357C19B50FDF043F92002B |
| q03-confirmed.json | 3D7FE105425379946C73A83293397660D1D7A45D57F3302ACDD204C8F04A6D79 |
| q03-success-restart.json | 9F058550C97D81B911712F5874A81EFADB45B6711EDD7F932603590CCC4D1FF3 |
