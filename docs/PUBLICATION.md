# Initial GitHub publication

Release candidate: **0.1.0-alpha.queue.20261006.3**. Target repository: [How-e/SnippyGrab](https://github.com/How-e/SnippyGrab). This remains an unsigned alpha: 37 acceptance gates remain open, including real receiver/hardware coverage and prolonged resource tests. No stable readiness claim is made.

The repository was empty when inspected on 2026-10-07. Initial publication preserves the sanitized local commit history and publishes one version tag and one release. Earlier ignored local packages retain their original provenance and are not uploaded. The candidate is rebuilt from the clean matching tag so its provenance identifies the published source, rather than the superseded pre-rewrite source identity.

Local verification before publication:

- Locked restore, Release build with warnings as errors, and format verification passed. Build: zero warnings/errors.
- All 206 tests passed: 152 core and 54 Windows integration, no failures/skips.
- NuGet vulnerability audit passed for all five projects, including transitive dependencies and all severities. Audit policy fixtures passed.
- Native source-receipt/hash/inventory validation passed: reviewed Tesseract 5.5.3 snapshot, Leptonica 1.88.0, no external image codecs. The current upstream published Tesseract advisory list matches the ten entries mapped in NATIVE-OCR.md. Leptonica's GitHub advisory list returned no published entries; this is not an exhaustive CVE clearance.
- Native provenance, stable/prerelease gate, signing, installer payload/path/integrity, upgrade rollback/data preservation, and artifact-retention fixtures passed.
- Gitleaks 8.30.1, downloaded from its upstream release and checked against its release checksum, scanned all reachable history with no secrets found. A separate metadata/patch privacy scan found no private Gmail address or machine-user path. Git object integrity passed.
- GitHub secret scanning and push protection were confirmed enabled. Private vulnerability reporting, vulnerability alerts and automated dependency security fixes were enabled.

Publication sequence: push main, require successful hosted CI and CodeQL plus no open security alerts, then push only the matching candidate tag. The release workflow rebuilds and verifies ZIP/setup/checksums from that tag. A failed hosted check blocks publication of downloadable artifacts. Final hosted status is available in [Actions](https://github.com/How-e/SnippyGrab/actions) and the [release](https://github.com/How-e/SnippyGrab/releases/tag/v0.1.0-alpha.queue.20261006.3).

These checks establish the documented alpha publication boundary. They do not prove absence of unknown vulnerabilities, signed publisher identity, exhaustive native fuzzing, or completion of the remaining desktop acceptance gates.
