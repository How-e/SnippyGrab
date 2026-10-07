# Pinned native OCR build and security review

Reviewed 2026-10-06. The managed NuGet wrapper remains 5.2.0. Its bundled old native libraries are replaced in development, Windows integration tests and publishing by the source-built libraries below. No native binaries are committed and no fallback to the NuGet binaries is permitted.

| Library | Source pin | Reported version | Distributed filename |
|---|---|---|---|
| Tesseract | [db20f322d03664d1e878e2fbf6e904f5da755594](https://github.com/tesseract-ocr/tesseract/commit/db20f322d03664d1e878e2fbf6e904f5da755594) | 5.5.3 | tesseract50.dll |
| Leptonica | [8ad618f103972eed12f195499b92cff1ab37067b](https://github.com/DanBloomberg/leptonica/commit/8ad618f103972eed12f195499b92cff1ab37067b) | 1.88.0 | leptonica-1.82.0.dll |

The filenames preserve the managed wrapper's lookup ABI, not the upstream versions. The Tesseract snapshot contains security fixes after the 5.5.3 release while its VERSION string still says 5.5.3. Source identity and fix ancestry are therefore required; a version string alone cannot establish patch status.

## Build and provenance

Install Visual Studio 2022 C++ x64 tools, Windows SDK and CMake; PowerShell 7 and Git are required. `pwsh scripts/build-native-ocr.ps1` locates CMake with vswhere (or accepts -CMake), fetches each exact Git commit, verifies HEAD and admits only the reviewed output-name CMake additions. It builds x64 Release with four build jobs. The normal App build invokes it; integration tests and publish explicitly use its outputs. Build-time networking fetches sources; runtime has no downloads.

Leptonica's external GIF/JPEG/PNG/TIFF/WebP/OpenJPEG/zlib options are all OFF. Tesseract's TIFF/curl/libarchive integration, graphics, training tools, native-host optimization and OpenMP are OFF. The legacy engine API remains compiled because the wrapper binds its orientation export during initialization; the production engine selects LstmOnly. Built-in Leptonica helpers/format code may still exist, but production passes no encoded image to native readers. Windows WIC decodes bounded admitted content to RGBA rows. Native recognition, allocation and scaling remain.

The ignored cache is artifacts/native-ocr. Build outputs include NATIVE-OCR-PROVENANCE.json with source commits, recipe SHA-256, generator/configuration and the exact two DLL hashes. Policy verifies that receipt against the committed sources/recipe and file bytes before version inventory or packaging. This is a trusted build-machine receipt, not a signed external attestation. Same-user modification of source/build tools/receipts remains outside the application's isolation boundary. Rebuild sources are retained for inspection; no source cache is recursively deleted by provisioning. Old source-build directories can be archived deliberately after recipe changes.

Upstream compilation currently emits MSVC warning C4849 for a SIMD OpenMP reduction pragma with OpenMP off; the compiler ignores that optimization. The managed warnings-as-errors build still has zero warnings/errors. Build timestamps/compiler/path differences can change native DLL hashes; each build records its own hashes. No independent native byte-reproducibility claim is made. Q42 carries that separate release evidence.

## Advisory mapping

These upstream fixes were reviewed in source and all eleven listed commits were verified as ancestors of the pinned Tesseract snapshot. The advisory IDs refer to [upstream advisories](https://github.com/tesseract-ocr/tesseract/security/advisories); their release-version metadata can lag unreleased fixes.

| Advisory / boundary | Reviewed upstream fix included in pin |
|---|---|
| GHSA-88qp-4g94-3rf3 GenericVector independent counts | [56e09ca1](https://github.com/tesseract-ocr/tesseract/commit/56e09ca12e751623fe796ce1554ce704bffd2ef0): cap reserved and require used between zero and reserved |
| GHSA-q44c-23p6-5mw6 FullyConnected dimensions | [103dc134](https://github.com/tesseract-ocr/tesseract/commit/103dc134eb36411ddc6833ec20aa2c76795bd0ff): weight matrix matches layer inputs/outputs |
| GHSA-jgq8-pprg-vc68 LSTM gates | [b494ac18](https://github.com/tesseract-ocr/tesseract/commit/b494ac18925f9d9aff9ef5815475de9943ab19bf): gate/input/state/softmax dimensions checked |
| GHSA-f6h7-cqr4-6fx4 empty network stacks | [55277123](https://github.com/tesseract-ocr/tesseract/commit/552771236b0d80cbdb0c7dd856120fa21a4672e5): reject empty stacks |
| GHSA-7v9h-3q3m-w68g recoder values | [c94a5532](https://github.com/tesseract-ocr/tesseract/commit/c94a5532ee04db5a4919542832fd94caee5ea58f): reject out-of-range codes |
| GHSA-2hm8-q5c7-c373 unicharset count/insert mismatch | [2d04d640](https://github.com/tesseract-ocr/tesseract/commit/2d04d640db2e8c7e3bab2369d599343b5a8b8443): reject ID/insert desynchronization |
| GHSA-rphx-x795-5qjv legacy inttemp counts | [8b057468](https://github.com/tesseract-ocr/tesseract/commit/8b0574680f3b22f246ade6a4c8e3029104255c63): validate serialized counts |
| GHSA-5j2p-r5vc-q7f3 legacy NormProtos buffer | [1bda5079](https://github.com/tesseract-ocr/tesseract/commit/1bda5079b1c8a7e25f523486837426903d29ce84): bounded token extraction |
| GHSA-7j76-5rq5-5jg8 Convolve shape/overflow | [2f4d2f4b](https://github.com/tesseract-ocr/tesseract/commit/2f4d2f4bf45c363785d7bf1da29b6628f8939a72): checked deserialization arithmetic/dimensions |
| GHSA-x3vq-7rr7-5x3h DAWG allocation/edges | [55287a94](https://github.com/tesseract-ocr/tesseract/commit/55287a94b8044c05ce3fd10f5aca6ebbd238e518) and [230b6132](https://github.com/tesseract-ocr/tesseract/commit/230b6132926e5b774de1d2ba5f66013076f14a79): bounded allocation, edge structure and zero-count checks |

The former embedded gif 5.2.1/jpeg-turbo 2.1.4/png 1.6.37/tiff 4.4.0/zlib 1.2.13 libraries are absent from this build. Their advisories are removed with those dependencies, not silently accepted. Exported getImagelibVersions must be empty. Model admission still requires the exact pinned English SHA-256 before native engine creation; arbitrary models/languages are unsupported. Leptonica is now source-pinned 1.88.0 rather than the 2022 build; native resource/fuzzing risks remain as documented in the threat model.

## Validation and licenses

Development Windows tests cover the actual source-built DLLs: known RGBA/alpha/resolution/native metadata, unavailable native PNG reader, cancellation/malformed/missing/modified model paths, full/area terminal/traceback/dialog corpus and exact CS1002. The repeatable `--check-ocr-corpus <absolute report>` checks six synthetic full/area cases plus malformed input and cancellation directly in any packaged executable. Resource probes run real OCR alongside pin/revision/editor workloads. Policy fixtures reject missing/stale/modified recipe/source/configuration/hash receipts.

Full Tesseract and Leptonica licenses are copied from the exact source snapshots to licenses/. The wrapper, English model and .NET license notices remain. No external codec license is needed for a library not compiled/linked into these outputs. Actual package checks/results belong in VALIDATION.md; source review and corpus tests do not claim exhaustive native fuzzing or all-day resource acceptance.
