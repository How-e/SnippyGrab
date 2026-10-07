# Releasing

1. Provision OCR, locked restore, Release build with warnings as errors, tests, format verification.
2. Complete docs/ACCEPTANCE.md and update docs/VALIDATION.md. Keep incomplete builds prerelease.
3. Update CHANGELOG.md; run `pwsh ./scripts/package.ps1 -Version 0.1.0-alpha`.
4. Run the bundle executable with `--self-test` on an interactive desktop; test install/removal in a disposable user profile.
5. Once a GitHub remote exists, push a reviewed version tag. The tag workflow builds/tests and publishes ZIP + SHA-256 using the ephemeral GitHub token. Ordinary PR builds need no secrets.

.NET is bundled. OCR native DLLs and English model must stay beside the executable. OCR may additionally require Microsoft VC++ 2015–2022 x64 redistributable. No binaries/models in Git; provisioning pins model commit and SHA-256. Full dependency licenses accompany releases.

Initial builds are unsigned; SmartScreen can warn. SHA-256 verifies download integrity, not publisher identity. Add Authenticode through a protected environment/external signer, then regenerate checksums. Never put a PFX/private key in source. SDK 10.0.400 and separate build/publish lock files are pinned. Update these deliberately when servicing the runtime; compilation is deterministic for the same SDK/dependencies, ZIP timestamps are not bit-for-bit reproducible.

After publication, enable private vulnerability reporting, secret scanning/push protection, required CI/CodeQL, protected main and restricted tags. These GitHub settings cannot be configured by source files alone.

Release metadata derives from assembly informational version, including commit identity. Bundles carry BUILD-PROVENANCE.json (version, commit, dirty status, SDK, lock hashes and native digests) plus the curated CHANGELOG. Hosted tag packaging requires a clean matching tag. Archive entries are sorted with fixed 1980 timestamps; identical inputs produce identical ZIPs under the same compression runtime. This is archive reproducibility, not proof that independent ReadyToRun/signing builds are byte-identical. Use scripts/archive.ps1 to compare fixed-input archives. Local development packages explicitly record a dirty source tree.

Installation/upgrade stages and verifies the entire bundle before switching directories. An older application directory is moved to a SnippyGrab-backup-<id> sibling, keeping obsolete sidecars out of the active installation and preserving unknown files for rollback. Failed registration restores the previous files/shortcut/registry values. User captures/settings remain in their separate data directory. Successful upgrade backups are retained for deliberate review/removal. The isolated file lab covers fresh install, obsolete sidecars, rollback, checksum failure and user-data preservation; it does not prove Windows 10/11 clean-profile/login or Apps uninstall acceptance.

Packaging retains the latest three registered build groups. Cleanup checks direct-child paths, rejects redirects, compares full file inventories/hashes and skips running or modified outputs. Run pwsh scripts/artifact-retention.ps1 to review a plan; -Apply executes it. Older unregistered bundles and failed staging directories are left for explicit review. The isolated fixture verifies dry-run behavior, three-build retention, modified/unowned preservation and path rejection. Cleanup never targets application cache/settings or installed backups.
