# Releasing

1. Provision OCR, locked restore, Release build with warnings as errors, tests, format verification.
2. Complete docs/ACCEPTANCE.md and update docs/VALIDATION.md. Keep incomplete builds prerelease.
3. Update CHANGELOG.md; run `pwsh ./scripts/package.ps1 -Version 0.1.0-alpha`.
4. Run the bundle executable with `--self-test` on an interactive desktop; test install/removal in a disposable user profile.
5. Once a GitHub remote exists, push a reviewed version tag. The tag workflow builds/tests and publishes ZIP + SHA-256 using the ephemeral GitHub token. Ordinary PR builds need no secrets.

.NET is bundled. OCR native DLLs and English model must stay beside the executable. OCR may additionally require Microsoft VC++ 2015–2022 x64 redistributable. No binaries/models in Git; provisioning pins model commit and SHA-256. Full dependency licenses accompany releases.

Initial builds are unsigned; SmartScreen can warn. SHA-256 verifies download integrity, not publisher identity. Add Authenticode through a protected environment/external signer, then regenerate checksums. Never put a PFX/private key in source. SDK servicing updates can change binaries; compilation is deterministic for the same SDK/dependencies, ZIP timestamps are not bit-for-bit reproducible.

After publication, enable private vulnerability reporting, secret scanning/push protection, required CI/CodeQL, protected main and restricted tags. These GitHub settings cannot be configured by source files alone.
