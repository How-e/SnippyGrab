# Dependency policy and native OCR review

Reviewed 2026-10-06. `scripts/audit-dependencies.ps1` parses .NET JSON output version 1, requires all four projects and a feed, and fails for any direct/transitive vulnerability, unsupported report, query error or failed command. All severities fail; no NuGet exceptions exist. Fourteen isolated policy fixtures cover clean, all severities, transitive findings and invalid reports. Restore also promotes NU1900–NU1905 to errors. This uses the [documented JSON audit interface](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-package-list).

CI and release run the policy after compilation. GitHub Actions references are pinned to upstream commit IDs resolved from their v4/v3 tags; Dependabot remains enabled. Hosted execution is still pending .

## Components outside the NuGet advisory feed

`scripts/ocr-components.json` pins SHA-256 for the exact native x64 Tesseract/Leptonica binaries distributed by Tesseract wrapper 5.2.0 and the English model provisioned from immutable tessdata_fast commit `87416418657359cb625c412a48b6e1d6d41c29bd`. Build/package auditing checks these files; runtime OCR additionally rejects a modified, oversized or redirected model before creating the native engine. Provisioning is build-time only. Runtime has no downloads or alternate language/model selection.

The bundled library filenames are `tesseract50.dll` and `leptonica-1.82.0.dll`. Hashes identify bytes, not a complete inventory of compiled codec versions. NuGet reporting is not proof that native code is vulnerability-free.

Upstream Tesseract has model-deserialization advisories affecting versions through 5.5.3, including [GenericVector model buffer writes](https://github.com/tesseract-ocr/tesseract/security/advisories/GHSA-88qp-4g94-3rf3) and [FullyConnected layer mismatch](https://github.com/tesseract-ocr/tesseract/security/advisories/GHSA-q44c-23p6-5mw6). The [Convolve advisory](https://github.com/tesseract-ocr/tesseract/security/advisories/GHSA-7j76-5rq5-5jg8) explicitly describes trusted traineddata as a workaround. Runtime digest validation removes arbitrary model admission in SnippyGrab; it does not patch those native libraries or defend against a same-user process replacing files between checks and use.

Leptonica 1.82.0 is older than the [current upstream releases](https://github.com/DanBloomberg/leptonica/releases). Only app-encoded bounded PNGs enter OCR from ordinary capture/import; malformed external imports first go through Windows codecs. Native codec dependencies still require an inventory, applicable-advisory review and a tested upgrade before broad stable security acceptance.  tracks this concrete follow-up after the / policy/review closure. No approval of vulnerable native components is implied by a passing NuGet/integrity check.

Packaged unsigned alpha builds remain suitable for local evaluation under the documented trust boundary, not a claim of stable security acceptance.
