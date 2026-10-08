# Latest verified GitHub publication

Published unsigned prerelease: [0.1.0-alpha.acceptance.20261007.8](https://github.com/How-e/SnippyGrab/releases/tag/v0.1.0-alpha.acceptance.20261007.8), 2026-10-08, clean source `08c830952a68cfb162abf4198f320f8eeac30e34`. All **51 acceptance entries are closed / zero required gates open** within the recorded owner-approved scope. See [final acceptance and limits](FINAL-FOUR-ACCEPTANCE-20261007.md).

Both final-source CI jobs and CodeQL passed. [Release workflow 37726837608](https://github.com/How-e/SnippyGrab/actions/runs/37726837608) passed tagged build, all **171 core + 61 Windows tests**, boundary/security fixtures, audit, packaging, embedded installer verification and corrected automated upload without manual recovery. Exactly four public assets were independently downloaded and verified: ZIP, setup and their SHA-256 files. Native reliability and OCR corpus/error/cancellation also passed against the downloaded executable; source/version/Dirty=false provenance matches the approved tag.

Gitleaks scanned all 135 reachable candidate commits without findings. GitHub reported zero open CodeQL, Dependabot and secret-scanning alerts before publication. ZIP SHA-256: `DD2C195B416A5E01D02598465C8E5AB28FF5465E5DACC5CDC177DF1A95E8AF85`; setup SHA-256: `67347602A7413F5D0F3FAFCB82DCABFC7681FDC53C496CE5C91C183E88859BF2`. The earlier alpha and failed CI runs retain their historical identities. No existing artifact or tag was rewritten.

Windows 10, exact hotkey-overlay distributions, certified cold boot, physical 4K/8K workflow, all-day workload and signed-publisher acceptance remain unmeasured/unsupported claims. Closure uses the owner's explicit scope decisions; only an unsigned prerelease is published. The GIF is an explicitly labeled illustration substituted at owner request.

## Initial GitHub publication — historical snapshot

Published prerelease: **0.1.0-alpha.queue.20261006.3**. Repository: [How-e/SnippyGrab](https://github.com/How-e/SnippyGrab). This remains an unsigned alpha: 34 acceptance gates remain open after Q36/Q35/Q06 closure, including full receiver/hardware coverage. The [two-hour synthetic resource review](RESOURCE-REVIEW-20261007.md) passes; all-day use remains unverified. See [current reconciliation](REMAINING-WORK.md) for recorded protection/security/check evidence and the upload recovery limitation. No stable readiness claim is made.

The repository was empty when inspected on 2026-10-07. Initial publication preserves the sanitized local commit history and publishes one version tag and one release. Earlier ignored local packages retain their original provenance and are not uploaded. The candidate is rebuilt from the clean matching tag so its provenance identifies the published source, rather than the superseded pre-rewrite source identity.

Local verification before publication:

- Locked restore, Release build with warnings as errors, and format verification passed. Build: zero warnings/errors.
- All 206 tests passed: 152 core and 54 Windows integration, no failures/skips.
- NuGet vulnerability audit passed for all five projects, including transitive dependencies and all severities. Audit policy fixtures passed.
- Native source-receipt/hash/inventory validation passed: reviewed Tesseract 5.5.3 snapshot, Leptonica 1.88.0, no external image codecs. The current upstream published Tesseract advisory list matches the ten entries mapped in NATIVE-OCR.md. Leptonica's GitHub advisory list returned no published entries; this is not an exhaustive CVE clearance.
- Native provenance, stable/prerelease gate, signing, installer payload/path/integrity, upgrade rollback/data preservation, and artifact-retention fixtures passed.
- Gitleaks 8.30.1, downloaded from its upstream release and checked against its release checksum, scanned all reachable history with no secrets found. A separate metadata/patch privacy scan found no private Gmail address or machine-user path. Git object integrity passed.
- GitHub secret scanning and push protection were confirmed enabled. Private vulnerability reporting, vulnerability alerts and automated dependency security fixes were enabled.
- Fresh clean-tag packaging passed complete bundle inventory, ZIP/setup checksums and independent embedded installer-payload verification. Packaged reliability, six-case OCR corpus/malformed-input/cancellation and overlay-layout probes passed.
- The broad desktop self-test initially failed intermittently at its rendered blue-window pixel assertion. An independent synthetic window pixel probe confirmed expected blue pixels; one full packaged retry passed without source changes. Later reruns reproduced the failure. The diagnostic now explicitly exposes its synthetic HWND, keeps it topmost during the probe, and waits up to three seconds for its own window and actual blue pixels instead of assuming a fixed 200 ms delay. Three consecutive fresh development self-tests passed with this diagnostic change, including native physical capture and local synthetic OLE delivery. The initial failures remain in ignored audit reports; their precise trigger was not established. This is bounded synthetic diagnostic evidence, not broad hardware/receiver acceptance.

Publication sequence: push main, require successful hosted CI and CodeQL plus no open security alerts, then push only the matching candidate tag. The release workflow rebuilds and verifies ZIP/setup/checksums from that tag. A failed hosted check blocks publication of downloadable artifacts. Final hosted status is available in [Actions](https://github.com/How-e/SnippyGrab/actions) and the [release](https://github.com/How-e/SnippyGrab/releases/tag/v0.1.0-alpha.queue.20261006.3).

The first hosted CI clean build exposed native source Git line-ending warnings and MSVC C4849, promoted to errors through MSBuild Exec. The recipe now preserves upstream LF checkouts and disables only the known ignored optional SIMD-pragma warning. Source pins, codec restrictions and managed warnings-as-errors policy remain in effect. The changed recipe invalidates the previous native receipt and requires fresh source-built libraries and renewed package verification.

On 2026-10-07 the owner also reported successful drop and paste in Codex, ChatGPT, VS Code and a browser. These user-reported receiver passes are recorded in USER-TESTING.md; unspecified build/receiver versions, exact selection/order and delayed-read cases remain open.

These checks establish the documented alpha publication boundary. They do not prove absence of unknown vulnerabilities, signed publisher identity, exhaustive native fuzzing, or completion of the remaining desktop acceptance gates.
