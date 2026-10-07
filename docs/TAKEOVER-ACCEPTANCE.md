# Local acceptance record — 2026-10-07

This is a short verification pass, not stable acceptance. **37 gates remain open, 14 are checked.** PASS applies only to the stated check. Current queue and milestone documents remain tracked because the release parser requires the queue. Earlier dated evidence is historical.

## Environment and boundaries

Tested portable build: **0.1.0-alpha.queue.20261006.3**, original package source `212267c`. Source verification started at `90cc5b4` on main with a clean tree, 59 commits and no remote. The authorized privacy rewrite changes source commit identities; existing ignored packages retain their original provenance and were not relabeled or published.

Windows 11 Home, 10.0.26300 / build 26300, x64; .NET SDK 10.0.400; NVIDIA GeForce RTX 4080 Laptop GPU. Connected displays: 1920×1080 at (0,0), 96 DPI; 1920×1080 at (-1920,9), 96 DPI; 2560×1600 at (1920,-221), 144 DPI. HDR state and Windows text scaling were not measured. No scaling, topology, Windows preference, registry, startup, installed application, normal cache or personal setting was changed.

The installed Windows computer-use skill supplied real input to the existing opt-in isolated interaction fixture. It used 20 synthetic 720×360 captures, disabled global hotkeys and intercepted clipboard writes. Actual WPF windows and the native export dialog were used. The tool activates its target window, so this does not prove ordinary nonactivating hover or composer focus retention. Capture buttons were not used: full desktop/window screenshots could include unrelated private content. No evidence images, OCR text, cache, settings or raw logs were added to Git. Preview images and the deferred GIF were untouched.

## Real interaction results

| Task IDs | Result | Observed check and remaining limit |
|---|---|---|
| Q02, Q20, Q22, Q32 | PASS | Drew an arrow on the first synthetic capture; Escape closed the editor, committed Edited state and reached the image sink. Reopening through shelf Enter showed the annotation. No failed-apply, multiple-dirty-editor Exit or persistence-across-restart UX was tested. |
| Q02 | PASS | Drew a separate arrow on the second capture and chose Discard. It closed with Edited=false and no added clipboard operation. Normal isolated Exit completed after the checks. |
| Q03 | NOT AVAILABLE | No naturally occurring recovery UI was encountered in the isolated lane. Normal private history was not opened or corrupted. Isolated automated recovery regressions remain complementary evidence. |
| Q49, Q16, Q18, Q32 | PASS | Observed compact/expanded native shelves with 1/3/5/20 captures. Three-item pointer entry and top-card reachability held expansion; five-item wheel scrolling reached capture 5; twenty-item End navigation reached capture 20. No visible jump was observed in these samples. Bounded visible subsets fit the work area. |
| Q49, Q16, Q18 | NOT RUN | Repeated cold/warm latency distributions, selection leave/re-entry, all edge/orientation placements, full wheel traversal and adjacent/nonadjacent Alt-reorder in both directions. The computer-use drag API has no held-modifier argument; Alt-drag was not improvised with other input automation. |
| Q15, Q19, Q32 | PASS | Clicked nonprimary badge 3/3 to focus without editing, observed Focus/Selected text, Space-selected it, Home focused 1/3 while retaining selection, Ctrl+C reached the image sink, Delete dismissed only selected capture 3 while leaving focused capture 1, and Enter opened capture 1. Twenty-item End and Escape navigation also worked. |
| Q15, Q19, Q50 | NOT RUN | Exact copied pixel identity, nonadjacent multi-selection across scrolling, full Tab/button traversal and exact native multi-file membership/order after reorder. The existing sink records format names only; it cannot prove file identity/order or external delivery. |
| Q19 | NOT RUN | Light/system theme changes, high contrast, screen reader and changed Windows text scaling. Visible dark-theme controls and accessible labels were inspected, not treated as broad accessibility acceptance. |
| Q21 | PASS | Apply/close copied the managed image without creating an export. Export PNG used the isolated configured directory, created a PNG, showed its full path and enabled Open export folder. Repeat export remembered its filename, prompted for overwrite and accepted replacement of the synthetic file. Cancellation reported no file written, preserved the prior export hash and added no clipboard write. |
| Q21 | NOT RUN | Open export folder interaction, alternate destinations, failure UX, overwrite decline and Ctrl+S after confirmed canvas/button focus. Export buttons were exercised; a chord while a text input retained focus was not counted as shortcut acceptance. |
| Q08–Q12, Q31 | NOT RUN | New screen capture, global hotkeys, cursor, monitor/DPI/cross-monitor/protected-content and receiver checks. Synthetic isolated interaction is not proof of these production paths. |
| Q30, Q12, Q50 | NOT AVAILABLE | Codex/ChatGPT desktop automation is excluded by the installed skill; no equivalent receiver lane with exact payload inspection was used. Other receivers were not installed or acquired. Existing user-reported successes remain historical, not fresh passes. |
| Q04–Q07, Q13, Q14, Q24–Q29 | NOT RUN | New interactive pin/history/storage/OS clipboard/OCR/settings/startup/sleep/Explorer-restart acceptance. Relevant short automated tests and packaged probes below pass; real fault scenarios were not created. |
| Q41 | NOT AVAILABLE | No already available clean Windows 10/11 profile was used. No account, VM, hardware or receiver application was installed. |
| Q34 | PASS | Five-second process sample with 20 synthetic captures after editor/export interaction: initial working set 232.03 MiB, private bytes 140.46 MiB, 1,225 process handles, CPU delta 171.875 ms; final working set 232.00 MiB. This is an interaction-fixture process sample, not fresh tray idle or a leak result. |
| Q34 | NOT RUN | Cold CLR/startup, hotkey-to-overlay, selection-to-crop/file/dock/OS clipboard, first OCR, 1080p/4K/8K distributions, Snipping Tool comparison and receiver latency. No stage omitted from the sample is claimed measured. |
| Q35 | NOT RUN | The 7,200-second test, all-day use and equivalent prolonged/high-resource work were explicitly excluded. Gate remains open. |
| Q36, Q44 | NOT RUN | Hosted CI/CodeQL/protection, remote setup, publication, tag and GIF. No remote or tag was created, nothing pushed or published, and no messages were sent. |

