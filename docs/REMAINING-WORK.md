# Remaining acceptance review — 2026-10-07

Reviewed clean `main` at `ff0db0a` against the queue, implementation ledger and live GitHub APIs. **35 required gates remain open; 16 entries are checked.** Q36 and Q35 are now closed. The earlier 37-gate local acceptance snapshot remains historical. No new application defect was established by this review; the unchecked implementation descriptions should be read with their closure records.

## Work already completed

- Remote: [How-e/SnippyGrab](https://github.com/How-e/SnippyGrab). Hosted PR [CI](https://github.com/How-e/SnippyGrab/actions/runs/37654596735) and [CodeQL](https://github.com/How-e/SnippyGrab/actions/runs/37654596738) passed at `d08a5d7`, the reviewed PR head preceding the final dependency merge. Hosted main [CI](https://github.com/How-e/SnippyGrab/actions/runs/37654173525) and [CodeQL](https://github.com/How-e/SnippyGrab/actions/runs/37654173640) passed at `4c8820b`. These are exact observed revisions; no checks were returned for `ff0db0a` itself.
- Main protection requires up-to-date `build` and `analyze`, applies to administrators, and forbids force pushes/deletion. Active [release-tag rules](https://github.com/How-e/SnippyGrab/rules/24629728) restrict creation/update/deletion of `v*` tags to administrator bypass.
- Private vulnerability reporting, Dependabot security updates, secret scanning and push protection are enabled. Live API queries returned zero open code-scanning, secret-scanning and Dependabot alerts. This is a point-in-time service result, not proof of absence of vulnerabilities.
- Exactly one [published prerelease](https://github.com/How-e/SnippyGrab/releases/tag/v0.1.0-alpha.queue.20261006.3) exists, with ZIP, setup and two checksum files. The [tagged release run](https://github.com/How-e/SnippyGrab/actions/runs/37575994683) passed build/tests, security and boundary fixtures, clean-tag packaging and verification, then failed at upload. Manual publication recovered it. [PR #8](https://github.com/How-e/SnippyGrab/pull/8) corrected the PowerShell argument array. A subsequent successful end-to-end automated publish has **not** been observed; retain this follow-up under Q44 for the next authorized release.
- User-reported Codex/ChatGPT/VS Code/browser drop/paste successes are already recorded. They establish ordinary reported use, while exact-version, membership/order and delayed-read coverage remains incomplete.

## Available work completed in this review

Closed Q36 with the evidence above, corrected current remote/publication/receiver statements and linked this review from the queue and milestone ledger. Historical dated reports retain their original results. Added `scripts/review-resource-report.ps1`, a read-only reviewer for the existing stress-report format, with malformed/incomplete-report, CPU-rate and recovery-phase fixtures. It reports interval CPU as percent of one core, workload memory/handle changes and separate post-GC/disposal samples. It never changes a report, closes a gate or assigns a leak verdict.

The completed user-run report has now been reviewed: **Q35 PASS**, 7,201.09 seconds, 7,279 cycles and 72 OCR runs. All 294 samples show bounded handles, modest memory growth with retained history metadata and managed-memory recovery after GC. See [the full resource review](RESOURCE-REVIEW-20261007.md). Current queue, milestone, validation and publication status are reconciled. No additional implementation task was recorded as depending solely on this clearance; Q01/Q44 still require other gates. No stress workload was rerun.

## What still needs evidence

| Tasks | Remaining work and owner |
|---|---|
| Resource follow-up | Q35 two-hour acceptance is complete. All-day behavior and actual editor/idle usage remain unverified limits; no automatic prolonged rerun is scheduled. |
| Q02–Q07, Q13–Q14, Q22, Q24, Q28–Q29 | Existing fixes and isolated regressions pass historically; failed-apply/multiple-editor Exit, recovery UI, pin/history/clipboard/OCR/storage failure and actual transfer/crash acceptance still need a coordinated isolated interactive pass. Agent/shared. |
| Q15–Q16, Q18–Q21, Q32, Q49 | Dock repeated cold/warm hover, selection leave/re-entry, symmetric Alt-reorder, full keyboard/selection traversal, editor/export failure/dialog and accessibility checks. Existing short UI passes are partial. Shared. |
| Q08–Q11, Q25–Q27, Q31, Q41 | Available hardware/hotkey modes, reconnect/DPI/HDR, Explorer/sleep/login and clean Windows 10/11 installation/uninstall. Environment/user evidence required; no new OS/account/hardware changes in this review. |
| Q12, Q30, Q50 | Exact build/receiver versions, nonadjacent selection, exact count/content/order after reorder and delayed receiver reads. User/shared. |
| Q34 | Cold/warm end-to-end startup/capture/OS clipboard/OCR distributions and same-machine Snipping Tool comparison. Run separately from the stress workload. Shared. |
| Q01, Q44 | Consolidate final acceptance only after required gates close or explicit scope decisions. Privacy-safe capture-to-Codex GIF remains deferred; stable remains blocked. Next authorized tag should verify corrected automated publishing. |

Unsigned/SmartScreen guidance remains accurate. Q48's optional signing hook is complete; actual certificate provisioning is not an unfinished mandatory task. Q45–Q48 optional features/decisions and Q51 native remediation are already closed. No new release or scope waiver is implied by this review.
