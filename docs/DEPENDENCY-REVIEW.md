# Dependency policy

Restore promotes NuGet audit warnings to errors. `scripts/audit-dependencies.ps1` validates the JSON audit format, requires all projects and a feed, and rejects direct/transitive vulnerabilities of any severity, query failures and unsupported reports. No exceptions are configured. Run its fixture tests with dependency changes.

GitHub Actions references use immutable commits and Dependabot. Native OCR sources, recipe and English model are pinned in `scripts/ocr-components.json`; the build receipt ties binary hashes to these inputs. CI and packaging verify integrity and exported versions. Runtime admits only the pinned English model and never downloads models or native components.

Production OCR uses source-built Tesseract and Leptonica with external codecs disabled; WPF supplies bounded, validated raw pixels through the managed wrapper. DLL filenames preserve wrapper compatibility and do not identify the source version. See [NATIVE-OCR.md](NATIVE-OCR.md) for build instructions, pins, advisory mapping, receipt policy and license provenance.

WebP export separately uses pinned libwebp 1.6.0 through an owned pixel-only encoder ABI. Its source/recipe/binary provenance is checked in CI and packaging; this native component is not scanned by the NuGet audit. See [WEBP-ADMISSION.md](WEBP-ADMISSION.md) for the dated advisory/license review, limits and residual risks. OCR's disabled WebP codec stays disabled.

Hash verification identifies admitted bytes, not publisher identity or a guarantee that native code has no vulnerabilities. Review upstream advisories when upgrading sources; retain model/loader/cancellation/raw-pixel regressions and packaged OCR checks. Same-user replacement races, Windows image codecs and unknown future native issues remain within the [threat model](THREAT-MODEL.md).

Detailed audit findings, dated inventories, test transcripts and package result reports belong in external evidence storage or CI artifacts.
