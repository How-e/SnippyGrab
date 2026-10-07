# Dependency policy and native OCR review

Reviewed 2026-10-06. `scripts/audit-dependencies.ps1` parses .NET JSON output version 1, requires all four projects and a feed, and fails for any direct/transitive vulnerability, unsupported report, query error or failed command. All severities fail; no NuGet exceptions exist. Fourteen isolated policy fixtures cover clean, all severities, transitive findings and invalid reports. Restore also promotes NU1900–NU1905 to errors. This uses the [documented JSON audit interface](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-package-list).

CI and release run the policy after compilation. GitHub Actions references are pinned to upstream commit IDs resolved from their v4/v3 tags; Dependabot remains enabled. Hosted execution is still pending .

## Components outside the NuGet advisory feed

`scripts/ocr-components.json` pins SHA-256 for the exact native x64 Tesseract/Leptonica binaries distributed by Tesseract wrapper 5.2.0 and the English model provisioned from immutable tessdata_fast commit `87416418657359cb625c412a48b6e1d6d41c29bd`. Build/package auditing checks these files; runtime OCR additionally rejects a modified, oversized or redirected model before creating the native engine. Provisioning is build-time only. Runtime has no downloads or alternate language/model selection.

The bundled library filenames are `tesseract50.dll` and `leptonica-1.82.0.dll`. Hashes identify bytes, not a complete inventory of compiled codec versions. NuGet reporting is not proof that native code is vulnerability-free.

Upstream Tesseract has model-deserialization advisories affecting versions through 5.5.3, including [GenericVector model buffer writes](https://github.com/tesseract-ocr/tesseract/security/advisories/GHSA-88qp-4g94-3rf3) and [FullyConnected layer mismatch](https://github.com/tesseract-ocr/tesseract/security/advisories/GHSA-q44c-23p6-5mw6). The [Convolve advisory](https://github.com/tesseract-ocr/tesseract/security/advisories/GHSA-7j76-5rq5-5jg8) explicitly describes trusted traineddata as a workaround. Runtime digest validation removes arbitrary model admission in SnippyGrab; it does not patch those native libraries or defend against a same-user process replacing files between checks and use.

Leptonica 1.82.0 is older than the [current upstream releases](https://github.com/DanBloomberg/leptonica/releases). Only app-encoded bounded PNGs enter OCR from ordinary capture/import; malformed external imports first go through Windows codecs. Native codec dependencies still require an inventory, applicable-advisory review and a tested upgrade before broad stable security acceptance.  tracks this concrete follow-up after the / policy/review closure. No approval of vulnerable native components is implied by a passing NuGet/integrity check.

Packaged unsigned alpha builds remain suitable for local evaluation under the documented trust boundary, not a claim of stable security acceptance.

## Exact native inventory and parser boundary —  follow-up

On 2026-10-06, exported `TessVersion`, `getLeptonicaVersion` and `getImagelibVersions` from the hash-pinned x64 libraries reported the following. `scripts/inventory-native-ocr.ps1` repeats the query, frees owned version strings, refuses digest mismatches and checks expected inventory. Dependency auditing and packaging now execute it; BUILD-PROVENANCE carries expected versions beside digests.

| Embedded component | Actual version | Admission/review status |
|---|---|---|
| Tesseract | **5.0.0**, despite wrapper package 5.2.0 | Native engine/model parser remains; pinned English model only. The upstream [model advisories](https://github.com/tesseract-ocr/tesseract/security/advisories) include FullyConnected/LSTM/recoder/GenericVector/DAWG/count and legacy model-loader failures. LSTM-only and trusted models reduce admission; they do not patch 5.0.0. |
| Leptonica | 1.82.0, compiled Nov 7 2022, MSVC 1933 x64 | Raw-pixel allocation/scaling/recognition helpers remain. [Upstream convolution fix](https://github.com/DanBloomberg/leptonica/commit/f062b42c0ea8dddebdc6a152fd16152de215d614) and later releases need source/build applicability review; a build date alone does not prove backports. |
| libgif | 5.2.1 | Embedded; production OCR does not call native GIF decoding. |
| libjpeg | IJG 6b, libjpeg-turbo 2.1.4 | Embedded; production OCR does not call native JPEG decoding. [No published upstream GHSA entries](https://github.com/libjpeg-turbo/libjpeg-turbo/security/advisories) is not an exhaustive CVE clearance. |
| libpng | 1.6.37 | Embedded version is in the affected range of [GHSA-qvg3-h654-xq3j](https://github.com/pnggroup/libpng/security/advisories/GHSA-qvg3-h654-xq3j). That advisory also requires a particular reader call sequence; version inclusion is not proof of application exploitability. Production OCR no longer calls the native PNG reader. |
| libtiff | 4.4.0 | Embedded; production admits no TIFF and calls no native image decoder. Still needs upstream upgrade/source inventory; unreachable here does not mean patched. |
| zlib | 1.2.13 | Embedded; production does not use the compressed native image readers. This version string does not prove whether optional minizip code was compiled; no unverified subcomponent claim is made. |

Production OCR now admits encoded content through Windows WIC, then copies bounded decoded RGBA rows into a native Pix. It preserves resolution, samples-per-pixel and input-format metadata so Tesseract's alpha handling matches the former PNG input. No compressed PNG/JPEG/GIF/TIFF/BMP bytes reach native Leptonica readers. Tests compare the entire pixel buffer/resolution/samples with the former decoder on a benign fixture; the exact CS1002 recognition regression and full corpus/cancellation/error suite pass. Windows image decoding remains an OS-maintained trust boundary. Model parsing, native allocation/scaling and same-user replacement races remain.

** stays open.** This is a concrete inventory and parser-exposure reduction, not an approved narrower stable scope or a tested upgraded native build. Stable acceptance still needs a source-pinned maintained native build with codec configuration/SBOM/licenses and packaged corpus/cancellation/error checks, or an explicit owner acceptance of a narrower security scope. Existing native hashes were deliberately preserved; no downloaded third-party DLL was substituted under the old identity.

## Source-built native replacement — 

The old-byte inventory above is historical evidence. The current development/test/package pipeline replaces it with pinned maintained Tesseract snapshot db20f322d03664d1e878e2fbf6e904f5da755594 (VERSION 5.5.3 plus later fixes) and Leptonica 8ad618f103972eed12f195499b92cff1ab37067b (1.88.0). Eleven reviewed upstream model-hardening commits were verified as ancestors. External gif/jpeg/png/tiff/webp/openjpeg/zlib dependencies and Tesseract curl/archive/TIFF paths are disabled; exported codec inventory is empty. The legacy orientation API remains for wrapper compatibility, while production uses LstmOnly and the pinned English digest.

[Native build/review](NATIVE-OCR.md) maps all ten reviewed Tesseract advisories to included source fixes, describes source admission, build-receipt hash policy, unchanged managed/model licenses and newly copied native licenses. Native SHA-256 now comes from the verified source-build receipt, tied to committed source pins/recipe, instead of old NuGet binary hashes. A receipt is trusted build provenance, not a signature or byte-reproducibility proof. Same-user races, OS decoder admission, unknown future native issues and prolonged resources remain residual boundaries.

206 development tests pass against the replaced DLLs. Known raw colors/alpha/resolution/metadata, unavailable native PNG codec, full/area corpus, exact CS1002, malformed input, queued cancellation and model/loader failures pass. Eight isolated native-policy fixtures reject stale/missing/tampered build receipts. The development executable's six-case OCR corpus/error/cancellation probe passes. Final packaged validation is recorded in VALIDATION.md before  closure. No claim of vulnerability-free native code or completed stable release is implied.
