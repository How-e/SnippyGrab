# Two-hour resource acceptance — 2026-10-07

**PASS; Q35 complete for the documented two-hour synthetic workload.** Reviewed all 294 samples with the read-only report reviewer and checked the stress harness's workload and recovery sequence. No application change or repeated stress run was needed.

Evidence: user-supplied `resources-2h-20261007-121914.json`, retained outside the repository in the user's test-results folder. SHA-256: `8F78D59C40AF23D41CCC78CEF2A776C9232681BBC7EE56B927E3BE4734B571A7`. Requested 7,200 seconds; elapsed **7,201.0927556 seconds**, **7,279 captures/cycles**, **72 real local OCR runs**, final `PASS`. All completion criteria pass; resource values are numeric/nonnegative and sample time/cumulative CPU are monotonic.

The manual instructions designate portable **0.1.0-alpha.queue.20261006.3**. The report does not embed executable identity, Windows version or competing workloads, so those are not independently established by this JSON. The previously recorded host was Windows 11 Home build 26300; this review does not remeasure the test environment. `PASS` shows the workload reached its final report; cleanup outcome and the absence of every visible freeze are not separate report fields.

| Phase | Seconds | Captures | Working MiB | Private MiB | Managed MiB | Process handles | GDI | User |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Initial | 0.80 | 0 | 101.01 | 46.42 | 3.77 | 481 | 17 | 19 |
| After warm-up | 109.31 | 400 | 170.82 | 100.28 | 3.09 | 711 | 56 | 28 |
| Middle | 3,605.74 | 3,800 | 194.52 | 120.14 | 6.12 | 641 | 52 | 27 |
| Last workload checkpoint | 7,195.85 | 7,275 | 196.09 | 121.78 | 7.27 | 641 | 56 | 27 |
| Post-GC/finalizers | 7,201.08 | 7,279 | 192.25 | 118.09 | 5.06 | 644 | 56 | 27 |
| Controller/dock disposed | 7,201.09 | 7,279 | 192.34 | 118.14 | 5.06 | 642 | 53 | 23 |

Process handles peak at 721 and finish at 642; they do not accumulate with thousands of pin/effect cycles. GDI handles repeatedly cycle within a maximum of 68 and finish at 53; user handles peak at 32 and finish at 23. Disposal reduces GDI/user handles from 56/27 to 53/23. Recovery does not imply a return to cold startup: WPF/native runtime resources and the repository remain reachable at this immediate checkpoint.

Memory increases modestly over the run. Mean working/private/managed MiB across successive periods are 178.59/106.35/4.94 (100–1,800 seconds), 183.47/110.01/5.35, 187.80/113.91/6.43 and 190.28/116.51/6.53 (last half-hour). Peak working/private/managed memory is 199.03/124.83/8.94 MiB. Post-GC managed memory is 5.06 MiB, below the last workload checkpoint's 7.27 MiB. Retention=never intentionally preserves all 7,279 captures and their in-memory repository metadata; this is consistent with modest growth and recovering transient allocations, not proof that every allocation is leak-free. No accelerating memory or continually growing handle pattern warrants rejecting this two-hour workload.

After warm-up, interval-weighted CPU averages across those periods are **1.30%, 1.40%, 1.64%, 1.84% of one logical core**. This is workload CPU, not tray-idle CPU. Regular 25-cycle checkpoint intervals span 25.54–26.20 seconds, with no sampled long stall. The modest CPU increase accompanies a growing repository; it does not close end-to-end latency or idle-performance acceptance.

The harness covers synthetic 640×360 storage/history=never, hidden dock rebuilds, worker blur/revision rendering, three offscreen pins per 25 cycles and real local OCR every 100 cycles. It does not cover actual editor gestures/undo sessions, desktop acquisition, OS clipboard, receiver delivery or all-day use. Existing bounded-cache/undo implementation checks remain complementary evidence. Q34 latency comparisons, interactive history/editor/pin acceptance and hardware/receiver/OS gates retain their own criteria.

Q35 clearance completes the pending report/trend review and enables current queue/milestone/release-status reconciliation. Q01/Q44 still depend on other open gates; no separate implementation task was recorded as waiting solely on Q35. Stable packaging remains blocked by **35 required open entries**, with **16 checked**. No package, tag or publication is required by this clearance.

Validation: existing resource-review completion/incompletion, CPU/phase, malformed-report and read-only CLI fixtures pass. All ten release-gate fixtures pass; the actual updated queue admits alpha and rejects stable with the remaining 35 gates. Git whitespace validation passes and the original report hash remains unchanged. Application code and binaries are unchanged.