UI-helper limitations: an initial occluded-window snapshot did not match its accessibility target; subsequent work activated the synthetic window first. Some accessibility-index clicks returned `coordinate input geometry is unavailable` or a bounds error. Refreshing and using observed screenshot coordinates recovered the controls. Accessibility text sometimes lagged the rendered shelf; final observations were checked against rendered state and the isolated state report. These are tool limitations, not recorded app PASS or app FAIL.

## Short automated verification

| Check | Result |
|---|---|
| Locked restore; Release warnings-as-errors build | PASS; zero build warnings/errors |
| Core tests | PASS; 152/152, no skips |
| Windows integration tests | Initial FAIL: 53 pass, 1 foreground-preservation assertion failure during desktop focus activity. Isolated rerun PASS: 54/54, no skips. No code change was needed; concurrent UI work was avoided on rerun. |
| Format verification and Git whitespace check | PASS |
| Live NuGet audit, all five projects including transitive packages | PASS; no feed-reported vulnerabilities |
| Native/model/source-receipt integrity and inventory | PASS; Tesseract VERSION 5.5.3 at pinned patched source, Leptonica 1.88.0; codec inventory empty. This is not an exhaustive native CVE scan. |
| Native-receipt/release-gate/signing/dependency fixtures | PASS; 8 / 10 / 8 / 16 fixtures respectively; no signing credential or network access in signing fixtures |
| Isolated installer/file-upgrade/retention fixtures | PASS; checksum/path admission, hidden ancestor, fresh/obsolete upgrade, rollback, separate-data preservation and bounded dry-run cleanup policy |
| Existing portable ZIP/setup/bundle and embedded payload | PASS; complete inventory and digest verification. No new package or tag generated. |
| Packaged reliability and six-case OCR corpus/malformed/cancellation probes | PASS; offscreen synthetic data and injected clipboard, not real acceptance |

Raw local reports and synthetic export remain ignored under `artifacts/takeover-checks/`; the synthetic interaction cache is isolated in Windows Temp and the fixture exited. No private runtime data was copied into the tracked report.

## Exact release status

Open (37): Q01–Q16, Q18–Q22, Q24–Q32, Q34–Q36, Q41, Q44, Q49, Q50.

Checked (14): Q17, Q23, Q33, Q37–Q40, Q42, Q43, Q45–Q48, Q51.

All 37 open entries are required P0/P1 gates. Stable packaging is blocked; prerelease gate admission does not authorize or establish publication. No checkbox changed in this pass. Q51 remains closed by the recorded source-built upgrade/package evidence; its superseded open-status paragraph is now explicitly historical. Current acceptance documents remain in the final tree. Source and deferred preview-image bytes are preserved.

## Local history privacy cleanup

The owner approved author name How-e and GitHub noreply metadata. The starting history had 59 commits with private-domain author and committer metadata throughout; one resource-instruction blob contained a machine-specific path. Current instructions now resolve the portable folder from the repository root. No original private address is reproduced in this report.

The external complete Git bundles were verified before rewriting. The local rewrite retains the 59-commit source sequence plus this documentation update, preserves original dates and parent topology, and verifies identical non-Markdown source/image trees for every commit. Author and committer metadata use the approved noreply identity. Task scopes and internal planning wording are removed from commit messages. Stale queue, milestone and shelf-decision snapshots are removed from older trees, and historical Markdown is scrubbed of task identifiers and planning handoffs. Historical packaging at those old points may therefore lack the queue consumed by its release parser; the current final tree retains all current release documents and release behavior. Useful source history and technical evidence remain.

All 12 refs present at rewrite were handled, including 11 Codex refs pointing directly to tree snapshots. No recovery branch/tag/ref was left. Current implementation references in the milestone ledger are updated; older validation/queue/package identifiers remain explicitly historical and can be related through the external commit map. The only final-tree differences from the starting source are this report and the documented privacy/evidence edits to README, queue, milestone, validation, user-testing, resource-testing and dependency-review Markdown. Current code, configuration, tests and preview images are identical.

Final verification: expired local reflogs and pruned superseded objects after verifying the external backups. A complete stored-object audit found zero Gmail-domain or machine-user-path findings and zero planning-message findings; `git fsck --full --no-reflogs` passed. Post-rewrite Release build passed with zero warnings/errors and the full test run passed **152 core + 54 Windows integration tests**, no failures/skips. The actual release parser admits alpha with 37 required entries open and rejects stable with those exact entries. Main has 60 commits; working tree is clean, no remotes/tags/recovery refs exist, and nothing was pushed or published. The external original-history bundles intentionally retain the original private metadata for recovery; they are not part of this repository.
